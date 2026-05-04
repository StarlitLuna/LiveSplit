using System;
using System.IO;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class SetSizeFormMust
{
    [Fact]
    public void KeepOriginalSizeSnapshotForCancelRollback()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/SetSizeForm.cs"));

        Assert.Contains("_originalWidth", source, StringComparison.Ordinal);
        Assert.Contains("_originalHeight", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_oldWidth = newWidth;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_oldHeight = newHeight;", source, StringComparison.Ordinal);
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
