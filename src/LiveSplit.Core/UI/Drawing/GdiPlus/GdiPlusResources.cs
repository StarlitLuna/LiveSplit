using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;

namespace LiveSplit.UI.Drawing.GdiPlus;

/// <summary>
/// GDI+ (System.Drawing) implementations of the <see cref="LiveSplit.UI.Drawing"/> resource
/// abstractions. Each type is a thin owning wrapper around its System.Drawing counterpart
/// and forwards Dispose through. Windows-only because System.Drawing.Common's Brush/Pen/Font
/// types are Windows-only; the SkiaSharp resources cover other platforms.
/// </summary>

[SupportedOSPlatform("windows")]
internal sealed class GdiPlusSolidBrush : ISolidBrush
{
    internal readonly SolidBrush Native;

    public GdiPlusSolidBrush(Color color)
    {
        Native = new SolidBrush(color);
    }

    public Color Color
    {
        get => Native.Color;
        set => Native.Color = value;
    }

    public void Dispose() => Native.Dispose();
}

[SupportedOSPlatform("windows")]
internal sealed class GdiPlusLinearGradientBrush : ILinearGradientBrush
{
    internal readonly LinearGradientBrush Native;

    public GdiPlusLinearGradientBrush(PointF start, PointF end, Color startColor, Color endColor)
    {
        Native = new LinearGradientBrush(start, end, startColor, endColor);
    }

    public void Dispose() => Native.Dispose();
}

[SupportedOSPlatform("windows")]
internal sealed class GdiPlusPen : IPen
{
    internal readonly Pen Native;

    public GdiPlusPen(Color color, float width)
    {
        Native = new Pen(color, width);
    }

    public Color Color
    {
        get => Native.Color;
        set => Native.Color = value;
    }

    public float Width
    {
        get => Native.Width;
        set => Native.Width = value;
    }

    public LineJoin LineJoin
    {
        get => Native.LineJoin;
        set => Native.LineJoin = value;
    }

    public LineCap StartCap
    {
        get => Native.StartCap;
        set => Native.StartCap = value;
    }

    public LineCap EndCap
    {
        get => Native.EndCap;
        set => Native.EndCap = value;
    }

    public void Dispose() => Native.Dispose();
}

[SupportedOSPlatform("windows")]
internal sealed class GdiPlusFont : IFont
{
    internal readonly Font Native;

    public GdiPlusFont(string familyName, float size, System.Drawing.FontStyle style, GraphicsUnit unit)
    {
        Native = new Font(familyName, size, style, unit);
    }

    public GdiPlusFont(Font existingFont)
    {
        Native = existingFont;
    }

    public string FamilyName => Native.FontFamily.Name;
    public float Size => Native.Size;
    public System.Drawing.FontStyle Style => Native.Style;
    public GraphicsUnit Unit => Native.Unit;

    public float Ascent
    {
        get
        {
            float emHeight = Native.FontFamily.GetEmHeight(Native.Style);
            return Native.FontFamily.GetCellAscent(Native.Style) * Native.Size / emHeight;
        }
    }

    public float Descent
    {
        get
        {
            float emHeight = Native.FontFamily.GetEmHeight(Native.Style);
            return Native.FontFamily.GetCellDescent(Native.Style) * Native.Size / emHeight;
        }
    }

    public void Dispose() => Native.Dispose();
}

[SupportedOSPlatform("windows")]
internal sealed class GdiPlusImage : IImage
{
    internal readonly Image Native;
    private readonly bool _ownsNative;

    public GdiPlusImage(int width, int height)
    {
        Native = new Bitmap(width, height);
        _ownsNative = true;
    }

    public GdiPlusImage(Image existingImage, bool ownsImage = false)
    {
        Native = existingImage;
        _ownsNative = ownsImage;
    }

    public int Width => Native.Width;
    public int Height => Native.Height;

    public void Dispose()
    {
        if (_ownsNative)
        {
            Native.Dispose();
        }
    }
}

[SupportedOSPlatform("windows")]
internal sealed class GdiPlusGraphicsPath : IGraphicsPath
{
    internal readonly GraphicsPath Native;

    public GdiPlusGraphicsPath()
    {
        Native = new GraphicsPath();
    }

    public void Reset() => Native.Reset();

    public void AddString(string text, IFont font, RectangleF layoutRect, ITextFormat format)
    {
        var gdiFont = (GdiPlusFont)font;
        var gdiFormat = (GdiPlusTextFormat)format;
        Native.AddString(text, gdiFont.Native.FontFamily, (int)gdiFont.Native.Style, gdiFont.Native.Size, layoutRect, gdiFormat.Native);
    }

    public void Dispose() => Native.Dispose();
}

[SupportedOSPlatform("windows")]
internal sealed class GdiPlusTextFormat : ITextFormat
{
    internal readonly StringFormat Native;

    public GdiPlusTextFormat()
    {
        Native = new StringFormat();
    }

    public StringAlignment Alignment
    {
        get => Native.Alignment;
        set => Native.Alignment = value;
    }

    public StringAlignment LineAlignment
    {
        get => Native.LineAlignment;
        set => Native.LineAlignment = value;
    }

    public StringFormatFlags FormatFlags
    {
        get => Native.FormatFlags;
        set => Native.FormatFlags = value;
    }

    public StringTrimming Trimming
    {
        get => Native.Trimming;
        set => Native.Trimming = value;
    }
}
