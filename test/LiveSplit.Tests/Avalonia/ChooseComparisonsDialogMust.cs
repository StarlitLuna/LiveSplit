using System;
using System.IO;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class ChooseComparisonsDialogMust
{
    [Fact]
    public void UseMasterGolfHcpBoundsAndLabel()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/ChooseComparisonsDialog.cs"));

        Assert.Contains("Maximum = 50", source, StringComparison.Ordinal);
        Assert.Contains("Golf HCP Settings", source, StringComparison.Ordinal);
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
