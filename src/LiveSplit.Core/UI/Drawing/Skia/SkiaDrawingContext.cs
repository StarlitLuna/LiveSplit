using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

using SkiaSharp;

namespace LiveSplit.UI.Drawing.Skia;

/// <summary>
/// <see cref="IDrawingContext"/> implementation backed by a <see cref="SKCanvas"/>. The canvas
/// lives for the duration of one paint call; this context does not own it.
///
/// Model impedance notes:
///   * GDI+ exposes clip/transform as mutable state properties; Skia uses Save/Restore stacks.
///     <see cref="Save"/> wraps <see cref="SKCanvas.Save"/> and Restore happens when the returned
///     IDrawingState is disposed.
///   * GDI+ has a single-shot <c>Graphics.Clip = new Region();</c> to reset clipping. Skia has no
///     way to *widen* a clip; only further intersection. We implement <see cref="ClearClip"/> by
///     restoring to a canvas save-count the caller captured at entry. Callers that need a full
///     reset should take an initial <see cref="Save"/> at their entry point and restore it before
///     reaching for <c>ClearClip</c>.
///   * Quality enums (SmoothingMode, TextRenderingHint, InterpolationMode, CompositingQuality,
///     CompositingMode, PixelOffsetMode) are GDI+ canvas-level properties. Skia has no direct
///     equivalent — those settings are per-SKPaint (IsAntialias, ColorFilter, ImageFilter, etc.).
///     We store them as context-level state and apply them when materializing an SKPaint for each
///     draw call.
/// </summary>
public sealed class SkiaDrawingContext : IDrawingContext
{
    private readonly SKCanvas _canvas;
    private readonly SKSurface _surface;
    private readonly int _baseSaveCount;

    public SkiaDrawingContext(SKCanvas canvas, float dpiX = 96f, float dpiY = 96f)
        : this(canvas, null, dpiX, dpiY)
    {
    }

    public SkiaDrawingContext(SKSurface surface, float dpiX = 96f, float dpiY = 96f)
        : this(surface?.Canvas ?? throw new ArgumentNullException(nameof(surface)), surface, dpiX, dpiY)
    {
    }

    private SkiaDrawingContext(SKCanvas canvas, SKSurface surface, float dpiX, float dpiY)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _surface = surface;
        _baseSaveCount = canvas.SaveCount;
        DpiX = dpiX;
        DpiY = dpiY;
    }

    public float DpiX { get; }
    public float DpiY { get; }

    // --- Quality settings (stored on the context, applied per-SKPaint) ---
    public SmoothingMode SmoothingMode { get; set; } = SmoothingMode.Default;
    public TextRenderingHint TextRenderingHint { get; set; } = TextRenderingHint.SystemDefault;
    public InterpolationMode InterpolationMode { get; set; } = InterpolationMode.Default;
    public CompositingQuality CompositingQuality { get; set; } = CompositingQuality.Default;
    public CompositingMode CompositingMode { get; set; } = CompositingMode.SourceOver;
    public PixelOffsetMode PixelOffsetMode { get; set; } = PixelOffsetMode.Default;

    private bool IsAntialias =>
        SmoothingMode == SmoothingMode.AntiAlias || SmoothingMode == SmoothingMode.HighQuality;

    // --- Drawing primitives ---

    public void FillRectangle(IBrush brush, RectangleF rect)
    {
        if (TryFillGammaCorrectedSolidRectangle(brush, rect))
        {
            return;
        }

        if (TryDrawSnappedFillRectangle(brush, rect))
        {
            return;
        }

        using SKPaint paint = CreateFillPaint(brush);
        paint.IsAntialias = false;
        _canvas.DrawRect(ToSk(rect), paint);
    }

    public void FillRectangle(IBrush brush, float x, float y, float width, float height)
    {
        if (TryFillGammaCorrectedSolidRectangle(brush, new RectangleF(x, y, width, height)))
        {
            return;
        }

        if (TryDrawSnappedFillRectangle(brush, new RectangleF(x, y, width, height)))
        {
            return;
        }

        using SKPaint paint = CreateFillPaint(brush);
        paint.IsAntialias = false;
        _canvas.DrawRect(x, y, width, height, paint);
    }

    public void DrawRectangle(IPen pen, RectangleF rect)
    {
        using SKPaint paint = CreateStrokePaint(pen);
        _canvas.DrawRect(ToSk(rect), paint);
    }

    public void DrawLine(IPen pen, PointF p1, PointF p2)
    {
        using SKPaint paint = CreateStrokePaint(pen);
        _canvas.DrawLine(p1.X, p1.Y, p2.X, p2.Y, paint);
    }

    public void FillPolygon(IBrush brush, PointF[] points)
    {
        if (points == null || points.Length < 3)
        {
            return;
        }

        using SKPaint paint = CreateFillPaint(brush);
        using var path = new SKPath();
        path.MoveTo(points[0].X, points[0].Y);
        for (int i = 1; i < points.Length; i++)
        {
            path.LineTo(points[i].X, points[i].Y);
        }

        path.Close();
        _canvas.DrawPath(path, paint);
    }

    public void FillEllipse(IBrush brush, float x, float y, float width, float height)
    {
        using SKPaint paint = CreateFillPaint(brush);
        _canvas.DrawOval(SKRect.Create(x, y, width, height), paint);
    }

    public void DrawImage(IImage image, RectangleF destRect)
    {
        var skImage = ((SkiaImage)image).SkImage;
        using SKPaint paint = CreateImagePaint();
        _canvas.DrawImage(skImage, ToSk(destRect), paint);
    }

    public void DrawImage(IImage image, Rectangle destRect, Rectangle srcRect)
    {
        var skImage = ((SkiaImage)image).SkImage;
        using SKPaint paint = CreateImagePaint();
        _canvas.DrawImage(skImage, ToSk(srcRect), ToSk(destRect), paint);
    }

    public void DrawImageWithOpacity(IImage image, Rectangle destRect, Rectangle srcRect, float opacity, float blurSigma = 0f)
        => DrawImageWithOpacity(
            image,
            new RectangleF(destRect.X, destRect.Y, destRect.Width, destRect.Height),
            new RectangleF(srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height),
            opacity,
            blurSigma);

    public void DrawImageWithOpacity(IImage image, RectangleF destRect, RectangleF srcRect, float opacity, float blurSigma = 0f)
    {
        var skImage = ((SkiaImage)image).SkImage;
        SKImageFilter imageFilter = null;
        try
        {
            if (blurSigma > 0f)
            {
                imageFilter = SKImageFilter.CreateBlur(blurSigma, blurSigma);
            }

            using SKPaint paint = CreateImagePaint(Math.Clamp(opacity, 0f, 1f), imageFilter);
            _canvas.DrawImage(skImage, ToSk(srcRect), ToSk(destRect), paint);
        }
        finally
        {
            imageFilter?.Dispose();
        }
    }

    public void DrawString(string text, IFont font, IBrush brush, RectangleF bounds, ITextFormat format)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var skFont = (SkiaFont)font;
        using SKFont renderFont = CreateRenderFont(skFont, TextRenderingHint);
        using SKPaint paint = CreateTextFillPaint(brush);

        var skFormat = (SkiaTextFormat)format;
        float ascent = skFont.Ascent;
        float descent = skFont.Descent;

        // Honor StringTrimming.EllipsisCharacter: GDI+ trims automatically when the string
        // overflows the layout rect; Skia needs an explicit measure + truncate. Other trimming
        // modes are not currently used by any component, so they fall through to plain draw.
        if (skFormat.Trimming == StringTrimming.EllipsisCharacter && bounds.Width > 0)
        {
            text = TrimWithEllipsis(text, renderFont, bounds.Width);
        }

        float textWidth = MeasureText(renderFont, text);
        float textHeight = ascent + descent;

        float x = skFormat.Alignment switch
        {
            StringAlignment.Center => bounds.X + ((bounds.Width - textWidth) / 2f),
            StringAlignment.Far => bounds.X + bounds.Width - textWidth,
            _ => bounds.X,
        };

        // Skia's DrawText y-coordinate is the baseline, not the top, so offset by -ascent.
        float baselineY = skFormat.LineAlignment switch
        {
            StringAlignment.Center => bounds.Y + ((bounds.Height - textHeight) / 2f) + ascent,
            StringAlignment.Far => bounds.Y + bounds.Height - descent,
            _ => bounds.Y + ascent,
        };

        _canvas.DrawText(text, x, baselineY, renderFont, paint);
    }

    public SizeF MeasureString(string text, IFont font, int maxWidth, ITextFormat format)
    {
        if (string.IsNullOrEmpty(text))
        {
            return SizeF.Empty;
        }

        var skFont = (SkiaFont)font;
        using SKFont renderFont = CreateRenderFont(skFont, TextRenderingHint);
        float width = MeasureText(renderFont, text);
        float height = skFont.Ascent + skFont.Descent + skFont.GdiMeasureHeightPadding;
        return new SizeF(width, height);
    }

    private static float MeasureText(SKFont font, ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return 0f;
        }

        int count = font.CountGlyphs(text);
        if (count == 0)
        {
            return 0f;
        }

        ushort[] glyphs = new ushort[count];
        font.GetGlyphs(text, glyphs);
        return font.MeasureText(glyphs);
    }

    /// <summary>
    /// Truncate <paramref name="text"/> with a single ellipsis character when its measured
    /// width exceeds <paramref name="maxWidth"/>. Binary-search the longest prefix that fits
    /// with "…" appended; matches GDI+ <see cref="StringTrimming.EllipsisCharacter"/> output
    /// closely enough for the layout-driven components that already pre-cut at the SimpleLabel
    /// level.
    /// </summary>
    private static string TrimWithEllipsis(string text, SKFont skFont, float maxWidth)
    {
        const string Ellipsis = "…";
        if (MeasureText(skFont, text) <= maxWidth)
        {
            return text;
        }

        float ellipsisWidth = MeasureText(skFont, Ellipsis);
        if (ellipsisWidth >= maxWidth)
        {
            // The ellipsis itself doesn't fit — fall back to drawing nothing rather than
            // emitting a glyph that overflows the bounds.
            return string.Empty;
        }

        int low = 0;
        int high = text.Length;
        int best = 0;
        while (low <= high)
        {
            int mid = (low + high) / 2;
            float width = MeasureText(skFont, text.AsSpan(0, mid)) + ellipsisWidth;
            if (width <= maxWidth)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return string.Concat(text.AsSpan(0, best), Ellipsis);
    }

    internal static SKFont CreateRenderFont(SkiaFont source, TextRenderingHint textRenderingHint)
    {
        var font = new SKFont(source.Typeface, source.Font.Size, source.Font.ScaleX, source.Font.SkewX);
        ApplyTextRenderingHint(font, textRenderingHint);
        return font;
    }

    private static void ApplyTextRenderingHint(SKFont font, TextRenderingHint textRenderingHint)
    {
        switch (textRenderingHint)
        {
            case TextRenderingHint.AntiAlias:
                font.Edging = SKFontEdging.Antialias;
                font.Hinting = SKFontHinting.None;
                font.Subpixel = false;
                break;
            case TextRenderingHint.AntiAliasGridFit:
                font.Edging = SKFontEdging.Antialias;
                font.Hinting = SKFontHinting.Full;
                font.Subpixel = false;
                break;
            case TextRenderingHint.ClearTypeGridFit:
                font.Edging = SKFontEdging.SubpixelAntialias;
                font.Hinting = SKFontHinting.Full;
                font.Subpixel = true;
                break;
            case TextRenderingHint.SingleBitPerPixel:
                font.Edging = SKFontEdging.Alias;
                font.Hinting = SKFontHinting.None;
                font.Subpixel = false;
                break;
            case TextRenderingHint.SingleBitPerPixelGridFit:
                font.Edging = SKFontEdging.Alias;
                font.Hinting = SKFontHinting.Full;
                font.Subpixel = false;
                break;
            default:
                font.Edging = SKFontEdging.Antialias;
                font.Hinting = SKFontHinting.Normal;
                font.Subpixel = false;
                break;
        }
    }

    public void FillPath(IBrush brush, IGraphicsPath path)
    {
        using SKPaint paint = CreateFillPaint(brush);
        _canvas.DrawPath(((SkiaGraphicsPath)path).Path, paint);
    }

    public void DrawPath(IPen pen, IGraphicsPath path)
    {
        using SKPaint paint = CreateStrokePaint(pen);
        _canvas.DrawPath(((SkiaGraphicsPath)path).Path, paint);
    }

    // --- Transform ---

    public void TranslateTransform(float dx, float dy) => _canvas.Translate(dx, dy);

    public void ScaleTransform(float sx, float sy) => _canvas.Scale(sx, sy);

    public void ResetTransform() => _canvas.ResetMatrix();

    public System.Numerics.Matrix3x2 GetTransform()
    {
        SKMatrix m = _canvas.TotalMatrix;
        // SKMatrix is 3x3 but the bottom row (perspective) is (0, 0, 1) for affine transforms.
        // SKMatrix.Values layout: [ ScaleX, SkewX, TransX, SkewY, ScaleY, TransY, Persp0, Persp1, Persp2 ].
        return new System.Numerics.Matrix3x2(
            m11: m.ScaleX, m12: m.SkewY,
            m21: m.SkewX, m22: m.ScaleY,
            m31: m.TransX, m32: m.TransY);
    }

    // --- Clip ---

    public void ClearClip()
    {
        // Skia can't widen a clip in place; restore to the save-count captured at construction
        // (which represents "no per-frame clipping applied yet"), then re-save so subsequent
        // Save/Restore calls don't underflow our base count.
        while (_canvas.SaveCount > _baseSaveCount)
        {
            _canvas.Restore();
        }

        _canvas.Save();
    }

    public void SetClip(RectangleF rect)
    {
        // Sets-and-restricts (intersection). Replacing the clip with one wider than the current
        // is not expressible in Skia without rewinding saves; callers should structure their
        // Save / Restore boundaries to scope the clip explicitly.
        _canvas.ClipRect(ToSk(rect), SKClipOperation.Intersect);
    }

    public void IntersectClip(RectangleF rect) => _canvas.ClipRect(ToSk(rect), SKClipOperation.Intersect);

    public bool IsVisible(RectangleF rect) => !_canvas.QuickReject(ToSk(rect));

    // --- State save/restore ---

    public IDrawingState Save() => new SkiaDrawingState(this, _canvas);

    // --- Helpers ---

    private SKPaint CreateFillPaint(IBrush brush)
    {
        var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = IsAntialias,
        };

        switch (brush)
        {
            case SkiaSolidBrush solid:
                paint.Color = solid.SkColor;
                break;
            case SkiaLinearGradientBrush gradient:
                paint.Shader = gradient.Shader;
                break;
            default:
                paint.Dispose();
                throw new ArgumentException(
                    $"Unsupported IBrush implementation: {brush?.GetType().FullName ?? "null"}. " +
                    $"SkiaDrawingContext expects brushes created by SkiaDrawingFactory.",
                    nameof(brush));
        }

        return paint;
    }

    private SKPaint CreateTextFillPaint(IBrush brush)
    {
        SKPaint paint = CreateFillPaint(brush);
        paint.IsAntialias = TextRenderingHint is not TextRenderingHint.SingleBitPerPixel
            and not TextRenderingHint.SingleBitPerPixelGridFit;
        return paint;
    }

    private SKPaint CreateStrokePaint(IPen pen)
    {
        var skPen = (SkiaPen)pen;
        return new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = skPen.Width,
            StrokeJoin = skPen.SkStrokeJoin,
            StrokeCap = skPen.SkStrokeCap,
            Color = new SKColor(skPen.Color.R, skPen.Color.G, skPen.Color.B, skPen.Color.A),
            IsAntialias = IsAntialias,
        };
    }

    private static SKRect ToSk(RectangleF r) => SKRect.Create(r.X, r.Y, r.Width, r.Height);
    private static SKRectI ToSk(Rectangle r) => SKRectI.Create(r.X, r.Y, r.Width, r.Height);

    private SKPaint CreateImagePaint(float opacity = 1f, SKImageFilter imageFilter = null)
        => new()
        {
            Color = new SKColor(255, 255, 255, (byte)(Math.Clamp(opacity, 0f, 1f) * 255)),
            IsAntialias = IsAntialias,
            ImageFilter = imageFilter,
            FilterQuality = InterpolationMode switch
            {
                InterpolationMode.NearestNeighbor or InterpolationMode.Low => SKFilterQuality.None,
                InterpolationMode.Bicubic or InterpolationMode.HighQualityBicubic => SKFilterQuality.High,
                _ => SKFilterQuality.Low,
            },
        };

    private bool TryDrawSnappedFillRectangle(IBrush brush, RectangleF rect)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return true;
        }

        SKMatrix matrix = _canvas.TotalMatrix;
        if (matrix.SkewX != 0f
            || matrix.SkewY != 0f
            || matrix.Persp0 != 0f
            || matrix.Persp1 != 0f
            || matrix.Persp2 != 1f
            || !matrix.TryInvert(out SKMatrix inverse))
        {
            return false;
        }

        SKRect mapped = matrix.MapRect(ToSk(rect));
        float left = Math.Min(mapped.Left, mapped.Right);
        float right = Math.Max(mapped.Left, mapped.Right);
        float top = Math.Min(mapped.Top, mapped.Bottom);
        float bottom = Math.Max(mapped.Top, mapped.Bottom);

        float snappedLeft = MathF.Ceiling(left);
        float snappedRight = MathF.Ceiling(right);
        float snappedTop = MathF.Ceiling(top);
        float snappedBottom = MathF.Ceiling(bottom);
        if (snappedLeft >= snappedRight || snappedTop >= snappedBottom)
        {
            return true;
        }

        SKRect snappedLocal = inverse.MapRect(SKRect.Create(
            snappedLeft,
            snappedTop,
            snappedRight - snappedLeft,
            snappedBottom - snappedTop));

        using SKPaint paint = CreateFillPaint(brush);
        paint.IsAntialias = false;
        _canvas.DrawRect(snappedLocal, paint);
        return true;
    }

    private bool TryFillGammaCorrectedSolidRectangle(IBrush brush, RectangleF rect)
    {
        if (_surface is null
            || CompositingQuality != CompositingQuality.GammaCorrected
            || brush is not SkiaSolidBrush solid
            || rect.Width <= 0f
            || rect.Height <= 0f)
        {
            return false;
        }

        Color color = solid.Color;
        if (color.A == 0)
        {
            return true;
        }

        if (color.A == 255)
        {
            return false;
        }

        using SKPixmap pixmap = _surface.PeekPixels();
        if (pixmap is null
            || pixmap.ColorType != SKColorType.Bgra8888
            || pixmap.AlphaType != SKAlphaType.Premul
            || pixmap.BytesPerPixel != 4)
        {
            return false;
        }

        SKMatrix matrix = _canvas.TotalMatrix;
        if (matrix.SkewX != 0f
            || matrix.SkewY != 0f
            || matrix.Persp0 != 0f
            || matrix.Persp1 != 0f
            || matrix.Persp2 != 1f)
        {
            return false;
        }

        SKRect mapped = matrix.MapRect(ToSk(rect));
        float left = Math.Min(mapped.Left, mapped.Right);
        float right = Math.Max(mapped.Left, mapped.Right);
        float top = Math.Min(mapped.Top, mapped.Bottom);
        float bottom = Math.Max(mapped.Top, mapped.Bottom);
        SKRectI clip = _canvas.DeviceClipBounds;

        int startX = Math.Max(clip.Left, (int)Math.Ceiling(left));
        int endX = Math.Min(clip.Right, (int)Math.Ceiling(right));
        int startY = Math.Max(clip.Top, (int)Math.Ceiling(top));
        int endY = Math.Min(clip.Bottom, (int)Math.Ceiling(bottom));
        if (startX >= endX || startY >= endY)
        {
            return true;
        }

        Span<byte> pixels = pixmap.GetPixelSpan<byte>();
        double sourceAlpha = color.A / 255d;
        double sourceRed = SrgbToLinear(color.R / 255d);
        double sourceGreen = SrgbToLinear(color.G / 255d);
        double sourceBlue = SrgbToLinear(color.B / 255d);

        for (int y = startY; y < endY; y++)
        {
            int rowOffset = y * pixmap.RowBytes;
            for (int x = startX; x < endX; x++)
            {
                BlendGammaCorrectedPixel(
                    pixels,
                    rowOffset + (x * 4),
                    sourceRed,
                    sourceGreen,
                    sourceBlue,
                    sourceAlpha);
            }
        }

        return true;
    }

    private static void BlendGammaCorrectedPixel(
        Span<byte> pixels,
        int offset,
        double sourceRed,
        double sourceGreen,
        double sourceBlue,
        double sourceAlpha)
    {
        double destinationAlpha = pixels[offset + 3] / 255d;
        double outputAlpha = sourceAlpha + (destinationAlpha * (1d - sourceAlpha));
        if (outputAlpha <= 0d)
        {
            pixels[offset] = 0;
            pixels[offset + 1] = 0;
            pixels[offset + 2] = 0;
            pixels[offset + 3] = 0;
            return;
        }

        double destinationBlue = Unpremultiply(pixels[offset], destinationAlpha);
        double destinationGreen = Unpremultiply(pixels[offset + 1], destinationAlpha);
        double destinationRed = Unpremultiply(pixels[offset + 2], destinationAlpha);

        pixels[offset] = PremultiplyToByte(BlendLinear(sourceBlue, destinationBlue, sourceAlpha, destinationAlpha, outputAlpha), outputAlpha);
        pixels[offset + 1] = PremultiplyToByte(BlendLinear(sourceGreen, destinationGreen, sourceAlpha, destinationAlpha, outputAlpha), outputAlpha);
        pixels[offset + 2] = PremultiplyToByte(BlendLinear(sourceRed, destinationRed, sourceAlpha, destinationAlpha, outputAlpha), outputAlpha);
        pixels[offset + 3] = ToByte(outputAlpha);
    }

    private static double BlendLinear(double source, double destination, double sourceAlpha, double destinationAlpha, double outputAlpha)
    {
        double destinationLinear = SrgbToLinear(destination);
        return ((source * sourceAlpha) + (destinationLinear * destinationAlpha * (1d - sourceAlpha))) / outputAlpha;
    }

    private static double Unpremultiply(byte channel, double alpha)
        => alpha <= 0d ? 0d : Math.Clamp((channel / 255d) / alpha, 0d, 1d);

    private static byte PremultiplyToByte(double linearChannel, double alpha)
        => ToByte(LinearToSrgb(linearChannel) * alpha);

    private static byte ToByte(double value)
        => (byte)Math.Clamp((int)Math.Round(value * 255d), 0, 255);

    private static double SrgbToLinear(double value)
        => value <= 0.04045d
            ? value / 12.92d
            : Math.Pow((value + 0.055d) / 1.055d, 2.4d);

    private static double LinearToSrgb(double value)
        => value <= 0.0031308d
            ? value * 12.92d
            : (1.055d * Math.Pow(Math.Clamp(value, 0d, 1d), 1d / 2.4d)) - 0.055d;
}

internal sealed class SkiaDrawingState : IDrawingState
{
    private readonly SkiaDrawingContext _context;
    private readonly SKCanvas _canvas;
    private readonly int _count;
    private readonly SmoothingMode _smoothingMode;
    private readonly TextRenderingHint _textRenderingHint;
    private readonly InterpolationMode _interpolationMode;
    private readonly CompositingQuality _compositingQuality;
    private readonly CompositingMode _compositingMode;
    private readonly PixelOffsetMode _pixelOffsetMode;

    public SkiaDrawingState(SkiaDrawingContext context, SKCanvas canvas)
    {
        _context = context;
        _canvas = canvas;
        _count = canvas.Save();
        _smoothingMode = context.SmoothingMode;
        _textRenderingHint = context.TextRenderingHint;
        _interpolationMode = context.InterpolationMode;
        _compositingQuality = context.CompositingQuality;
        _compositingMode = context.CompositingMode;
        _pixelOffsetMode = context.PixelOffsetMode;
    }

    public void Dispose()
    {
        _canvas.RestoreToCount(_count);
        _context.SmoothingMode = _smoothingMode;
        _context.TextRenderingHint = _textRenderingHint;
        _context.InterpolationMode = _interpolationMode;
        _context.CompositingQuality = _compositingQuality;
        _context.CompositingMode = _compositingMode;
        _context.PixelOffsetMode = _pixelOffsetMode;
    }
}
