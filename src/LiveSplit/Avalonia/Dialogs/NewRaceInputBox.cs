using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.Web.SRL;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>
/// Asks the user for a game name + category when starting a new race.
/// </summary>
public sealed class NewRaceInputBox : Window
{
    public string Game { get; private set; }
    public string Category { get; private set; }

    private readonly NewRaceInputBoxModel _model;
    private readonly TaskCompletionSource<bool> _result = new();

    public NewRaceInputBox(string initialGame = "", string initialCategory = "", ISrlGameLookup lookup = null)
    {
        _model = new NewRaceInputBoxModel(lookup ?? new SpeedRunsLiveGameLookup());

        Title = "New Race";
        Width = 420;
        Height = 240;
        DialogTheme.ApplyWindow(this);
        CanResize = false;

        NewRaceInputBoxState state = _model.CreateInitialState(initialGame, initialCategory);
        var gameBox = CreateAutocompleteBox(state.GameSuggestions, state.Game);
        var categoryBox = CreateAutocompleteBox(_model.GetCategorySuggestions(state.Game), state.Category);
        gameBox.TextChanged += async (_, _) =>
        {
            IReadOnlyList<string> categories = await Task.Run(() => _model.GetCategorySuggestions(gameBox.Text));
            categoryBox.ItemsSource = categories;
        };

        var ok = new Button { Content = "OK", Width = 80, IsDefault = true };
        ok.Click += async (_, _) =>
        {
            if (_model.RequiresUnknownGameConfirmation(gameBox.Text))
            {
                MessageResult confirm = await new MessageDialog(
                    "Game Not Found",
                    "The game you entered could not be found in the SpeedRunsLive Game List. Are you sure you would like to start a race with a New Game?",
                    MessageDialog.Buttons.YesNo).ShowDialogResultAsync(this);
                if (confirm != MessageResult.Yes)
                {
                    return;
                }
            }

            Game = gameBox.Text ?? string.Empty;
            Category = categoryBox.Text ?? string.Empty;
            _result.TrySetResult(true);
            Close();
        };
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        cancel.Click += (_, _) => { _result.TrySetResult(false); Close(); };

        Opened += (_, _) => gameBox.Focus();

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Game:" },
                gameBox,
                new TextBlock { Text = "Category:" },
                categoryBox,
                new TextBlock
                {
                    Text = "Creating a race without any opponents is against the rules.",
                    Foreground = global::Avalonia.Media.Brushes.Gray,
                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 0),
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Margin = new Thickness(0, 12, 0, 0),
                    Children = { ok, cancel },
                },
            },
        };

        Closed += (_, _) =>
        {
            if (!_result.Task.IsCompleted)
            {
                _result.TrySetResult(false);
            }
        };
    }

    private static AutoCompleteBox CreateAutocompleteBox(IReadOnlyList<string> items, string text)
        => new()
        {
            ItemsSource = items,
            Text = text ?? string.Empty,
            FilterMode = AutoCompleteFilterMode.Contains,
            IsTextCompletionEnabled = true,
            MinimumPrefixLength = 0,
        };

    public async Task<bool> ShowDialogAsync(Window owner)
    {
        if (owner is not null)
        {
            await ShowDialog(owner);
        }
        else
        {
            Show();
        }

        return await _result.Task;
    }
}

public interface ISrlGameLookup
{
    IReadOnlyList<string> GetGameNames();
    string GetGameIdFromName(string gameName);
    IReadOnlyList<string> GetCategories(string gameId);
}

internal sealed record NewRaceInputBoxState(
    string Game,
    string Category,
    IReadOnlyList<string> GameSuggestions);

internal sealed record NewRaceCreationRequest(
    string GameName,
    string GameId,
    string Category);

internal sealed class NewRaceInputBoxModel
{
    private readonly ISrlGameLookup _lookup;

    public static IReadOnlyList<string> DefaultCategories { get; } = ["Any%", "Low%", "100%"];

    public NewRaceInputBoxModel(ISrlGameLookup lookup)
    {
        _lookup = lookup;
    }

    public NewRaceInputBoxState CreateInitialState(string initialGame, string initialCategory)
    {
        IReadOnlyList<string> gameNames = SafeGetGameNames();
        string game = gameNames.FindMostSimilarValueTo(initialGame) ?? initialGame ?? string.Empty;
        return new NewRaceInputBoxState(game, initialCategory ?? string.Empty, gameNames);
    }

    public IReadOnlyList<string> GetCategorySuggestions(string gameName)
    {
        try
        {
            string gameId = SafeGetGameId(gameName);
            IReadOnlyList<string> categories = _lookup.GetCategories(gameId);
            return categories.Count > 0 ? categories : DefaultCategories;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return DefaultCategories;
        }
    }

    public bool RequiresUnknownGameConfirmation(string gameName)
        => string.IsNullOrEmpty(SafeGetGameId(gameName));

    public NewRaceCreationRequest BuildCreationRequest(string gameName, string category)
    {
        gameName ??= string.Empty;
        category ??= string.Empty;
        string gameId = SafeGetGameId(gameName);
        if (string.IsNullOrEmpty(gameId))
        {
            return new NewRaceCreationRequest("New Game", "new", $"{gameName} - {category}");
        }

        return new NewRaceCreationRequest(gameName, gameId, category);
    }

    private IReadOnlyList<string> SafeGetGameNames()
    {
        try
        {
            return _lookup.GetGameNames();
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return [];
        }
    }

    private string SafeGetGameId(string gameName)
    {
        try
        {
            return _lookup.GetGameIdFromName(gameName ?? string.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return null;
        }
    }
}

internal sealed class SpeedRunsLiveGameLookup : ISrlGameLookup
{
    public IReadOnlyList<string> GetGameNames()
        => SpeedRunsLiveAPI.Instance.GetGameNames().ToList();

    public string GetGameIdFromName(string gameName)
        => SpeedRunsLiveAPI.Instance.GetGameIDFromName(gameName);

    public IReadOnlyList<string> GetCategories(string gameId)
        => SpeedRunsLiveAPI.Instance.GetCategories(gameId).ToList();
}
