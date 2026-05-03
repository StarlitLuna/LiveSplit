using System;
using System.IO;

using global::Avalonia;

using LiveSplit.Avalonia;
using LiveSplit.Model;
using LiveSplit.Options;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class TimerWindowShellMust
{
    [Fact]
    public void MatchMasterTimerWindowShellDefaults()
    {
        string xaml = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/TimerWindow.axaml"));

        Assert.Contains("SystemDecorations=\"None\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"Black\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CompensateAvaloniaBorderlessBoundsToMatchMasterFormSize()
    {
        (int windowWidth, int windowHeight) = TimerWindow.GetWindowSizeForLayout(252, 50);

        Assert.Equal(255, windowWidth);
        Assert.Equal(51, windowHeight);

        (int layoutWidth, int layoutHeight) = TimerWindow.GetLayoutSizeForWindow(255, 51);

        Assert.Equal(252, layoutWidth);
        Assert.Equal(50, layoutHeight);
    }

    [Fact]
    public void EnableMousePassThroughOnlyWhileRunningAndNotForeground()
    {
        var settings = new LayoutSettings { MousePassThroughWhileRunning = true };

        Assert.False(TimerWindow.ShouldUseMousePassThrough(settings, TimerPhase.Running, isForeground: true));
        Assert.True(TimerWindow.ShouldUseMousePassThrough(settings, TimerPhase.Running, isForeground: false));
        Assert.False(TimerWindow.ShouldUseMousePassThrough(settings, TimerPhase.NotRunning, isForeground: false));

        settings.MousePassThroughWhileRunning = false;
        Assert.False(TimerWindow.ShouldUseMousePassThrough(settings, TimerPhase.Running, isForeground: false));
    }

    [Fact]
    public void ClampSavedLayoutPositionInsideAvailableScreenBounds()
    {
        var screen = new PixelRect(0, 0, 500, 400);

        Assert.Equal(
            new PixelPoint(0, 0),
            TimerWindow.ClampLayoutPosition(new PixelPoint(-20, -10), new Size(100, 100), [screen]));
        Assert.Equal(
            new PixelPoint(400, 300),
            TimerWindow.ClampLayoutPosition(new PixelPoint(900, 800), new Size(100, 100), [screen]));
    }

    [Fact]
    public void PreservePreviousServerStateWhenRestartingPreviousStateServerSettings()
    {
        Assert.Equal(
            ServerStateType.TCP,
            TimerWindow.ResolveServerStateForSettingsRestart(
                ServerStartupType.PreviousState,
                runningServerState: ServerStateType.TCP,
                savedServerState: ServerStateType.Off));
    }

    private static string FindRepoFile(string relativePath)
    {
        DirectoryInfo directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }
}
