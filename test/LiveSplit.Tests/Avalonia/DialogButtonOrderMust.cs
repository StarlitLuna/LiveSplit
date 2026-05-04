using System;
using System.IO;
using System.Linq;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class DialogButtonOrderMust
{
    [Fact]
    public void KeepMasterOkBeforeCancelOrderInStaticDialogFooters()
    {
        string dialogDirectory = FindRepoDirectory("src/LiveSplit/Avalonia/Dialogs");
        string[] offenders = Directory.GetFiles(dialogDirectory, "*.cs")
            .Where(path => !Path.GetFileName(path).Equals("MessageDialog.cs", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("Children = { cancel, ok }", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static string FindRepoDirectory(string relativePath)
    {
        DirectoryInfo directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(relativePath);
    }
}
