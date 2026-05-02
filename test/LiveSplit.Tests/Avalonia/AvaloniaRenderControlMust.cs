using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

using global::Avalonia;

using LiveSplit.Avalonia;
using LiveSplit.Options;
using LiveSplit.UI.Drawing;
using LiveSplit.UI.Drawing.Skia;

using SkiaSharp;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

[Collection("DrawingApi")]
public class AvaloniaRenderControlMust
{
    [Fact]
    public void UpdateTimerComponentsBeforeSnapshotRendering()
    {
        DrawingApi.Register(new SkiaDrawingFactory());
        EnsureComponentFolder();
        string settingsBackup = BackupSettingsFile();

        try
        {
            using var host = new AvaloniaTimerHost(
                static () => { },
                startBackgroundServices: false,
                persistOnDispose: false);
            var canvas = new SkiaRenderControl { Host = host };
            canvas.Measure(new Size(252, 50));
            canvas.Arrange(new Rect(0, 0, 252, 50));

            var timer = Assert.IsType<LiveSplit.UI.Components.Timer>(host.Renderer.VisibleComponents.Single());
            Assert.NotEqual(host.State.LayoutSettings.NotRunningColor.ToArgb(), timer.TimerColor.ToArgb());

            byte[] png = canvas.SnapshotPng();

            Assert.NotNull(png);
            Assert.Equal(host.State.LayoutSettings.NotRunningColor.ToArgb(), timer.TimerColor.ToArgb());
            Assert.Equal("0", timer.BigTextLabel.Text);
            Assert.Equal(".00", timer.SmallTextLabel.Text);
            Assert.True(HasAntialiasedTextEdgePixel(png));
        }
        finally
        {
            RestoreSettingsFile(settingsBackup);
        }
    }

    private static bool HasAntialiasedTextEdgePixel(byte[] png)
    {
        using SKBitmap bitmap = SKBitmap.Decode(png);
        Assert.NotNull(bitmap);

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                byte alpha = bitmap.GetPixel(x, y).Alpha;
                if (alpha > 0 && alpha != 128 && alpha < 255)
                {
                    return true;
                }
            }
        }

        return false;
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
}
