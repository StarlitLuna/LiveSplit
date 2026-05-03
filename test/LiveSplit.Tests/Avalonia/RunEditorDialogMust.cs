using System;
using System.IO;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class RunEditorDialogMust
{
    [Fact]
    public void BuildIndependentCustomComparisonBarsForTimingTabs()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/RunEditorDialog.cs"));

        Assert.DoesNotContain("stack.Children.Add(_customComparisonsPanel)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly StackPanel _customComparisonsPanel", source, StringComparison.Ordinal);
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
