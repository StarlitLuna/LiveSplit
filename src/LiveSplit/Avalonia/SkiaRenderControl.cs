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

using SkiaSharp;

namespace LiveSplit.Avalonia;

/// <summary>
/// Avalonia <see cref="Control"/> that runs <see cref="ComponentRenderer"/> through a
/// <see cref="SkiaDrawingContext"/>. Avalonia's desktop rendering backend is Skia-based, so we
/// lease its <see cref="SKCanvas"/> directly via <see cref="ISkiaSharpApiLeaseFeature"/> rather
/// than maintaining a separate framebuffer. <see cref="AvaloniaTimerHost"/> pumps
/// <c>InvalidateVisual</c> at ~30 Hz to drive the running clock.
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

        int w = (int)System.Math.Max(1, Bounds.Width);
        int h = (int)System.Math.Max(1, Bounds.Height);

        Host.Renderer.CalculateOverallSize(Host.State.Layout.Mode);

        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
        if (surface == null)
        {
            return null;
        }

        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        IDrawingContext ctx = new SkiaDrawingContext(canvas);
        LayoutMode mode = Host.State.Layout.Mode;

        RenderOp.DrawLayoutBackgroundInner(ctx, Host.State.LayoutSettings, w, h);

        float overallSize = Host.Renderer.OverallSize;
        float scale = mode == LayoutMode.Vertical ? h / overallSize : w / overallSize;
        if (scale > 0 && !float.IsInfinity(scale) && !float.IsNaN(scale))
        {
            ctx.ScaleTransform(scale, scale);
        }

        float drawWidth = mode == LayoutMode.Vertical ? w / scale : overallSize;
        float drawHeight = mode == LayoutMode.Vertical ? overallSize : h / scale;
        Host.Renderer.Render(ctx, Host.State, drawWidth, drawHeight, mode);

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

            using ISkiaSharpApiLease lease = leaseFeature.Lease();
            SKCanvas canvas = lease.SkCanvas;

            IDrawingContext ctx = new SkiaDrawingContext(canvas);

            LiveSplitState state = _host.State;
            float width = (float)Bounds.Width;
            float height = (float)Bounds.Height;
            LayoutMode mode = state.Layout.Mode;

            // Layout-level background (solid, gradient, or image). Drawn before the per-component
            // scale transform so the fill spans the full window. Per-component backgrounds
            // (set via BackgroundHelper.DrawBackground) layer on top inside each component's
            // own clip region.
            DrawLayoutBackgroundInner(ctx, state.LayoutSettings, width, height);

            // Scale so components paint at their natural size; the layout mode picks which
            // axis is the constraint (full height for vertical, full width for horizontal).
            float overallSize = _host.Renderer.OverallSize;
            float scale = mode == LayoutMode.Vertical
                ? height / overallSize
                : width / overallSize;
            if (scale > 0 && !float.IsInfinity(scale) && !float.IsNaN(scale))
            {
                ctx.ScaleTransform(scale, scale);
            }

            float drawWidth = mode == LayoutMode.Vertical ? width / scale : overallSize;
            float drawHeight = mode == LayoutMode.Vertical ? overallSize : height / scale;

            _host.Renderer.Render(ctx, state, drawWidth, drawHeight, mode);
        }

        internal static void DrawLayoutBackgroundInner(IDrawingContext ctx, LiveSplit.Options.LayoutSettings settings, float width, float height)
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
                    var src = new System.Drawing.Rectangle(0, 0, image.Width, image.Height);
                    if (opacity >= 1f)
                    {
                        ctx.DrawImage(image, dest, src);
                    }
                    else
                    {
                        ctx.DrawImageWithOpacity(image, dest, src, opacity);
                    }

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
    }
}
