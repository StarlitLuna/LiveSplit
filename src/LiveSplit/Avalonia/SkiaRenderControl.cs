using System;

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
        float overallSize = Host.Renderer.OverallSize;
        float scale = mode == LayoutMode.Vertical ? h / overallSize : w / overallSize;
        if (scale > 0 && !float.IsInfinity(scale) && !float.IsNaN(scale))
        {
            ctx.ScaleTransform(scale, scale);
        }

        float drawWidth = mode == LayoutMode.Vertical ? w / scale : overallSize;
        float drawHeight = mode == LayoutMode.Vertical ? overallSize : h / scale;
        Host.Renderer.Render(ctx, Host.State, drawWidth, drawHeight, mode, null);

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

            _host.Renderer.Render(ctx, state, drawWidth, drawHeight, mode, null);
        }
    }
}
