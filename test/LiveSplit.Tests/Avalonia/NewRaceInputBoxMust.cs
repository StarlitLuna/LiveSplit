using System;
using System.Collections.Generic;

using LiveSplit.Avalonia.Dialogs;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class NewRaceInputBoxMust
{
    [Fact]
    public void SeedGameWithMostSimilarSpeedRunsLiveGameName()
    {
        var lookup = new FakeSrlGameLookup(["The Legend of Zelda: Ocarina of Time", "Super Metroid"]);
        var model = new NewRaceInputBoxModel(lookup);

        NewRaceInputBoxState state = model.CreateInitialState("The Legend of Zelda Ocarina", "Any%");

        Assert.Equal("The Legend of Zelda: Ocarina of Time", state.Game);
        Assert.Equal("Any%", state.Category);
        Assert.Equal(lookup.GameNames, state.GameSuggestions);
    }

    [Fact]
    public void RefreshCategorySuggestionsForKnownSpeedRunsLiveGame()
    {
        var lookup = new FakeSrlGameLookup(["Known Game"])
        {
            GameIds = { ["Known Game"] = "known" },
            Categories = { ["known"] = ["Any%", "100%"] },
        };
        var model = new NewRaceInputBoxModel(lookup);

        IReadOnlyList<string> categories = model.GetCategorySuggestions("Known Game");

        Assert.Equal(new[] { "Any%", "100%" }, categories);
    }

    [Fact]
    public void FallBackToCommonRaceCategoriesWhenCategoryLookupFails()
    {
        var lookup = new FakeSrlGameLookup(["Known Game"])
        {
            GameIds = { ["Known Game"] = "known" },
            ThrowOnCategories = true,
        };
        var model = new NewRaceInputBoxModel(lookup);

        IReadOnlyList<string> categories = model.GetCategorySuggestions("Known Game");

        Assert.Equal(NewRaceInputBoxModel.DefaultCategories, categories);
    }

    [Fact]
    public void RequireConfirmationForUnknownSpeedRunsLiveGame()
    {
        var lookup = new FakeSrlGameLookup(["Known Game"])
        {
            GameIds = { ["Known Game"] = "known" },
        };
        var model = new NewRaceInputBoxModel(lookup);

        Assert.True(model.RequiresUnknownGameConfirmation("Mystery Game"));
        Assert.False(model.RequiresUnknownGameConfirmation("Known Game"));
    }

    [Fact]
    public void BuildNewGameRaceRequestLikeMaster()
    {
        var lookup = new FakeSrlGameLookup(["Known Game"]);
        var model = new NewRaceInputBoxModel(lookup);

        NewRaceCreationRequest request = model.BuildCreationRequest("Mystery Game", "Any%");

        Assert.Equal("New Game", request.GameName);
        Assert.Equal("new", request.GameId);
        Assert.Equal("Mystery Game - Any%", request.Category);
    }

    private sealed class FakeSrlGameLookup : ISrlGameLookup
    {
        public FakeSrlGameLookup(IReadOnlyList<string> gameNames)
        {
            GameNames = gameNames;
        }

        public IReadOnlyList<string> GameNames { get; }
        public Dictionary<string, string> GameIds { get; } = [];
        public Dictionary<string, IReadOnlyList<string>> Categories { get; } = [];
        public bool ThrowOnCategories { get; set; }

        public IReadOnlyList<string> GetGameNames() => GameNames;

        public string GetGameIdFromName(string gameName)
            => GameIds.TryGetValue(gameName, out string gameId) ? gameId : null;

        public IReadOnlyList<string> GetCategories(string gameId)
        {
            if (ThrowOnCategories)
            {
                throw new InvalidOperationException("lookup failed");
            }

            return Categories.TryGetValue(gameId, out IReadOnlyList<string> categories)
                ? categories
                : [];
        }
    }
}
