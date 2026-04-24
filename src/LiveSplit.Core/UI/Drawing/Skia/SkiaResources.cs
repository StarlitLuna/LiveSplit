using System;
using System.Drawing;
using System.Drawing.Drawing2D;

using SkiaSharp;

namespace LiveSplit.UI.Drawing.Skia;

/// <summary>
/// SkiaSharp implementations of the <see cref="LiveSplit.UI.Drawing"/> resource abstractions.
///
/// Design note: GDI+ <see cref="System.Drawing.Brush"/>/<see cref="System.Drawing.Pen"/> are
/// separate resources that <see cref="System.Drawing.Graphics"/> consumes directly. Skia instead
/// represents both fill and stroke with a single <see cref="SKPaint"/> that lives at draw time.
/// So these wrappers carry just the *state* (color, width, shader, line join) and
/// <see cref="SkiaDrawingContext"/> materializes an <see cref="SKPaint"/> on each call.
/// </summary>

internal sealed class SkiaSolidBrush : ISolidBrush
{
    public Color Color { get; set; }

    public SkiaSolidBrush(Color color)
    {
        Color = color;
    }

    public SKColor SkColor => new(Color.R, Color.G, Color.B, Color.A);

    public void Dispose()
    {
        // Nothing to release — SKColor is a value type.
    }
}

internal sealed class SkiaLinearGradientBrush : ILinearGradientBrush
{
    internal readonly SKShader Shader;

    public SkiaLinearGradientBrush(PointF start, PointF end, Color startColor, Color endColor)
    {
        var startSk = new SKPoint(start.X, start.Y);
        var endSk = new SKPoint(end.X, end.Y);
        Shader = SKShader.CreateLinearGradient(
            startSk, endSk,
            [Convert(startColor), Convert(endColor)],
            [0f, 1f],
            SKShaderTileMode.Clamp);
    }

    internal static SKColor Convert(Color color) => new(color.R, color.G, color.B, color.A);

    public void Dispose() => Shader?.Dispose();
}

internal sealed class SkiaPen : IPen
{
    public Color Color { get; }
    public float Width { get; }
    public LineJoin LineJoin { get; set; } = LineJoin.Miter;

    public SkiaPen(Color color, float width)
    {
        Color = color;
        Width = width;
    }

    public SKStrokeJoin SkStrokeJoin => LineJoin switch
    {
        LineJoin.Round => SKStrokeJoin.Round,
        LineJoin.Bevel => SKStrokeJoin.Bevel,
        _ => SKStrokeJoin.Miter,
    };

    public void Dispose()
    {
        // Nothing to release — the color / width / join state are value types.
    }
}

internal sealed class SkiaFont : IFont
{
    internal readonly SKTypeface Typeface;
    internal readonly SKFont Font;

    public string FamilyName { get; }
    public float Size { get; }
    public System.Drawing.FontStyle Style { get; }
    public GraphicsUnit Unit { get; }

    public SkiaFont(string familyName, float size, System.Drawing.FontStyle style, GraphicsUnit unit)
    {
        FamilyName = familyName;
        Size = size;
        Style = style;
        Unit = unit;

        SKFontStyle skStyle = MapStyle(style);
        Typeface = SKTypeface.FromFamilyName(familyName, skStyle) ?? SKTypeface.Default;
        // Convert size — GDI+ "Point" defaults to 1/72 inch; Skia measures in pixels. Most
        // LiveSplit layouts use GraphicsUnit.Pixel already, so size maps directly. For Point-sized
        // fonts we approximate at 96 DPI until the context provides a real DpiY at draw time.
        float pixelSize = unit == GraphicsUnit.Point ? size * (96f / 72f) : size;
        Font = new SKFont(Typeface, pixelSize);
    }

    private static SKFontStyle MapStyle(System.Drawing.FontStyle style)
    {
        SKFontStyleWeight weight = style.HasFlag(System.Drawing.FontStyle.Bold)
            ? SKFontStyleWeight.Bold
            : SKFontStyleWeight.Normal;
        SKFontStyleSlant slant = style.HasFlag(System.Drawing.FontStyle.Italic)
            ? SKFontStyleSlant.Italic
            : SKFontStyleSlant.Upright;
        return new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);
    }

    public void Dispose()
    {
        Font?.Dispose();
        Typeface?.Dispose();
    }
}

internal sealed class SkiaImage : IImage
{
    internal SKImage SkImage { get; private set; }
    internal SKBitmap SkBitmap { get; private set; }

    public int Width { get; }
    public int Height { get; }

    public SkiaImage(int width, int height)
    {
        Width = width;
        Height = height;
        SkBitmap = new SKBitmap(width, height);
        SkImage = SKImage.FromBitmap(SkBitmap);
    }

    public SkiaImage(SKImage skImage)
    {
        SkImage = skImage ?? throw new ArgumentNullException(nameof(skImage));
        Width = skImage.Width;
        Height = skImage.Height;
    }

    public void Dispose()
    {
        SkImage?.Dispose();
        SkBitmap?.Dispose();
    }
}

internal sealed class SkiaGraphicsPath : IGraphicsPath
{
    internal readonly SKPath Path = new();

    public void Reset() => Path.Reset();

    public void AddString(string text, IFont font, RectangleF layoutRect, ITextFormat format)
    {
        // TODO(phase-5.2): honor layoutRect width/height + ITextFormat alignment the way GDI+'s
        // GraphicsPath.AddString does. For now, lay out the text as a single line starting at the
        // rect's top-left (sufficient for SimpleLabel's shadow/outline rendering once we add
        // proper alignment on top of MeasureString in Phase 5.2).
        var skFont = (SkiaFont)font;
        using SKTextBlob blob = SKTextBlob.Create(text, skFont.Font);
        if (blob is null)
        {
            return;
        }

        // Convert each glyph into a path and add to our path, positioned by the blob's layout.
        // SkiaSharp exposes GetGlyphPath only on SKFont, so we synthesize positions with
        // MeasureText pass.
        ushort[] glyphs = skFont.Font.GetGlyphs(text);
        float[] widths = new float[glyphs.Length];
        skFont.Font.GetGlyphWidths(glyphs, widths, null);

        float x = layoutRect.X;
        float y = layoutRect.Y + skFont.Font.Metrics.CapHeight;
        for (int i = 0; i < glyphs.Length; i++)
        {
            using SKPath glyphPath = skFont.Font.GetGlyphPath(glyphs[i]);
            if (glyphPath is null)
            {
                x += widths[i];
                continue;
            }

            glyphPath.Transform(SKMatrix.CreateTranslation(x, y));
            Path.AddPath(glyphPath);
            x += widths[i];
        }
    }

    public void Dispose() => Path.Dispose();
}

internal sealed class SkiaTextFormat : ITextFormat
{
    public StringAlignment Alignment { get; set; }
    public StringAlignment LineAlignment { get; set; }
    public StringFormatFlags FormatFlags { get; set; }
    public StringTrimming Trimming { get; set; }
}
