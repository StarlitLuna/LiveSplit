using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.Versioning;

namespace LiveSplit.UI.Drawing.GdiPlus;

/// <summary>
/// <see cref="IDrawingContext"/> implementation that forwards every call to a
/// <see cref="System.Drawing.Graphics"/> owned by the WinForms paint loop.
/// Does not own the Graphics (it comes from PaintEventArgs); disposing this context is a no-op.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GdiPlusDrawingContext : IDrawingContext
{
    private readonly Graphics _g;

    public GdiPlusDrawingContext(Graphics graphics)
    {
        _g = graphics ?? throw new ArgumentNullException(nameof(graphics));
    }

    /// <summary>
    /// Escape hatch: exposes the underlying Graphics for call sites that haven't been migrated to
    /// IDrawingContext yet. Once Phase 4b completes this should be unused.
    /// </summary>
    public Graphics UnwrapGraphics() => _g;

    // --- Drawing primitives ---

    public void FillRectangle(IBrush brush, RectangleF rect)
        => _g.FillRectangle(Unwrap(brush), rect);

    public void FillRectangle(IBrush brush, float x, float y, float width, float height)
        => _g.FillRectangle(Unwrap(brush), x, y, width, height);

    public void DrawRectangle(IPen pen, RectangleF rect)
        => _g.DrawRectangle(((GdiPlusPen)pen).Native, rect.X, rect.Y, rect.Width, rect.Height);

    public void DrawLine(IPen pen, PointF p1, PointF p2)
        => _g.DrawLine(((GdiPlusPen)pen).Native, p1, p2);

    public void DrawImage(IImage image, RectangleF destRect)
        => _g.DrawImage(((GdiPlusImage)image).Native, destRect);

    public void DrawImage(IImage image, Rectangle destRect, Rectangle srcRect)
        => _g.DrawImage(((GdiPlusImage)image).Native, destRect, srcRect, GraphicsUnit.Pixel);

    public void DrawImageWithOpacity(IImage image, Rectangle destRect, Rectangle srcRect, float opacity)
    {
        using var attrs = new ImageAttributes();
        var matrix = new ColorMatrix { Matrix33 = opacity };
        attrs.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        _g.DrawImage(((GdiPlusImage)image).Native, destRect,
            srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height, GraphicsUnit.Pixel, attrs);
    }

    public void DrawString(string text, IFont font, IBrush brush, RectangleF bounds, ITextFormat format)
        => _g.DrawString(text, ((GdiPlusFont)font).Native, Unwrap(brush), bounds, ((GdiPlusTextFormat)format).Native);

    public SizeF MeasureString(string text, IFont font, int maxWidth, ITextFormat format)
        => _g.MeasureString(text, ((GdiPlusFont)font).Native, maxWidth, ((GdiPlusTextFormat)format).Native);

    public void FillPath(IBrush brush, IGraphicsPath path)
        => _g.FillPath(Unwrap(brush), ((GdiPlusGraphicsPath)path).Native);

    public void DrawPath(IPen pen, IGraphicsPath path)
        => _g.DrawPath(((GdiPlusPen)pen).Native, ((GdiPlusGraphicsPath)path).Native);

    // --- Transform ---

    public void TranslateTransform(float dx, float dy) => _g.TranslateTransform(dx, dy);
    public void ScaleTransform(float sx, float sy) => _g.ScaleTransform(sx, sy);
    public void ResetTransform() => _g.ResetTransform();

    // --- Clip ---

    public void ClearClip() => _g.Clip = new Region();

    public void SetClip(RectangleF rect) => _g.SetClip(rect);

    public void IntersectClip(RectangleF rect) => _g.IntersectClip(rect);

    public bool IsVisible(RectangleF rect) => _g.IsVisible(rect);

    // --- State save/restore ---

    public IDrawingState Save() => new GdiPlusDrawingState(_g);

    // --- Quality settings ---

    public SmoothingMode SmoothingMode
    {
        get => _g.SmoothingMode;
        set => _g.SmoothingMode = value;
    }

    public TextRenderingHint TextRenderingHint
    {
        get => _g.TextRenderingHint;
        set => _g.TextRenderingHint = value;
    }

    public InterpolationMode InterpolationMode
    {
        get => _g.InterpolationMode;
        set => _g.InterpolationMode = value;
    }

    public CompositingQuality CompositingQuality
    {
        get => _g.CompositingQuality;
        set => _g.CompositingQuality = value;
    }

    public CompositingMode CompositingMode
    {
        get => _g.CompositingMode;
        set => _g.CompositingMode = value;
    }

    public PixelOffsetMode PixelOffsetMode
    {
        get => _g.PixelOffsetMode;
        set => _g.PixelOffsetMode = value;
    }

    public float DpiX => _g.DpiX;
    public float DpiY => _g.DpiY;

    // --- helpers ---

    private static Brush Unwrap(IBrush brush)
    {
        return brush switch
        {
            GdiPlusSolidBrush solid => solid.Native,
            GdiPlusLinearGradientBrush gradient => gradient.Native,
            _ => throw new ArgumentException(
                $"Unsupported IBrush implementation: {brush?.GetType().FullName ?? "null"}. " +
                $"GdiPlusDrawingContext expects brushes created by GdiPlusDrawingFactory.",
                nameof(brush)),
        };
    }
}

[SupportedOSPlatform("windows")]
internal sealed class GdiPlusDrawingState : IDrawingState
{
    private readonly Graphics _g;
    private readonly GraphicsState _state;

    public GdiPlusDrawingState(Graphics g)
    {
        _g = g;
        _state = g.Save();
    }

    public void Dispose() => _g.Restore(_state);
}
