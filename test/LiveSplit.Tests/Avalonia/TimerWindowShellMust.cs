using System;
using System.IO;

using LiveSplit.Avalonia;

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
