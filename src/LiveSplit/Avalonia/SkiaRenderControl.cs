using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Platform;
using global::Avalonia.Rendering.SceneGraph;
using global::Avalonia.Skia;

using LiveSplit.UI;
using LiveSplit.UI.Drawing;
using LiveSplit.UI.Drawing.Skia;
using LiveSplit.UI.Components;
using LiveSplit.Model;
using LiveSplit.Options;

using SkiaSharp;

namespace LiveSplit.Avalonia;

/// <summary>
/// Avalonia <see cref="Control"/> that runs <see cref="ComponentRenderer"/> through a
/// <see cref="SkiaDrawingContext"/>. We render into a raster frame first so the drawing context
/// can emulate master WinForms' gamma-corrected GDI+ alpha compositing for transparent
/// separators without changing opaque gradient interpolation. <see cref="AvaloniaTimerHost"/>
/// pumps <c>InvalidateVisual</c> at the configured refresh rate to drive the running clock.
/// </summary>
public sealed class SkiaRenderControl : Control
{
    public AvaloniaTimerHost Host { get; set; }

    /// <summary>
    /// Renders the current layout to a PNG-encoded byte buffer via an offscreen Skia surface.
    /// Returns null if the host or layout is not yet initialized. Used by ShareRunDialog.
    /// </summary>
    public byte[] SnapshotPng()
    {
        if (Host?.State?.Layout == null)
        {
            return null;
        }

        (float renderWidth, float renderHeight) = GetLayoutRenderSize(Host, (float)Bounds.Width, (float)Bounds.Height);
        int w = (int)System.Math.Max(1, System.Math.Round(renderWidth));
        int h = (int)System.Math.Max(1, System.Math.Round(renderHeight));

        Host.UpdateComponentsForRender(w, h);

        using SKSurface surface = CreateFrameSurface(w, h);
        if (surface == null)
        {
            return null;
        }

        if (!RenderFrame(surface, Host, w, h, clearToBlack: false))
        {
            return null;
        }

        using SKImage image = surface.Snapshot();
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data?.ToArray();
    }

    public override void Render(DrawingContext context)
    {
        if (Host is null)
        {
            return;
        }

        Host.Renderer.CalculateOverallSize(Host.State.Layout.Mode);

        context.Custom(new RenderOp(new Rect(Bounds.Size), Host));
    }

    private sealed class RenderOp : ICustomDrawOperation
    {
        private readonly AvaloniaTimerHost _host;

        public RenderOp(Rect bounds, AvaloniaTimerHost host)
        {
            Bounds = bounds;
            _host = host;
        }

        public Rect Bounds { get; }

        public void Dispose() { }

        public bool Equals(ICustomDrawOperation other) => false;

        public bool HitTest(Point p) => Bounds.Contains(p);

        public void Render(ImmediateDrawingContext context)
        {
            ISkiaSharpApiLeaseFeature leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null)
            {
                // Non-Skia rendering backend (e.g. software renderer without the Skia feature
                // exposed). We require Avalonia.Skia, so this shouldn't normally hit.
                return;
            }

            int width = (int)System.Math.Max(1, System.Math.Ceiling(Bounds.Width));
            int height = (int)System.Math.Max(1, System.Math.Ceiling(Bounds.Height));
            using SKSurface surface = CreateFrameSurface(width, height);
            if (surface is null || !RenderFrame(surface, _host, width, height, clearToBlack: true))
            {
                return;
            }

            using SKImage image = surface.Snapshot();
            using ISkiaSharpApiLease lease = leaseFeature.Lease();
            SKCanvas canvas = lease.SkCanvas;
            canvas.DrawImage(
                image,
                SKRect.Create(0f, 0f, image.Width, image.Height),
                SKRect.Create((float)Bounds.X, (float)Bounds.Y, (float)Bounds.Width, (float)Bounds.Height),
                NoSamplingPaint);
        }

    }

    internal static SKSurface CreateFrameSurface(int width, int height)
    {
        var info = new SKImageInfo(
            System.Math.Max(1, width),
            System.Math.Max(1, height),
            SKColorType.Bgra8888,
            SKAlphaType.Premul,
            SKColorSpace.CreateSrgb());
        return SKSurface.Create(info);
    }

    private static bool RenderFrame(SKSurface surface, AvaloniaTimerHost host, float width, float height, bool clearToBlack)
    {
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        if (clearToBlack)
        {
            canvas.DrawRect(0f, 0f, width, height, BlackBackgroundPaint);
        }

        IDrawingContext ctx = new SkiaDrawingContext(surface);
        ApplyMasterRenderSettings(ctx, host.State.LayoutSettings);

        LiveSplitState state = host.State;
        (float layoutWidth, float layoutHeight) = GetLayoutRenderSize(host, width, height);
        host.UpdateComponentsForRender(layoutWidth, layoutHeight);
        LayoutMode mode = state.Layout.Mode;

        // Layout-level background (solid, gradient, or image). Drawn before the per-component
        // scale transform so the fill spans the full window. Per-component backgrounds
        // (set via BackgroundHelper.DrawBackground) layer on top inside each component's
        // own clip region.
        DrawLayoutBackground(ctx, state.LayoutSettings, width, height);

        // Scale so components paint at their natural size; the layout mode picks which
        // axis is the constraint (full height for vertical, full width for horizontal).
        float overallSize = System.Math.Max(host.Renderer.OverallSize, 1f);
        float scale = mode == LayoutMode.Vertical
            ? layoutHeight / overallSize
            : layoutWidth / overallSize;
        if (scale <= 0 || float.IsInfinity(scale) || float.IsNaN(scale))
        {
            return false;
        }

        ctx.ScaleTransform(scale, scale);

        float drawWidth = mode == LayoutMode.Vertical ? layoutWidth / scale : overallSize;
        float drawHeight = mode == LayoutMode.Vertical ? overallSize : layoutHeight / scale;

        ctx.TranslateTransform(-0.5f, -0.5f);
        host.Renderer.Render(ctx, state, drawWidth, drawHeight, mode);
        return true;
    }

    internal static void DrawLayoutBackground(IDrawingContext ctx, LiveSplit.Options.LayoutSettings settings, float width, float height)
    {
        if (settings is null)
        {
            return;
        }

        switch (settings.BackgroundType)
        {
            case LiveSplit.Options.BackgroundType.Image:
            {
                LiveSplit.UI.Drawing.IImage image = settings.GetCachedBackgroundImage();
                if (image is null)
                {
                    // Decode failed or no image set; fall through to a transparent fill so the
                    // window still paints rather than retaining old pixels.
                    return;
                }

                float opacity = System.Math.Clamp(settings.ImageOpacity, 0f, 1f);
                if (opacity <= 0f)
                {
                    return;
                }

                var dest = new System.Drawing.Rectangle(0, 0, (int)System.Math.Ceiling(width), (int)System.Math.Ceiling(height));
                var src = CalculateCoverSourceRect(image.Width, image.Height, width, height);
                float blurSigma = System.Math.Max(0f, settings.ImageBlur * 10f);
                ctx.DrawImageWithOpacity(image, dest, src, opacity, blurSigma);

                break;
            }
            case LiveSplit.Options.BackgroundType.SolidColor:
            {
                if (settings.BackgroundColor.A == 0)
                {
                    return;
                }

                using ISolidBrush brush = DrawingApi.Factory.CreateSolidBrush(settings.BackgroundColor);
                ctx.FillRectangle(brush, 0f, 0f, width, height);
                break;
            }
            case LiveSplit.Options.BackgroundType.VerticalGradient:
            case LiveSplit.Options.BackgroundType.HorizontalGradient:
            {
                if (settings.BackgroundColor.A == 0 && settings.BackgroundColor2.A == 0)
                {
                    return;
                }

                System.Drawing.PointF endPoint = settings.BackgroundType == LiveSplit.Options.BackgroundType.HorizontalGradient
                    ? new System.Drawing.PointF(width, 0f)
                    : new System.Drawing.PointF(0f, height);
                using LiveSplit.UI.Drawing.ILinearGradientBrush brush = DrawingApi.Factory.CreateLinearGradientBrush(
                    new System.Drawing.PointF(0f, 0f), endPoint, settings.BackgroundColor, settings.BackgroundColor2);
                ctx.FillRectangle(brush, 0f, 0f, width, height);
                break;
            }
        }
    }

    internal static System.Drawing.Rectangle CalculateCoverSourceRect(int imageWidth, int imageHeight, float targetWidth, float targetHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0 || targetWidth <= 0f || targetHeight <= 0f)
        {
            return System.Drawing.Rectangle.Empty;
        }

        float imageAspect = imageWidth / (float)imageHeight;
        float targetAspect = targetWidth / targetHeight;
        if (imageAspect > targetAspect)
        {
            int sourceWidth = System.Math.Max(1, (int)System.Math.Round(imageHeight * targetAspect));
            int sourceX = (imageWidth - sourceWidth) / 2;
            return new System.Drawing.Rectangle(sourceX, 0, sourceWidth, imageHeight);
        }

        int sourceHeight = System.Math.Max(1, (int)System.Math.Round(imageWidth / targetAspect));
        int sourceY = (imageHeight - sourceHeight) / 2;
        return new System.Drawing.Rectangle(0, sourceY, imageWidth, sourceHeight);
    }

    internal static void ApplyMasterRenderSettings(IDrawingContext ctx, LayoutSettings settings)
    {
#pragma warning disable CA1416 // The System.Drawing enum values are used by our cross-platform Skia abstraction only.
        ctx.TextRenderingHint = settings?.AntiAliasing == true
            ? System.Drawing.Text.TextRenderingHint.AntiAlias
            : System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        ctx.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.GammaCorrected;
        ctx.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
        ctx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
#pragma warning restore CA1416
    }

    private static (float Width, float Height) GetLayoutRenderSize(AvaloniaTimerHost host, float fallbackWidth, float fallbackHeight)
    {
        UI.ILayout layout = host?.State?.Layout;
        if (layout is null)
        {
            return (System.Math.Max(1f, fallbackWidth), System.Math.Max(1f, fallbackHeight));
        }

        bool vertical = layout.Mode == UI.LayoutMode.Vertical;
        int width = vertical ? layout.VerticalWidth : layout.HorizontalWidth;
        int height = vertical ? layout.VerticalHeight : layout.HorizontalHeight;
        if (width <= 0 || height <= 0 || width == UI.Layout.InvalidSize || height == UI.Layout.InvalidSize)
        {
            return (System.Math.Max(1f, fallbackWidth), System.Math.Max(1f, fallbackHeight));
        }

        return (width, height);
    }

    private static SKPaint BlackBackgroundPaint { get; } = new()
    {
        Style = SKPaintStyle.Fill,
        Color = SKColors.Black,
    };

    private static SKPaint NoSamplingPaint { get; } = new()
    {
        FilterQuality = SKFilterQuality.None,
        IsAntialias = false,
    };
}
