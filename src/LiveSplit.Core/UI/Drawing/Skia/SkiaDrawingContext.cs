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
    private readonly int _baseSaveCount;

    public SkiaDrawingContext(SKCanvas canvas, float dpiX = 96f, float dpiY = 96f)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
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
        using SKPaint paint = CreateFillPaint(brush);
        _canvas.DrawRect(ToSk(rect), paint);
    }

    public void FillRectangle(IBrush brush, float x, float y, float width, float height)
    {
        using SKPaint paint = CreateFillPaint(brush);
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
        _canvas.DrawImage(skImage, ToSk(destRect));
    }

    public void DrawImage(IImage image, Rectangle destRect, Rectangle srcRect)
    {
        var skImage = ((SkiaImage)image).SkImage;
        _canvas.DrawImage(skImage, ToSk(srcRect), ToSk(destRect));
    }

    public void DrawImageWithOpacity(IImage image, Rectangle destRect, Rectangle srcRect, float opacity)
    {
        var skImage = ((SkiaImage)image).SkImage;
        using var paint = new SKPaint
        {
            Color = new SKColor(255, 255, 255, (byte)(Math.Clamp(opacity, 0f, 1f) * 255)),
            IsAntialias = IsAntialias,
        };
        _canvas.DrawImage(skImage, ToSk(srcRect), ToSk(destRect), paint);
    }

    public void DrawString(string text, IFont font, IBrush brush, RectangleF bounds, ITextFormat format)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var skFont = (SkiaFont)font;
        using SKPaint paint = CreateFillPaint(brush);

        var skFormat = (SkiaTextFormat)format;
        SKFontMetrics metrics = skFont.Font.Metrics;

        // Honor StringTrimming.EllipsisCharacter: GDI+ trims automatically when the string
        // overflows the layout rect; Skia needs an explicit measure + truncate. Other trimming
        // modes are not currently used by any component, so they fall through to plain draw.
        if (skFormat.Trimming == StringTrimming.EllipsisCharacter && bounds.Width > 0)
        {
            text = TrimWithEllipsis(text, skFont, bounds.Width);
        }

        float textWidth = MeasureText(skFont.Font, text);
        float textHeight = metrics.Descent - metrics.Ascent;

        float x = skFormat.Alignment switch
        {
            StringAlignment.Center => bounds.X + ((bounds.Width - textWidth) / 2f),
            StringAlignment.Far => bounds.X + bounds.Width - textWidth,
            _ => bounds.X,
        };

        // Skia's DrawText y-coordinate is the baseline, not the top, so offset by -ascent.
        float baselineY = skFormat.LineAlignment switch
        {
            StringAlignment.Center => bounds.Y + ((bounds.Height - textHeight) / 2f) - metrics.Ascent,
            StringAlignment.Far => bounds.Y + bounds.Height - metrics.Descent,
            _ => bounds.Y - metrics.Ascent,
        };

        _canvas.DrawText(text, x, baselineY, skFont.Font, paint);
    }

    public SizeF MeasureString(string text, IFont font, int maxWidth, ITextFormat format)
    {
        if (string.IsNullOrEmpty(text))
        {
            return SizeF.Empty;
        }

        var skFont = (SkiaFont)font;
        float width = MeasureText(skFont.Font, text);
        SKFontMetrics metrics = skFont.Font.Metrics;
        float height = metrics.Descent - metrics.Ascent;
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
    private static string TrimWithEllipsis(string text, SkiaFont skFont, float maxWidth)
    {
        const string Ellipsis = "…";
        if (MeasureText(skFont.Font, text) <= maxWidth)
        {
            return text;
        }

        float ellipsisWidth = MeasureText(skFont.Font, Ellipsis);
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
            float width = MeasureText(skFont.Font, text.AsSpan(0, mid)) + ellipsisWidth;
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

    public IDrawingState Save() => new SkiaDrawingState(_canvas);

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
}

internal sealed class SkiaDrawingState : IDrawingState
{
    private readonly SKCanvas _canvas;
    private readonly int _count;

    public SkiaDrawingState(SKCanvas canvas)
    {
        _canvas = canvas;
        _count = canvas.Save();
    }

    public void Dispose() => _canvas.RestoreToCount(_count);
}
