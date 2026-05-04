using System;
using System.IO;
using System.Linq;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class DialogThemeMust
{
    [Fact]
    public void BeAppliedByEveryAvaloniaDialogWindow()
    {
        string dialogDirectory = FindRepoDirectory("src/LiveSplit/Avalonia/Dialogs");
        string[] excluded =
        [
            "DialogTheme.cs",
        ];

        foreach (string path in Directory.EnumerateFiles(dialogDirectory, "*.cs")
            .Where(path => !excluded.Contains(Path.GetFileName(path), StringComparer.Ordinal)))
        {
            string source = File.ReadAllText(path);
            if (!source.Contains(": Window", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.Contains("DialogTheme.ApplyWindow(this);", source, StringComparison.Ordinal);
        }
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
