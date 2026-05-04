using System;
using System.IO;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class AboutBoxMust
{
    [Fact]
    public void KeepMasterVisibleContentAndLinkTargets()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/AboutBox.cs"));

        Assert.Contains("Unknown Version", source, StringComparison.Ordinal);
        Assert.Contains("Made by:", source, StringComparison.Ordinal);
        Assert.Contains("CryZe", source, StringComparison.Ordinal);
        Assert.Contains("wooferzfg", source, StringComparison.Ordinal);
        Assert.Contains("If you like the program, please consider donating.", source, StringComparison.Ordinal);
        Assert.Contains("http://livesplit.org", source, StringComparison.Ordinal);
        Assert.Contains("http://twitter.com/CryZe107", source, StringComparison.Ordinal);
        Assert.Contains("http://twitter.com/wooferzfg", source, StringComparison.Ordinal);
        Assert.Contains("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=R3Z2LGPKRNBNJ", source, StringComparison.Ordinal);
        Assert.Contains("Git.RevisionUri", source, StringComparison.Ordinal);
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
