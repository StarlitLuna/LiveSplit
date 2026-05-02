using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

using LiveSplit.Avalonia;
using LiveSplit.Options;
using LiveSplit.UI.Drawing;
using LiveSplit.UI.Drawing.Skia;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

[Collection("DrawingApi")]
public class AvaloniaTimerHostPersistenceMust
{
    [Fact]
    public void SaveRunAsAssignsFilePathAndClearsDirtyFlag()
    {
        DrawingApi.Register(new SkiaDrawingFactory());
        EnsureComponentFolder();
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.lss");
        string settingsBackup = BackupSettingsFile();

        try
        {
            using var host = new AvaloniaTimerHost(
                static () => { },
                startBackgroundServices: false,
                persistOnDispose: false);
            host.State.Run.HasChanged = true;

            Assert.True(host.SaveRunAs(path));

            Assert.Equal(path, host.State.Run.FilePath);
            Assert.False(host.State.Run.HasChanged);
            Assert.True(File.Exists(path));
        }
        finally
        {
            RestoreSettingsFile(settingsBackup);
            TryDelete(path);
        }
    }

    [Fact]
    public void SaveLayoutAsAssignsFilePathPositionAndClearsDirtyFlag()
    {
        DrawingApi.Register(new SkiaDrawingFactory());
        EnsureComponentFolder();
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.lsl");
        string settingsBackup = BackupSettingsFile();

        try
        {
            using var host = new AvaloniaTimerHost(
                static () => { },
                startBackgroundServices: false,
                persistOnDispose: false);
            host.State.Layout.HasChanged = true;

            Assert.True(host.SaveLayoutAs(path, 12, 34));

            Assert.Equal(path, host.State.Layout.FilePath);
            Assert.Equal(12, host.State.Layout.X);
            Assert.Equal(34, host.State.Layout.Y);
            Assert.False(host.State.Layout.HasChanged);
            Assert.True(File.Exists(path));
        }
        finally
        {
            RestoreSettingsFile(settingsBackup);
            TryDelete(path);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void EnsureComponentFolder()
    {
        string baseDir = AppContext.BaseDirectory;
        string componentsDir = Path.Combine(baseDir, "Components");
        Directory.CreateDirectory(componentsDir);

        foreach (string dll in Directory.EnumerateFiles(baseDir, "LiveSplit.*.dll")
            .Where(path =>
            {
                string name = Path.GetFileName(path);
                return name != "LiveSplit.dll"
                    && name != "LiveSplit.Core.dll"
                    && name != "LiveSplit.Tests.dll";
            }))
        {
            string destination = Path.Combine(componentsDir, Path.GetFileName(dll));
            CopyComponentDll(dll, destination);
        }
    }

    private static void CopyComponentDll(string source, string destination)
    {
        try
        {
            File.Copy(source, destination, overwrite: true);
        }
        catch (IOException) when (File.Exists(destination) && AreFilesIdentical(source, destination))
        {
        }
        catch (UnauthorizedAccessException) when (File.Exists(destination) && AreFilesIdentical(source, destination))
        {
        }
    }

    private static bool AreFilesIdentical(string first, string second)
    {
        byte[] firstHash = SHA256.HashData(File.ReadAllBytes(first));
        byte[] secondHash = SHA256.HashData(File.ReadAllBytes(second));
        return firstHash.SequenceEqual(secondHash);
    }

    private static string BackupSettingsFile()
    {
        string path = UserDataPaths.SettingsFile;
        if (!File.Exists(path))
        {
            return null;
        }

        string contents = File.ReadAllText(path);
        File.Delete(path);
        return contents;
    }

    private static void RestoreSettingsFile(string backup)
    {
        string path = UserDataPaths.SettingsFile;
        if (backup is null)
        {
            TryDelete(path);
            return;
        }

        File.WriteAllText(path, backup);
    }
}
