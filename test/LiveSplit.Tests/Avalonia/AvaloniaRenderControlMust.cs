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
    public void PaintLayoutBackgroundThroughBorderlessCompensation()
    {
        DrawingApi.Register(new SkiaDrawingFactory());
        LayoutSettings settings = new()
        {
            BackgroundType = BackgroundType.SolidColor,
            BackgroundColor = System.Drawing.Color.FromArgb(255, 15, 15, 15),
        };

        using SKSurface surface = SKSurface.Create(new SKImageInfo(255, 51, SKColorType.Bgra8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Black);
        var ctx = new SkiaDrawingContext(surface.Canvas);

        SkiaRenderControl.DrawLayoutBackground(ctx, settings, 255, 51);

        using SKImage image = surface.Snapshot();
        using SKBitmap bitmap = SKBitmap.FromImage(image);

        Assert.Equal(new SKColor(15, 15, 15), bitmap.GetPixel(254, 50));
        Assert.Equal(new SKColor(15, 15, 15), bitmap.GetPixel(252, 49));
    }

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
            (int windowWidth, int windowHeight) = TimerWindow.GetWindowSizeForLayout(252, 50);
            canvas.Measure(new Size(windowWidth, windowHeight));
            canvas.Arrange(new Rect(0, 0, windowWidth, windowHeight));

            var timer = Assert.IsType<LiveSplit.UI.Components.Timer>(host.Renderer.VisibleComponents.Single());
            Assert.NotEqual(host.State.LayoutSettings.NotRunningColor.ToArgb(), timer.TimerColor.ToArgb());

            byte[] png = canvas.SnapshotPng();

            Assert.NotNull(png);
            AssertSnapshotSize(png, 252, 50);
            Assert.Equal(host.State.LayoutSettings.NotRunningColor.ToArgb(), timer.TimerColor.ToArgb());
            Assert.Equal("0", timer.BigTextLabel.Text);
            Assert.Equal(".00", timer.SmallTextLabel.Text);
            Assert.True(HasIntermediateTextPixel(png, host.State.LayoutSettings.BackgroundColor));
            Assert.False(HasColoredSubpixelFringe(png));
            Assert.InRange(MaxVisibleLuminance(png), 220, 255);
            AssertPixelColor(png, 0, 0, host.State.LayoutSettings.BackgroundColor);
        }
        finally
        {
            RestoreSettingsFile(settingsBackup);
        }
    }

    private static void AssertSnapshotSize(byte[] png, int width, int height)
    {
        using SKBitmap bitmap = SKBitmap.Decode(png);
        Assert.NotNull(bitmap);

        Assert.Equal(width, bitmap.Width);
        Assert.Equal(height, bitmap.Height);
    }

    private static void AssertPixelColor(byte[] png, int x, int y, System.Drawing.Color expected)
    {
        using SKBitmap bitmap = SKBitmap.Decode(png);
        Assert.NotNull(bitmap);

        SKColor pixel = bitmap.GetPixel(x, y);
        Assert.Equal(expected.A, pixel.Alpha);
        Assert.Equal(expected.R, pixel.Red);
        Assert.Equal(expected.G, pixel.Green);
        Assert.Equal(expected.B, pixel.Blue);
    }

    private static bool HasIntermediateTextPixel(byte[] png, System.Drawing.Color background)
    {
        using SKBitmap bitmap = SKBitmap.Decode(png);
        Assert.NotNull(bitmap);

        int backgroundLuminance = Math.Max(background.R, Math.Max(background.G, background.B));
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                SKColor pixel = bitmap.GetPixel(x, y);
                int luminance = Math.Max(pixel.Red, Math.Max(pixel.Green, pixel.Blue));
                if (luminance > backgroundLuminance + 3 && luminance < 220)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int MaxVisibleLuminance(byte[] png)
    {
        using SKBitmap bitmap = SKBitmap.Decode(png);
        Assert.NotNull(bitmap);

        int max = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                SKColor pixel = bitmap.GetPixel(x, y);
                if (pixel.Alpha == 0)
                {
                    continue;
                }

                max = Math.Max(max, Math.Max(pixel.Red, Math.Max(pixel.Green, pixel.Blue)));
            }
        }

        return max;
    }

    private static bool HasColoredSubpixelFringe(byte[] png)
    {
        using SKBitmap bitmap = SKBitmap.Decode(png);
        Assert.NotNull(bitmap);

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                SKColor pixel = bitmap.GetPixel(x, y);
                if (pixel.Alpha == 0)
                {
                    continue;
                }

                if (Math.Abs(pixel.Red - pixel.Green) > 1
                    || Math.Abs(pixel.Green - pixel.Blue) > 1
                    || Math.Abs(pixel.Red - pixel.Blue) > 1)
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
