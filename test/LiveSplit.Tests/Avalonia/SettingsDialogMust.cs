using System;
using System.IO;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class SettingsDialogMust
{
    [Fact]
    public void ExposeMasterSettingsSurfaceInOrder()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/SettingsDialog.cs"));

        Assert.DoesNotContain("new TabControl", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Comparisons\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Racing\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"General\"", source, StringComparison.Ordinal);

        foreach (string token in new string[]
        {
            "\"Hotkeys\"",
            "\"Start / Split:\"",
            "\"Reset:\"",
            "\"Undo Split:\"",
            "\"Skip Split:\"",
            "\"Pause:\"",
            "\"Switch Comparison (Previous):\"",
            "\"Switch Comparison (Next):\"",
            "\"Toggle Global Hotkeys:\"",
            "\"Global Hotkeys\"",
            "\"Deactivate For Other Programs\"",
            "\"Double Tap Prevention\"",
            "\"Hotkey Delay (Seconds):\"",
            "\"Allow Gamepads as Hotkeys\"",
            "\"Hotkey Profiles\"",
            "\"Active Hotkey Profile:\"",
            "\"New\"",
            "\"Rename\"",
            "\"Remove\"",
            "\"Simple Sum of Best Calculation\"",
            "\"Warn On Reset If Better Times\"",
            "\"Race Viewer:\"",
            "\"Manage Racing Services...\"",
            "\"Active Comparisons:\"",
            "\"Choose Active Comparisons...\"",
            "\"Saved Accounts:\"",
            "\"Log Out of All Accounts\"",
            "\"LiveSplit Server\"",
            "\"Server Port:\"",
            "\"Startup Behavior:\"",
            "\"Refresh Rate (Hz):\"",
        })
        {
            Assert.Contains(token, source, StringComparison.Ordinal);
        }

        Assert.Contains("\"OK\"", source, StringComparison.Ordinal);
        Assert.Contains("\"Cancel\"", source, StringComparison.Ordinal);
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
