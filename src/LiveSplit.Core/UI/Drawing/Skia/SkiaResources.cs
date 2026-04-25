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
    public Color Color { get; set; }
    public float Width { get; set; }
    public LineJoin LineJoin { get; set; } = LineJoin.Miter;
    public LineCap StartCap { get; set; } = LineCap.Flat;
    public LineCap EndCap { get; set; } = LineCap.Flat;

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

    // Skia only has one stroke cap per paint, so we collapse Start/End to a single value —
    // preferring round/square if either endpoint requests it. GDI+ callers that set both ends
    // to the same cap (the common case) get the expected result.
    public SKStrokeCap SkStrokeCap
    {
        get
        {
            LineCap chosen = StartCap != LineCap.Flat ? StartCap : EndCap;
            return chosen switch
            {
                LineCap.Round => SKStrokeCap.Round,
                LineCap.Square => SKStrokeCap.Square,
                _ => SKStrokeCap.Butt,
            };
        }
    }

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

    public float Ascent => -Font.Metrics.Ascent;
    public float Descent => Font.Metrics.Descent;

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

    /// <summary>
    /// Adds a single line of glyphs to the path, honoring <paramref name="layoutRect"/> for
    /// horizontal + vertical alignment and <see cref="StringTrimming.EllipsisCharacter"/> on
    /// overflow. Multi-line wrapping (the absence of <see cref="StringFormatFlags.NoWrap"/>)
    /// is intentionally not implemented — the only caller is
    /// <see cref="LiveSplit.UI.SimpleLabel"/>'s outline/shadow path, which always sets
    /// <c>NoWrap</c>. A future caller that needs wrapping will need to extend this with an
    /// <see cref="SkiaSharp.HarfBuzz"/>-based shaper.
    /// </summary>
    public void AddString(string text, IFont font, RectangleF layoutRect, ITextFormat format)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var skFont = (SkiaFont)font;

        ushort[] glyphs = skFont.Font.GetGlyphs(text);
        if (glyphs.Length == 0)
        {
            return;
        }

        float[] widths = new float[glyphs.Length];
        skFont.Font.GetGlyphWidths(glyphs, widths, null);

        // Trim with ellipsis if the run overflows and the format requests it. Walk left-to-right,
        // keeping glyphs whose cumulative width plus the ellipsis still fits. Other trimming
        // modes (Word, Character) are uncommon for SimpleLabel and fall through to clipping by
        // the surrounding IDrawingContext clip, which is the natural Skia behavior.
        float totalWidth = 0f;
        for (int i = 0; i < widths.Length; i++)
        {
            totalWidth += widths[i];
        }

        bool overflows = layoutRect.Width > 0f && totalWidth > layoutRect.Width;
        ushort[] keptGlyphs = glyphs;
        float[] keptWidths = widths;
        float finalWidth = totalWidth;

        if (overflows && format != null && format.Trimming == StringTrimming.EllipsisCharacter)
        {
            ushort[] ellipsisGlyphs = skFont.Font.GetGlyphs("…");
            float[] ellipsisWidths = new float[ellipsisGlyphs.Length];
            skFont.Font.GetGlyphWidths(ellipsisGlyphs, ellipsisWidths, null);
            float ellipsisWidth = 0f;
            foreach (float w in ellipsisWidths)
            {
                ellipsisWidth += w;
            }

            float budget = Math.Max(0f, layoutRect.Width - ellipsisWidth);
            int keep = 0;
            float acc = 0f;
            while (keep < glyphs.Length && acc + widths[keep] <= budget)
            {
                acc += widths[keep];
                keep++;
            }

            keptGlyphs = new ushort[keep + ellipsisGlyphs.Length];
            keptWidths = new float[keep + ellipsisGlyphs.Length];
            Array.Copy(glyphs, keptGlyphs, keep);
            Array.Copy(widths, keptWidths, keep);
            Array.Copy(ellipsisGlyphs, 0, keptGlyphs, keep, ellipsisGlyphs.Length);
            Array.Copy(ellipsisWidths, 0, keptWidths, keep, ellipsisGlyphs.Length);
            finalWidth = acc + ellipsisWidth;
        }

        // Horizontal alignment within layoutRect.
        float x = layoutRect.X;
        if (layoutRect.Width > 0f && format != null)
        {
            switch (format.Alignment)
            {
                case StringAlignment.Center:
                    x = layoutRect.X + ((layoutRect.Width - finalWidth) / 2f);
                    break;
                case StringAlignment.Far:
                    x = layoutRect.X + (layoutRect.Width - finalWidth);
                    break;
                case StringAlignment.Near:
                default:
                    x = layoutRect.X;
                    break;
            }
        }

        // Vertical alignment. Skia glyph paths are baseline-relative (y=0 == baseline);
        // Ascent is negative (measured up from baseline), Descent is positive. The line-box
        // height for a single line is (Descent - Ascent), and the baseline sits at lineTop +
        // (-Ascent).
        SKFontMetrics metrics = skFont.Font.Metrics;
        float lineHeight = metrics.Descent - metrics.Ascent;
        float lineTop = layoutRect.Y;
        if (layoutRect.Height > 0f && format != null)
        {
            switch (format.LineAlignment)
            {
                case StringAlignment.Center:
                    lineTop = layoutRect.Y + ((layoutRect.Height - lineHeight) / 2f);
                    break;
                case StringAlignment.Far:
                    lineTop = layoutRect.Y + (layoutRect.Height - lineHeight);
                    break;
                case StringAlignment.Near:
                default:
                    lineTop = layoutRect.Y;
                    break;
            }
        }

        float y = lineTop - metrics.Ascent;

        for (int i = 0; i < keptGlyphs.Length; i++)
        {
            using SKPath glyphPath = skFont.Font.GetGlyphPath(keptGlyphs[i]);
            if (glyphPath is null)
            {
                x += keptWidths[i];
                continue;
            }

            glyphPath.Transform(SKMatrix.CreateTranslation(x, y));
            Path.AddPath(glyphPath);
            x += keptWidths[i];
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
