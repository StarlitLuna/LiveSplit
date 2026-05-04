using System;
using System.IO;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class RaceProviderManagingDialogMust
{
    [Fact]
    public void UseMasterManageRacingServicesTitle()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/RaceProviderManagingDialog.cs"));

        Assert.Contains("Title = \"Manage Racing Services\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UseMasterStructureWithCheckedProviderListAndLinkRows()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/RaceProviderManagingDialog.cs"));

        Assert.Contains("Width = 450", source, StringComparison.Ordinal);
        Assert.Contains("MinWidth = 450", source, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions = new ColumnDefinitions(\"150,*\")", source, StringComparison.Ordinal);
        Assert.Contains("RowDefinitions = new RowDefinitions(\"40,*,32\")", source, StringComparison.Ordinal);
        Assert.Contains("Width = 144", source, StringComparison.Ordinal);
        Assert.Contains("DialogLabel(\"Website:\")", source, StringComparison.Ordinal);
        Assert.Contains("DialogLabel(\"Rules:\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Content = \"Website\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Content = \"Rules\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Content = \"Enabled\"", source, StringComparison.Ordinal);
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
