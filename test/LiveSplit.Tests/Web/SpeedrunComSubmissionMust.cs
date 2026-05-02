using System.Collections.Generic;

using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Web.Share;

using SpeedrunComSharp;

using Xunit;

namespace LiveSplit.Tests.Web;

public class SpeedrunComSubmissionMust
{
    [Fact]
    public void RejectRunThatAlreadyHasSpeedrunComIdWithoutAuthenticating()
    {
        IRun run = new StandardRunFactory().Create(new StandardComparisonGeneratorsFactory());
        run.Metadata.RunID = "existing-run";

        bool accepted = SpeedrunCom.ValidateRun(run, out string reasonForRejection);

        Assert.False(accepted);
        Assert.Equal("This run already exists on speedrun.com.", reasonForRejection);
    }

    [Theory]
    [InlineData("https://example.com/watch?v=1", "https://example.com/watch?v=1")]
    [InlineData("youtu.be/example", "http://youtu.be/example")]
    public void NormalizeVideoUrlBeforeSubmitting(string text, string expected)
    {
        Assert.True(SpeedrunComSubmissionOptions.TryNormalizeVideoUri(text, out System.Uri uri, out string error));
        Assert.Null(error);
        Assert.Equal(expected, uri.AbsoluteUri);
    }

    [Fact]
    public void RejectInvalidVideoUrlBeforeSubmitting()
    {
        Assert.False(SpeedrunComSubmissionOptions.TryNormalizeVideoUri("not a valid url", out System.Uri uri, out string error));
        Assert.Null(uri);
        Assert.Equal("You didn't provide a valid Video URL.", error);
    }

    [Fact]
    public void AcceptEmptyVideoUrlAsNoVideo()
    {
        Assert.True(SpeedrunComSubmissionOptions.TryNormalizeVideoUri("", out System.Uri uri, out string error));
        Assert.Null(uri);
        Assert.Null(error);
    }

    [Fact]
    public void PatchMissingGameTimeAndAttemptHistoryFromSubmissionOptions()
    {
        IRun run = new StandardRunFactory().Create(new StandardComparisonGeneratorsFactory());
        Time runTime = new(realTime: new System.TimeSpan(0, 1, 20));
        run[0].PersonalBestSplitTime = runTime;
        run.AttemptHistory.Add(new Attempt(
            index: 1,
            started: null,
            ended: null,
            time: runTime,
            pauseTime: null));

        SpeedrunComSubmissionOptions.PatchGameTime(run, new System.TimeSpan(0, 1, 15));

        Assert.Equal(new System.TimeSpan(0, 1, 15), run[0].PersonalBestSplitTime.GameTime);
        Assert.Equal(new System.TimeSpan(0, 1, 15), Assert.Single(run.AttemptHistory).Time.GameTime);
    }

    [Fact]
    public void ParseOptionalSubmissionTimeWithoutPatchingRun()
    {
        Assert.True(SpeedrunComSubmissionOptions.TryParseOptionalTime(
            "1:15",
            out System.TimeSpan? time,
            out string error));

        Assert.Equal(new System.TimeSpan(0, 1, 15), time);
        Assert.Null(error);
    }

    [Fact]
    public void RejectInvalidOptionalSubmissionTime()
    {
        Assert.False(SpeedrunComSubmissionOptions.TryParseOptionalTime(
            "not time",
            out System.TimeSpan? time,
            out string error));

        Assert.Null(time);
        Assert.Equal("You didn't enter a valid time.", error);
    }

    [Fact]
    public void FindMissingMandatorySpeedrunComVariable()
    {
        Variable variable = CreateVariable(name: "Character", isMandatory: true);

        string missingVariableName = SpeedrunCom.FindMissingMandatoryVariableName(
            new Dictionary<Variable, VariableValue>
            {
                [variable] = null
            });

        Assert.Equal("Character", missingVariableName);
    }

    private static Variable CreateVariable(string name, bool isMandatory)
    {
        var variable = (Variable)System.Activator.CreateInstance(typeof(Variable), nonPublic: true);
        typeof(Variable).GetProperty(nameof(Variable.ID))?.SetValue(variable, name);
        typeof(Variable).GetProperty(nameof(Variable.Name))?.SetValue(variable, name);
        typeof(Variable).GetProperty(nameof(Variable.IsMandatory))?.SetValue(variable, isMandatory);
        return variable;
    }
}
