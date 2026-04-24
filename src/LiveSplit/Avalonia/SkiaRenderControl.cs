using System;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Platform;
using global::Avalonia.Rendering.SceneGraph;
using global::Avalonia.Skia;
using global::Avalonia.Threading;

using LiveSplit.UI.Drawing;
using LiveSplit.UI.Drawing.Skia;

using SkiaSharp;

namespace LiveSplit.Avalonia;

/// <summary>
/// Avalonia <see cref="Control"/> that invokes the shared render stack through a
/// <see cref="SkiaDrawingContext"/>. Avalonia's desktop rendering backend is Skia-based, so we
/// can lease its <see cref="SKCanvas"/> directly instead of maintaining our own framebuffer.
///
/// Phase 5.2b paints a placeholder marker so "F5 → window opens → rectangle visible" proves the
/// Avalonia bootstrap, SkiaSharp lease feature, and <see cref="SkiaDrawingContext"/> all work
/// end-to-end. Once Phase 5.3 finishes migrating components off the AsGraphics() bridge, this
/// control will route to <c>ComponentRenderer.Render(ctx, …)</c> with a fully-populated
/// <c>LiveSplitState</c> — at which point the window renders the real timer layout.
/// </summary>
public sealed class SkiaRenderControl : Control
{
    public override void Render(DrawingContext context)
    {
        context.Custom(new RenderOp(new Rect(Bounds.Size)));

        // Schedule the next frame — Avalonia is repaint-on-demand by default, so for a running
        // timer we need to pump InvalidateVisual() at the refresh rate.
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Background);
    }

    private sealed class RenderOp : ICustomDrawOperation
    {
        public RenderOp(Rect bounds)
        {
            Bounds = bounds;
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
                // Non-Skia rendering backend (e.g. software renderer without the Skia feature).
                // Phase 5.2b vertical slice requires Avalonia.Skia, so this shouldn't normally hit.
                return;
            }

            using ISkiaSharpApiLease lease = leaseFeature.Lease();
            SKCanvas canvas = lease.SkCanvas;

            IDrawingContext ctx = new SkiaDrawingContext(canvas);

            // Placeholder rendering: a solid rectangle in the middle of the window, plus a
            // smaller one in one corner. If this shows up when you run with --avalonia, the
            // IDrawingContext.FillRectangle path works through SkiaSharp end-to-end.
            using LiveSplit.UI.Drawing.IBrush brush = DrawingApi.Factory.CreateSolidBrush(
                System.Drawing.Color.FromArgb(255, 60, 180, 90));
            ctx.FillRectangle(brush, new System.Drawing.RectangleF(
                (float)Bounds.X + 20f,
                (float)Bounds.Y + 20f,
                (float)Bounds.Width - 40f,
                (float)Bounds.Height - 40f));

            using LiveSplit.UI.Drawing.IBrush corner = DrawingApi.Factory.CreateSolidBrush(
                System.Drawing.Color.FromArgb(255, 220, 60, 60));
            ctx.FillRectangle(corner, new System.Drawing.RectangleF(
                (float)Bounds.X + 8f,
                (float)Bounds.Y + 8f,
                24f, 24f));
        }
    }
}
