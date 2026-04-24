using System;
using System.Drawing;
using System.IO;

using SkiaSharp;

namespace LiveSplit.UI.Drawing.Skia;

/// <summary>
/// Factory for the SkiaSharp backing. Registered at app startup on platforms that use the
/// Skia renderer (all non-Windows, and optionally Windows when we switch the UI to Avalonia).
/// </summary>
public sealed class SkiaDrawingFactory : IDrawingFactory
{
    public ISolidBrush CreateSolidBrush(Color color) => new SkiaSolidBrush(color);

    public ILinearGradientBrush CreateLinearGradientBrush(PointF start, PointF end, Color startColor, Color endColor)
        => new SkiaLinearGradientBrush(start, end, startColor, endColor);

    public IPen CreatePen(Color color, float width) => new SkiaPen(color, width);

    public IFont CreateFont(string familyName, float size, System.Drawing.FontStyle style, GraphicsUnit unit)
        => new SkiaFont(familyName, size, style, unit);

    public IImage CreateImage(int width, int height) => new SkiaImage(width, height);

    public IImage LoadImage(Stream stream)
    {
        // SKImage.FromEncodedData copies the data; caller can dispose the Stream after.
        SKImage img = SKImage.FromEncodedData(stream);
        if (img is null)
        {
            throw new InvalidOperationException("Failed to decode image from stream.");
        }

        return new SkiaImage(img);
    }

    public IGraphicsPath CreateGraphicsPath() => new SkiaGraphicsPath();

    public ITextFormat CreateTextFormat() => new SkiaTextFormat();
}
