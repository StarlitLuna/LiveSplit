using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace LiveSplit.UI.Drawing;

/// <summary>
/// Creates backend-specific drawing resources (brushes, pens, fonts, images, paths, text formats).
/// Components use <see cref="DrawingApi.Factory"/> rather than constructing System.Drawing types
/// directly, so the whole stack can be swapped in Phase 5 when we replace GDI+ with SkiaSharp.
/// </summary>
public interface IDrawingFactory
{
    ISolidBrush CreateSolidBrush(Color color);
    ILinearGradientBrush CreateLinearGradientBrush(PointF start, PointF end, Color startColor, Color endColor);

    IPen CreatePen(Color color, float width);

    IFont CreateFont(string familyName, float size, System.Drawing.FontStyle style, GraphicsUnit unit);

    /// <summary>Create a blank mutable image of the given size.</summary>
    IImage CreateImage(int width, int height);
    /// <summary>Load an image from a raw byte stream (PNG, JPEG, BMP, etc.).</summary>
    IImage LoadImage(Stream stream);

    IGraphicsPath CreateGraphicsPath();

    ITextFormat CreateTextFormat();
}

/// <summary>
/// Selects the drawing backend for the current OS. Today: GDI+ on Windows only (Phase 4a has
/// no Linux backend yet — that lands with the Avalonia/SkiaSharp swap in Phase 5).
/// </summary>
public static class DrawingApi
{
    private static IDrawingFactory _factory;

    /// <summary>
    /// The active drawing factory. Lazily initializes to <c>GdiPlusDrawingFactory</c> on Windows
    /// via the registered default resolver; other platforms must explicitly register a factory
    /// before any drawing work happens (Phase 5 does this at app startup).
    /// </summary>
    public static IDrawingFactory Factory
    {
        get
        {
            if (_factory is null)
            {
                throw new InvalidOperationException(
                    $"No IDrawingFactory has been registered. On Windows this is wired up by " +
                    $"{nameof(LiveSplit.UI.Drawing.GdiPlus)}.GdiPlusDrawingFactory; on Linux the " +
                    $"Phase 5 Avalonia/SkiaSharp bootstrap must register one at startup.");
            }

            return _factory;
        }
    }

    /// <summary>
    /// Register the drawing factory for this process. Called once at app startup — on Windows
    /// by the WinForms entry point, on Linux by the Avalonia entry point (Phase 5).
    /// </summary>
    public static void Register(IDrawingFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }
}
