using System;
using System.Drawing.Drawing2D;
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

using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingSolidBrush = System.Drawing.SolidBrush;

namespace LiveSplit.Tests.Avalonia;

[Collection("DrawingApi")]
public class AvaloniaRenderControlMust
{
    [Fact]
    public void PaintLayoutBackgroundToWindowEdges()
    {
        DrawingApi.Register(new SkiaDrawingFactory());
        LayoutSettings settings = new()
        {
            BackgroundType = BackgroundType.SolidColor,
            BackgroundColor = System.Drawing.Color.FromArgb(255, 15, 15, 15),
        };

        using SKSurface surface = SKSurface.Create(new SKImageInfo(252, 50, SKColorType.Bgra8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Black);
        var ctx = new SkiaDrawingContext(surface.Canvas);

        SkiaRenderControl.DrawLayoutBackground(ctx, settings, 252, 50);

        using SKImage image = surface.Snapshot();
        using SKBitmap bitmap = SKBitmap.FromImage(image);

        Assert.Equal(new SKColor(15, 15, 15), bitmap.GetPixel(251, 49));
        Assert.Equal(new SKColor(15, 15, 15), bitmap.GetPixel(249, 48));
    }

    [Fact]
    public void CalculateImageBackgroundSourceRectAsMasterCoverCrop()
    {
        System.Drawing.Rectangle source = SkiaRenderControl.CalculateCoverSourceRect(
            imageWidth: 400,
            imageHeight: 200,
            targetWidth: 100,
            targetHeight: 100);

        Assert.Equal(new System.Drawing.Rectangle(100, 0, 200, 200), source);
    }

    [Fact]
    public void CalculateImageBackgroundSourceRectPreservesMasterFractionalCrop()
    {
        System.Drawing.RectangleF source = SkiaRenderControl.CalculateCoverSourceRectF(
            imageWidth: 403,
            imageHeight: 200,
            targetWidth: 101,
            targetHeight: 100);

        Assert.Equal(new System.Drawing.RectangleF(100.5f, 0f, 202f, 200f), source);
    }

    [Fact]
    public void ImageBackgroundUsesMasterHighQualitySamplingBlurAndOpacity()
    {
        DrawingApi.Register(new SkiaDrawingFactory());
        var settings = new LayoutSettings
        {
            BackgroundType = BackgroundType.Image,
            BackgroundImage = CreateEncodedImage(),
            ImageOpacity = 0.65f,
            ImageBlur = 2.5f,
        };
        var ctx = new RecordingDrawingContext
        {
            InterpolationMode = InterpolationMode.Bilinear,
        };

        SkiaRenderControl.DrawLayoutBackground(ctx, settings, 80, 40);

        Assert.Equal(InterpolationMode.HighQualityBicubic, ctx.ImageInterpolationMode);
        Assert.Equal(InterpolationMode.Bilinear, ctx.InterpolationMode);
        Assert.Equal(0.65f, ctx.ImageOpacity);
        Assert.Equal(25f, ctx.ImageBlurSigma);
        Assert.Equal(new System.Drawing.RectangleF(0f, 0f, 80f, 40f), ctx.ImageDestination);
        Assert.Equal(new System.Drawing.RectangleF(0f, 25f, 100f, 50f), ctx.ImageSource);
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
            int maxVisibleLuminance = MaxVisibleLuminance(png);
            Assert.True(
                maxVisibleLuminance > host.State.LayoutSettings.NotRunningColor.R + 40,
                $"Expected timer gradient to be visibly brighter than the base not-running color, actual max luminance was {maxVisibleLuminance}.");
            AssertPixelColor(png, 0, 0, host.State.LayoutSettings.BackgroundColor);
        }
        finally
        {
            RestoreSettingsFile(settingsBackup);
        }
    }

    [Fact]
    public void DefaultLayoutSnapshotRendersComponentsBelowTitle()
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
            host.LoadDefaultLayout();
            var canvas = new SkiaRenderControl { Host = host };
            (int windowWidth, int windowHeight) = TimerWindow.GetWindowSizeForLayout(
                host.State.Layout.VerticalWidth,
                host.State.Layout.VerticalHeight);
            canvas.Measure(new Size(windowWidth, windowHeight));
            canvas.Arrange(new Rect(0, 0, windowWidth, windowHeight));

            byte[] png = canvas.SnapshotPng();

            Assert.NotNull(png);
            AssertSnapshotSize(png, host.State.Layout.VerticalWidth, host.State.Layout.VerticalHeight);
            Assert.True(
                HasBrightPixelInBand(png, host.State.Layout.VerticalHeight - 140, host.State.Layout.VerticalHeight - 20, 180),
                "Expected the default layout timer/footer components to render below the title band.");
        }
        finally
        {
            RestoreSettingsFile(settingsBackup);
        }
    }

    [Fact]
    public void DefaultLayoutSnapshotRendersUpperSplitSeparators()
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
            host.LoadDefaultLayout();
            var canvas = new SkiaRenderControl { Host = host };
            (int windowWidth, int windowHeight) = TimerWindow.GetWindowSizeForLayout(
                host.State.Layout.VerticalWidth,
                host.State.Layout.VerticalHeight);
            canvas.Measure(new Size(windowWidth, windowHeight));
            canvas.Arrange(new Rect(0, 0, windowWidth, windowHeight));

            byte[] png = canvas.SnapshotPng();

            Assert.NotNull(png);
            using SKBitmap bitmap = SKBitmap.Decode(png);
            int[] rows = FindDimHorizontalRows(bitmap, 45, host.State.Layout.VerticalHeight / 2);

            Assert.True(
                rows.Length >= 6,
                $"Expected the upper default-layout split separators to be visible; found rows: {string.Join(", ", rows)}.");
        }
        finally
        {
            RestoreSettingsFile(settingsBackup);
        }
    }

    [Fact]
    public void GammaCorrectedSolidFillMatchesGdiSeparatorCompositingOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        System.Drawing.Color background = System.Drawing.Color.FromArgb(255, 15, 15, 15);
        System.Drawing.Color separator = System.Drawing.Color.FromArgb(3, 255, 255, 255);
        byte expected = RenderGdiComposite(background, separator);

        DrawingApi.Register(new SkiaDrawingFactory());
        using SKSurface surface = SkiaRenderControl.CreateFrameSurface(4, 4);
        var ctx = new SkiaDrawingContext(surface);
        SkiaRenderControl.ApplyMasterRenderSettings(ctx, new LayoutSettings { AntiAliasing = true });
        using (ISolidBrush brush = DrawingApi.Factory.CreateSolidBrush(background))
        {
            ctx.FillRectangle(brush, 0f, 0f, 4f, 4f);
        }

        using (ISolidBrush brush = DrawingApi.Factory.CreateSolidBrush(separator))
        {
            ctx.FillRectangle(brush, 0f, 0f, 4f, 1f);
        }

        using SKImage image = surface.Snapshot();
        using SKBitmap bitmap = SKBitmap.FromImage(image);
        SKColor actual = bitmap.GetPixel(0, 0);

        Assert.InRange(actual.Red, expected - 1, expected + 1);
        Assert.Equal(actual.Red, actual.Green);
        Assert.Equal(actual.Red, actual.Blue);
    }

    [Fact]
    public void OpaqueTitleGradientStaysCloseToGdiRowsOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        byte[] expected = RenderGdiGradientRows(
            System.Drawing.Color.FromArgb(255, 42, 42, 42),
            System.Drawing.Color.FromArgb(255, 19, 19, 19),
            39);

        DrawingApi.Register(new SkiaDrawingFactory());
        using SKSurface surface = SkiaRenderControl.CreateFrameSurface(4, expected.Length);
        var ctx = new SkiaDrawingContext(surface);
        SkiaRenderControl.ApplyMasterRenderSettings(ctx, new LayoutSettings { AntiAliasing = true });
        using (ILinearGradientBrush brush = DrawingApi.Factory.CreateLinearGradientBrush(
            new System.Drawing.PointF(0f, 0f),
            new System.Drawing.PointF(0f, expected.Length),
            System.Drawing.Color.FromArgb(255, 42, 42, 42),
            System.Drawing.Color.FromArgb(255, 19, 19, 19)))
        {
            ctx.FillRectangle(brush, 0f, 0f, 4f, expected.Length);
        }

        using SKImage image = surface.Snapshot();
        using SKBitmap bitmap = SKBitmap.FromImage(image);

        int maxDifference = 0;
        int maxPlateau = 0;
        int currentPlateau = 0;
        byte previous = bitmap.GetPixel(0, 0).Red;
        for (int y = 0; y < expected.Length; y++)
        {
            byte actual = bitmap.GetPixel(0, y).Red;
            maxDifference = Math.Max(maxDifference, Math.Abs(actual - expected[y]));
            if (y > 0 && actual == previous)
            {
                currentPlateau++;
                maxPlateau = Math.Max(maxPlateau, currentPlateau);
            }
            else
            {
                currentPlateau = 0;
            }

            previous = actual;
        }

        Assert.InRange(maxDifference, 0, 2);
        Assert.InRange(maxPlateau, 0, 2);
    }

    [Fact]
    public void FractionalGradientRectangleTopEdgeMatchesGdiOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const int bitmapHeight = 519;
        const float scale = 0.84641f;
        const float top = 582.1781f;
        const float height = 31f;
        System.Drawing.Color background = System.Drawing.Color.FromArgb(255, 15, 15, 15);
        System.Drawing.Color start = System.Drawing.Color.FromArgb(255, 28, 28, 28);
        System.Drawing.Color end = System.Drawing.Color.FromArgb(255, 13, 13, 13);
        byte[] expected = RenderGdiFractionalGradientRows(background, start, end, top, height, scale, bitmapHeight);

        DrawingApi.Register(new SkiaDrawingFactory());
        using SKSurface surface = SkiaRenderControl.CreateFrameSurface(4, expected.Length);
        var ctx = new SkiaDrawingContext(surface);
        SkiaRenderControl.ApplyMasterRenderSettings(ctx, new LayoutSettings { AntiAliasing = true });
        using (ISolidBrush brush = DrawingApi.Factory.CreateSolidBrush(background))
        {
            ctx.FillRectangle(brush, 0f, 0f, 4f, expected.Length);
        }

        ctx.ScaleTransform(scale, scale);
        ctx.TranslateTransform(-0.5f, -0.5f);
        using (ILinearGradientBrush brush = DrawingApi.Factory.CreateLinearGradientBrush(
            new System.Drawing.PointF(0f, top),
            new System.Drawing.PointF(0f, top + height),
            start,
            end))
        {
            ctx.FillRectangle(brush, 0f, top, 4f / scale, height);
        }

        using SKImage image = surface.Snapshot();
        using SKBitmap bitmap = SKBitmap.FromImage(image);

        Assert.Equal(background.R, bitmap.GetPixel(1, 492).Red);

        for (int y = 490; y < expected.Length; y++)
        {
            byte actual = bitmap.GetPixel(1, y).Red;
            Assert.True(
                actual >= expected[y] - 2 && actual <= expected[y] + 2,
                $"Expected row {y} to match GDI red {expected[y]}, actual {actual}.");
        }
    }

    [Fact]
    public void MasterHalfPixelOffsetIsAppliedBeforeScaling()
    {
        const float scale = 2f;
        System.Numerics.Matrix3x2 transform = SkiaRenderControl.GetMasterLayoutTransform(scale);

        Assert.Equal(scale, transform.M11);
        Assert.Equal(scale, transform.M22);
        Assert.Equal(-0.5f, transform.M31);
        Assert.Equal(-0.5f, transform.M32);
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

    private static bool HasBrightPixelInBand(byte[] png, int startY, int endY, int threshold)
    {
        using SKBitmap bitmap = SKBitmap.Decode(png);
        Assert.NotNull(bitmap);

        startY = Math.Max(0, startY);
        endY = Math.Min(bitmap.Height, endY);
        for (int y = startY; y < endY; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                SKColor pixel = bitmap.GetPixel(x, y);
                if (Math.Max(pixel.Red, Math.Max(pixel.Green, pixel.Blue)) >= threshold)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int[] FindDimHorizontalRows(SKBitmap bitmap, int startY, int endY)
    {
        var rows = new System.Collections.Generic.List<int>();
        startY = Math.Max(0, startY);
        endY = Math.Min(bitmap.Height, endY);

        for (int y = startY; y < endY; y++)
        {
            int count = 0;
            for (int x = 0; x < bitmap.Width; x++)
            {
                SKColor pixel = bitmap.GetPixel(x, y);
                int luminance = Math.Max(pixel.Red, Math.Max(pixel.Green, pixel.Blue));
                if (pixel.Alpha > 240 && luminance >= 25 && luminance <= 55)
                {
                    count++;
                }
            }

            if (count >= bitmap.Width * 0.8)
            {
                rows.Add(y);
            }
        }

        return rows.ToArray();
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

    private static byte[] CreateEncodedImage()
    {
        using var bitmap = new SKBitmap(100, 100, SKColorType.Bgra8888, SKAlphaType.Premul);
        bitmap.Erase(SKColors.Red);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte RenderGdiComposite(System.Drawing.Color background, System.Drawing.Color foreground)
    {
        using var bitmap = new DrawingBitmap(4, 4, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using DrawingGraphics graphics = DrawingGraphics.FromImage(bitmap);
        graphics.CompositingQuality = CompositingQuality.GammaCorrected;
        using var backgroundBrush = new DrawingSolidBrush(background);
        using var foregroundBrush = new DrawingSolidBrush(foreground);
        graphics.FillRectangle(backgroundBrush, 0, 0, 4, 4);
        graphics.FillRectangle(foregroundBrush, 0, 0, 4, 1);
        return bitmap.GetPixel(0, 0).R;
    }

    private static byte[] RenderGdiGradientRows(System.Drawing.Color start, System.Drawing.Color end, int height)
    {
        using var bitmap = new DrawingBitmap(4, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using DrawingGraphics graphics = DrawingGraphics.FromImage(bitmap);
        graphics.CompositingQuality = CompositingQuality.GammaCorrected;
        using var brush = new LinearGradientBrush(
            new System.Drawing.PointF(0f, 0f),
            new System.Drawing.PointF(0f, height),
            start,
            end);
        graphics.FillRectangle(brush, 0, 0, 4, height);

        byte[] rows = new byte[height];
        for (int y = 0; y < rows.Length; y++)
        {
            rows[y] = bitmap.GetPixel(0, y).R;
        }

        return rows;
    }

    private static byte[] RenderGdiFractionalGradientRows(
        System.Drawing.Color background,
        System.Drawing.Color start,
        System.Drawing.Color end,
        float top,
        float height,
        float scale,
        int bitmapHeight)
    {
        using var bitmap = new DrawingBitmap(4, bitmapHeight, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using DrawingGraphics graphics = DrawingGraphics.FromImage(bitmap);
        graphics.CompositingQuality = CompositingQuality.GammaCorrected;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var backgroundBrush = new DrawingSolidBrush(background);
        graphics.FillRectangle(backgroundBrush, 0, 0, bitmap.Width, bitmap.Height);
        graphics.ScaleTransform(scale, scale);
        graphics.TranslateTransform(-0.5f, -0.5f);
        using var brush = new LinearGradientBrush(
            new System.Drawing.PointF(0f, top),
            new System.Drawing.PointF(0f, top + height),
            start,
            end);
        graphics.FillRectangle(brush, 0f, top, bitmap.Width / scale, height);

        byte[] rows = new byte[bitmap.Height];
        for (int y = 0; y < rows.Length; y++)
        {
            rows[y] = bitmap.GetPixel(1, y).R;
        }

        return rows;
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

    private sealed class RecordingDrawingContext : IDrawingContext
    {
        public InterpolationMode ImageInterpolationMode { get; private set; }
        public float ImageOpacity { get; private set; }
        public float ImageBlurSigma { get; private set; }
        public System.Drawing.RectangleF ImageDestination { get; private set; }
        public System.Drawing.RectangleF ImageSource { get; private set; }

        public SmoothingMode SmoothingMode { get; set; }
        public System.Drawing.Text.TextRenderingHint TextRenderingHint { get; set; }
        public InterpolationMode InterpolationMode { get; set; }
        public CompositingQuality CompositingQuality { get; set; }
        public CompositingMode CompositingMode { get; set; }
        public PixelOffsetMode PixelOffsetMode { get; set; }
        public float DpiX => 96f;
        public float DpiY => 96f;

        public void DrawImageWithOpacity(IImage image, System.Drawing.RectangleF destRect, System.Drawing.RectangleF srcRect, float opacity, float blurSigma = 0)
        {
            ImageInterpolationMode = InterpolationMode;
            ImageDestination = destRect;
            ImageSource = srcRect;
            ImageOpacity = opacity;
            ImageBlurSigma = blurSigma;
        }

        public void DrawImageWithOpacity(IImage image, System.Drawing.Rectangle destRect, System.Drawing.Rectangle srcRect, float opacity, float blurSigma = 0)
            => DrawImageWithOpacity(
                image,
                new System.Drawing.RectangleF(destRect.X, destRect.Y, destRect.Width, destRect.Height),
                new System.Drawing.RectangleF(srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height),
                opacity,
                blurSigma);

        public void FillRectangle(IBrush brush, System.Drawing.RectangleF rect) { }
        public void FillRectangle(IBrush brush, float x, float y, float width, float height) { }
        public void DrawRectangle(IPen pen, System.Drawing.RectangleF rect) { }
        public void DrawLine(IPen pen, System.Drawing.PointF p1, System.Drawing.PointF p2) { }
        public void FillPolygon(IBrush brush, System.Drawing.PointF[] points) { }
        public void FillEllipse(IBrush brush, float x, float y, float width, float height) { }
        public void DrawImage(IImage image, System.Drawing.RectangleF destRect) { }
        public void DrawImage(IImage image, System.Drawing.Rectangle destRect, System.Drawing.Rectangle srcRect) { }
        public void DrawString(string text, IFont font, IBrush brush, System.Drawing.RectangleF bounds, ITextFormat format) { }
        public System.Drawing.SizeF MeasureString(string text, IFont font, int maxWidth, ITextFormat format) => System.Drawing.SizeF.Empty;
        public void FillPath(IBrush brush, IGraphicsPath path) { }
        public void DrawPath(IPen pen, IGraphicsPath path) { }
        public void TranslateTransform(float dx, float dy) { }
        public void ScaleTransform(float sx, float sy) { }
        public void ResetTransform() { }
        public System.Numerics.Matrix3x2 GetTransform() => System.Numerics.Matrix3x2.Identity;
        public void ClearClip() { }
        public void SetClip(System.Drawing.RectangleF rect) { }
        public void IntersectClip(System.Drawing.RectangleF rect) { }
        public bool IsVisible(System.Drawing.RectangleF rect) => true;
        public IDrawingState Save() => new RecordingState();

        private sealed class RecordingState : IDrawingState
        {
            public void Dispose() { }
        }
    }
}
