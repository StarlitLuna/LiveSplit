using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace LiveSplit.UI.Drawing;

/// <summary>
/// Creates backend-specific drawing resources (brushes, pens, fonts, images, paths, text
/// formats). Consumers go through <see cref="DrawingApi.Factory"/> instead of constructing
/// System.Drawing types directly so the rendering backend can be swapped at runtime.
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
/// Process-wide registry for the active <see cref="IDrawingFactory"/>. The application
/// entry point registers a factory before any drawing work happens.
/// </summary>
public static class DrawingApi
{
    private static IDrawingFactory _factory;

    /// <summary>
    /// The active drawing factory. Throws if no factory has been registered yet.
    /// </summary>
    public static IDrawingFactory Factory
    {
        get
        {
            if (_factory is null)
            {
                throw new InvalidOperationException(
                    "No IDrawingFactory has been registered. The application entry point must " +
                    "call DrawingApi.Register(...) before any rendering occurs.");
            }

            return _factory;
        }
    }

    /// <summary>Register the drawing factory for this process. Called once at app startup.</summary>
    public static void Register(IDrawingFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }
}
