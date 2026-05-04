using System;
using System.IO;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class SmallDialogParityMust
{
    [Fact]
    public void TextInputDialogUsesMasterButtonOrderAndCompactSize()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/TextInputDialog.cs"));

        Assert.Contains("Width = 396", source, StringComparison.Ordinal);
        Assert.Contains("Height = 150", source, StringComparison.Ordinal);
        Assert.Contains("Children = { ok, cancel }", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Children = { cancel, ok }", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EditHistoryDialogUsesMasterButtonOrderSizeAndLabels()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/EditHistoryDialog.cs"));

        Assert.Contains("Width = 390", source, StringComparison.Ordinal);
        Assert.Contains("Height = 270", source, StringComparison.Ordinal);
        Assert.Contains("MinWidth = 300", source, StringComparison.Ordinal);
        Assert.Contains("Content = \"Remove Selected\"", source, StringComparison.Ordinal);
        Assert.Contains("Children = { ok, cancel }", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Children = { cancel, ok }", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthenticationDialogUsesMasterGridStructureAndButtonOrder()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/AuthenticationDialog.cs"));

        Assert.Contains("Width = 270", source, StringComparison.Ordinal);
        Assert.Contains("Height = 180", source, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions = new ColumnDefinitions(\"74,*,81\")", source, StringComparison.Ordinal);
        Assert.Contains("Children = { ok, cancel }", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Children = { cancel, ok }", source, StringComparison.Ordinal);
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
