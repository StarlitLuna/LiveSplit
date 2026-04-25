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
/// Avalonia <see cref="Control"/> that runs the LiveSplit <see cref="ComponentRenderer"/> through
/// a <see cref="SkiaDrawingContext"/>. Avalonia's desktop rendering backend is Skia-based, so we
/// can lease its <see cref="SKCanvas"/> directly instead of maintaining our own framebuffer.
///
/// The control is paint-driven; <see cref="AvaloniaTimerHost"/> pumps <c>InvalidateVisual</c> at
/// ~30 Hz so the timer label updates while the user isn't interacting. When <see cref="Host"/>
/// hasn't been assigned yet (the window is still wiring up), it draws nothing — Avalonia's window
/// background fills the surface.
/// </summary>
public sealed class SkiaRenderControl : Control
{
    public AvaloniaTimerHost Host { get; set; }

    public override void Render(DrawingContext context)
    {
        if (Host is null)
        {
            return;
        }

        // Push the renderer Update tick so components see fresh state before being drawn.
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
                // Non-Skia backend (e.g. software renderer without the Skia feature). The
                // vertical slice requires Avalonia.Skia, so this shouldn't normally hit.
                return;
            }

            using ISkiaSharpApiLease lease = leaseFeature.Lease();
            SKCanvas canvas = lease.SkCanvas;

            IDrawingContext ctx = new SkiaDrawingContext(canvas);

            LiveSplitState state = _host.State;
            float width = (float)Bounds.Width;
            float height = (float)Bounds.Height;
            LayoutMode mode = state.Layout.Mode;

            // Apply the layout's logical scale so the components paint at their natural size.
            // Mirrors TimerForm.PaintForm's `g.ScaleTransform(scale, scale)` block, but we let the
            // mode pick which axis is the constraint.
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
