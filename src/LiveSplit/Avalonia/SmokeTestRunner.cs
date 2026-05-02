using System;
using System.Linq;

using LiveSplit.UI;
using LiveSplit.UI.Drawing;
using LiveSplit.UI.Drawing.Skia;

using SkiaSharp;

namespace LiveSplit.Avalonia;

public sealed class SmokeTestOptions
{
    public string SplitsPath { get; set; }
    public string LayoutPath { get; set; }
    public int Width { get; set; } = 320;
    public int Height { get; set; } = 120;
    public bool StartBackgroundServices { get; set; }
    public bool PersistOnDispose { get; set; }
}

public static class SmokeTestRunner
{
    public static int Run(SmokeTestOptions options)
    {
        options ??= new SmokeTestOptions();

        try
        {
            DrawingApi.Register(new SkiaDrawingFactory());

            using var host = new AvaloniaTimerHost(
                static () => { },
                options.SplitsPath,
                options.LayoutPath,
                options.StartBackgroundServices,
                options.PersistOnDispose);

            if (host.State?.Run is null)
            {
                throw new InvalidOperationException("Smoke test failed to initialize a run.");
            }

            if (host.State.Layout?.LayoutComponents is null
                || !host.State.Layout.LayoutComponents.Any(component => component?.Component is not null))
            {
                throw new InvalidOperationException("Smoke test failed to initialize layout components.");
            }

            RenderOnce(host, options.Width, options.Height);
            return 0;
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    private static void RenderOnce(AvaloniaTimerHost host, int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        host.Renderer.CalculateOverallSize(host.State.Layout.Mode);

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        if (surface is null)
        {
            throw new InvalidOperationException("Smoke test failed to create a Skia surface.");
        }

        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var ctx = new SkiaDrawingContext(canvas);
        LayoutMode mode = host.State.Layout.Mode;

        float overallSize = host.Renderer.OverallSize;
        float scale = mode == LayoutMode.Vertical ? height / overallSize : width / overallSize;
        if (scale > 0 && !float.IsInfinity(scale) && !float.IsNaN(scale))
        {
            ctx.ScaleTransform(scale, scale);
        }

        float drawWidth = mode == LayoutMode.Vertical ? width / scale : overallSize;
        float drawHeight = mode == LayoutMode.Vertical ? overallSize : height / scale;
        host.Renderer.Render(ctx, host.State, drawWidth, drawHeight, mode);

        if (!host.Renderer.VisibleComponents.Any())
        {
            throw new InvalidOperationException("Smoke test rendered no visible components.");
        }

        using SKImage image = surface.Snapshot();
        if (!HasNonTransparentPixel(image))
        {
            throw new InvalidOperationException("Smoke test rendered a blank frame.");
        }

        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        if (data is null || data.Size == 0)
        {
            throw new InvalidOperationException("Smoke test failed to encode the rendered frame.");
        }
    }

    private static bool HasNonTransparentPixel(SKImage image)
    {
        using SKBitmap bitmap = SKBitmap.FromImage(image);
        if (bitmap is null)
        {
            return false;
        }

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
