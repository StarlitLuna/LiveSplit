using System;
using System.Drawing;

using LiveSplit.UI.Drawing.GdiPlus;

namespace LiveSplit.UI.Drawing;

/// <summary>
/// Transitional compatibility bridge between <see cref="IDrawingContext"/> and the underlying
/// <see cref="System.Drawing.Graphics"/> for call sites that still construct
/// <see cref="Brush"/> / <see cref="Pen"/> / <see cref="Font"/> directly.
///
/// Phase 4b routes every component, the layout renderer, and the TimerForm paint loop through
/// IDrawingContext for the drawing surface seam, but defers the full resource-type migration
/// (SolidBrush → ISolidBrush etc.) to Phase 5's backend swap. Until that lands, component
/// drawing code calls <see cref="AsGraphics"/> at entry and keeps its existing System.Drawing
/// usage unchanged — this file will be deleted when Phase 5 finishes the swap.
/// </summary>
public static class GraphicsCompatibilityExtensions
{
    /// <summary>
    /// Returns the underlying <see cref="Graphics"/> of a GDI+-backed context. Throws on any
    /// other backing (e.g. the future SkiaSharp-backed context) — caller must have migrated
    /// off System.Drawing resources by then.
    /// </summary>
    public static Graphics AsGraphics(this IDrawingContext ctx)
    {
        if (ctx is GdiPlusDrawingContext gdi)
        {
            return gdi.UnwrapGraphics();
        }

        throw new NotSupportedException(
            $"IDrawingContext.AsGraphics() is a GDI+-only compatibility bridge. The current " +
            $"backing is {ctx?.GetType().FullName ?? "null"}; migrate this call site to use " +
            $"IBrush / IPen / IFont / etc. directly before Phase 5 replaces the GDI+ backend.");
    }
}
