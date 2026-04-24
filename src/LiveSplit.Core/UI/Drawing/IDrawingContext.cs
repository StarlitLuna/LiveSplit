using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Numerics;

namespace LiveSplit.UI.Drawing;

/// <summary>
/// Abstraction over the per-frame paint surface used by LiveSplit's rendering code. Mirrors
/// the subset of <see cref="System.Drawing.Graphics"/> that components and the layout renderer
/// actually consume. Today the only implementation is GDI+ backed; Phase 5 replaces the backend
/// with SkiaSharp to run on Linux.
/// </summary>
public interface IDrawingContext
{
    // --- Drawing primitives ---
    void FillRectangle(IBrush brush, RectangleF rect);
    void FillRectangle(IBrush brush, float x, float y, float width, float height);
    void DrawRectangle(IPen pen, RectangleF rect);
    void DrawLine(IPen pen, PointF p1, PointF p2);

    void DrawImage(IImage image, RectangleF destRect);
    void DrawImage(IImage image, Rectangle destRect, Rectangle srcRect);
    /// <summary>Draw a sub-rectangle of <paramref name="image"/> with a uniform opacity in [0, 1].</summary>
    void DrawImageWithOpacity(IImage image, Rectangle destRect, Rectangle srcRect, float opacity);

    void DrawString(string text, IFont font, IBrush brush, RectangleF bounds, ITextFormat format);
    SizeF MeasureString(string text, IFont font, int maxWidth, ITextFormat format);

    void FillPath(IBrush brush, IGraphicsPath path);
    void DrawPath(IPen pen, IGraphicsPath path);

    // --- Transform ---
    void TranslateTransform(float dx, float dy);
    void ScaleTransform(float sx, float sy);
    void ResetTransform();

    /// <summary>
    /// Current transform as a 2D affine matrix. <c>M11</c> and <c>M22</c> carry the scale on
    /// X/Y, <c>M31</c> and <c>M32</c> the translation. Used by the layout renderer to compute
    /// per-component bounds for culling without reaching through to the backend's Graphics/
    /// SKCanvas directly.
    /// </summary>
    Matrix3x2 GetTransform();

    // --- Clip ---
    /// <summary>Set the clip region to the whole surface (no clipping).</summary>
    void ClearClip();
    void SetClip(RectangleF rect);
    void IntersectClip(RectangleF rect);
    bool IsVisible(RectangleF rect);

    // --- State save/restore ---
    /// <summary>
    /// Snapshot the current transform, clip, and quality settings. Disposing the returned state
    /// restores the snapshot. Intended for the <c>using var state = ctx.Save();</c> pattern.
    /// </summary>
    IDrawingState Save();

    // --- Quality settings ---
    SmoothingMode SmoothingMode { get; set; }
    TextRenderingHint TextRenderingHint { get; set; }
    InterpolationMode InterpolationMode { get; set; }
    CompositingQuality CompositingQuality { get; set; }
    CompositingMode CompositingMode { get; set; }
    PixelOffsetMode PixelOffsetMode { get; set; }

    // --- Measurement ---
    float DpiX { get; }
    float DpiY { get; }
}

/// <summary>Opaque handle returned by <see cref="IDrawingContext.Save"/>. Dispose to restore.</summary>
public interface IDrawingState : IDisposable { }

// --- Resource types (all IDisposable because the GDI+ backing holds native handles) ---

public interface IBrush : IDisposable { }

public interface ISolidBrush : IBrush
{
    Color Color { get; set; }
}

public interface ILinearGradientBrush : IBrush { }

public interface IPen : IDisposable
{
    Color Color { get; }
    float Width { get; }
    LineJoin LineJoin { get; set; }
}

public interface IFont : IDisposable
{
    string FamilyName { get; }
    float Size { get; }
    FontStyle Style { get; }
    GraphicsUnit Unit { get; }
}

public interface IImage : IDisposable
{
    int Width { get; }
    int Height { get; }
}

public interface IGraphicsPath : IDisposable
{
    void Reset();
    void AddString(string text, IFont font, RectangleF layoutRect, ITextFormat format);
}

public interface ITextFormat
{
    StringAlignment Alignment { get; set; }
    StringAlignment LineAlignment { get; set; }
    StringFormatFlags FormatFlags { get; set; }
    StringTrimming Trimming { get; set; }
}
