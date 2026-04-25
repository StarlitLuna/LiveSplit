using System;
using System.Drawing;

using LiveSplit.UI.Drawing.GdiPlus;

namespace LiveSplit.UI.Drawing;

/// <summary>
/// Bridge from <see cref="IDrawingContext"/> down to the underlying
/// <see cref="System.Drawing.Graphics"/> for call sites that still need direct access to a
/// <see cref="Graphics"/> (e.g. drawing a <see cref="System.Drawing.Image"/>). Only works
/// when the active backing is GDI+; throws otherwise so non-GDI+ paths don't silently
/// degrade.
/// </summary>
public static class GraphicsCompatibilityExtensions
{
    public static Graphics AsGraphics(this IDrawingContext ctx)
    {
        if (ctx is GdiPlusDrawingContext gdi)
        {
            return gdi.UnwrapGraphics();
        }

        throw new NotSupportedException(
            $"IDrawingContext.AsGraphics() requires a GDI+ backing. The current backing is " +
            $"{ctx?.GetType().FullName ?? "null"}; this call site must migrate to IBrush / IPen / " +
            $"IFont primitives.");
    }
}
