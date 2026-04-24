using System;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;

namespace LiveSplit.UI.Drawing.GdiPlus;

[SupportedOSPlatform("windows")]
public sealed class GdiPlusDrawingFactory : IDrawingFactory
{
    public ISolidBrush CreateSolidBrush(Color color) => new GdiPlusSolidBrush(color);

    public ILinearGradientBrush CreateLinearGradientBrush(PointF start, PointF end, Color startColor, Color endColor)
        => new GdiPlusLinearGradientBrush(start, end, startColor, endColor);

    public IPen CreatePen(Color color, float width) => new GdiPlusPen(color, width);

    public IFont CreateFont(string familyName, float size, System.Drawing.FontStyle style, GraphicsUnit unit)
        => new GdiPlusFont(familyName, size, style, unit);

    public IImage CreateImage(int width, int height) => new GdiPlusImage(width, height);

    public IImage LoadImage(Stream stream)
    {
        // The Image owns the stream data here; caller is responsible for disposing the returned IImage.
        Image img = Image.FromStream(stream);
        return new GdiPlusImage(img, ownsImage: true);
    }

    public IGraphicsPath CreateGraphicsPath() => new GdiPlusGraphicsPath();

    public ITextFormat CreateTextFormat() => new GdiPlusTextFormat();
}
