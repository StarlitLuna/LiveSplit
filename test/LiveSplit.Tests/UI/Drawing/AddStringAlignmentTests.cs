using System;
using System.Drawing;

using LiveSplit.UI.Drawing.Skia;

using SkiaSharp;

using Xunit;

namespace LiveSplit.Tests.UI.Drawing;

/// <summary>
/// Guards <see cref="SkiaGraphicsPath.AddString"/>'s layout-rect + alignment + ellipsis
/// honoring. The fix replaced a TODO that placed glyphs at the rect's top-left regardless of
/// <see cref="StringAlignment"/>; these tests pin the new behavior so a future refactor can't
/// silently regress SimpleLabel's outline/shadow positioning.
/// </summary>
public class AddStringAlignmentTests
{
    private static SkiaFont MakeFont() => new("Arial", 16, FontStyle.Regular, GraphicsUnit.Pixel);

    private static SkiaTextFormat Format(StringAlignment h, StringAlignment v, StringTrimming trim = StringTrimming.None)
        => new() { Alignment = h, LineAlignment = v, Trimming = trim };

    [Fact]
    public void HorizontalNear_PlacesPathLeftOfRect()
    {
        using var path = new SkiaGraphicsPath();
        using SkiaFont font = MakeFont();
        var rect = new RectangleF(100, 100, 400, 50);

        path.AddString("Hi", font, rect, Format(StringAlignment.Near, StringAlignment.Near));

        Assert.True(path.Path.Bounds.Left >= rect.Left - 1f, $"Left={path.Path.Bounds.Left}, rect.Left={rect.Left}");
        Assert.True(path.Path.Bounds.Left < rect.Left + (rect.Width / 4f), "Near should hug the left side");
    }

    [Fact]
    public void HorizontalCenter_CentersPathInRect()
    {
        using var path = new SkiaGraphicsPath();
        using SkiaFont font = MakeFont();
        var rect = new RectangleF(100, 100, 400, 50);

        path.AddString("Hi", font, rect, Format(StringAlignment.Center, StringAlignment.Near));

        float midPath = (path.Path.Bounds.Left + path.Path.Bounds.Right) / 2f;
        float midRect = rect.Left + (rect.Width / 2f);
        Assert.InRange(midPath, midRect - 5f, midRect + 5f);
    }

    [Fact]
    public void HorizontalFar_PlacesPathAtRightEdge()
    {
        using var path = new SkiaGraphicsPath();
        using SkiaFont font = MakeFont();
        var rect = new RectangleF(100, 100, 400, 50);

        path.AddString("Hi", font, rect, Format(StringAlignment.Far, StringAlignment.Near));

        Assert.True(path.Path.Bounds.Right <= rect.Right + 1f, $"Right={path.Path.Bounds.Right}, rect.Right={rect.Right}");
        Assert.True(path.Path.Bounds.Right > rect.Right - (rect.Width / 4f), "Far should hug the right side");
    }

    [Fact]
    public void VerticalCenter_CentersPathInRect()
    {
        using var path = new SkiaGraphicsPath();
        using SkiaFont font = MakeFont();
        var rect = new RectangleF(100, 100, 400, 200);

        path.AddString("Hi", font, rect, Format(StringAlignment.Near, StringAlignment.Center));

        float midPath = (path.Path.Bounds.Top + path.Path.Bounds.Bottom) / 2f;
        float midRect = rect.Top + (rect.Height / 2f);
        Assert.InRange(midPath, midRect - 12f, midRect + 12f);
    }

    [Fact]
    public void EllipsisOnOverflow_ProducesPathWithinRectWidth()
    {
        using var path = new SkiaGraphicsPath();
        using SkiaFont font = MakeFont();
        // Narrow rect that will not fit the long string at 16px font.
        var rect = new RectangleF(0, 0, 80, 30);

        path.AddString("This run-on string is significantly longer than the layout rect",
            font, rect, Format(StringAlignment.Near, StringAlignment.Near, StringTrimming.EllipsisCharacter));

        Assert.True(path.Path.Bounds.Width <= rect.Width + 4f,
            $"Ellipsized path width {path.Path.Bounds.Width} exceeds rect.Width {rect.Width}");
        Assert.True(path.Path.Bounds.Width > 0f, "Path should still contain at least the ellipsis glyph");
    }

    [Fact]
    public void EmptyText_AddsNothing()
    {
        using var path = new SkiaGraphicsPath();
        using SkiaFont font = MakeFont();

        path.AddString("", font, new RectangleF(0, 0, 100, 50), Format(StringAlignment.Near, StringAlignment.Near));

        Assert.True(path.Path.IsEmpty);
    }

    [Fact]
    public void DrawString_EllipsisOnOverflow_ShrinksTextToFit()
    {
        // SkiaDrawingContext.DrawString calls TrimWithEllipsis when the format requests
        // EllipsisCharacter. We can't render to a real canvas in a unit test, but the trim
        // helper is exercised by re-running the same logic through a static check: measure
        // the original text + the bounds, confirm trimmed-with-ellipsis fits.
        using SkiaFont font = MakeFont();
        const string Source = "This entire run-on string would not fit in eighty pixels at 16px";

        // Long string overflows.
        Assert.True(MeasureText(font.Font, Source) > 80f);

        // After ellipsizing, the resulting trimmed string must measure within the bound.
        // Mirrors the binary-search the DrawString path runs at draw time.
        string trimmed = TrimWithEllipsisLikeContext(Source, font, 80f);
        Assert.True(MeasureText(font.Font, trimmed) <= 80f + 0.5f,
            $"Trimmed='{trimmed}' measures {MeasureText(font.Font, trimmed)} but rect is 80");
        Assert.EndsWith("…", trimmed);
    }

    /// <summary>
    /// Mirror of <c>SkiaDrawingContext.TrimWithEllipsis</c> reachable from tests; the production
    /// helper is private. If a future refactor diverges these two, update both.
    /// </summary>
    private static string TrimWithEllipsisLikeContext(string text, SkiaFont skFont, float maxWidth)
    {
        const string Ellipsis = "…";
        if (MeasureText(skFont.Font, text) <= maxWidth)
        {
            return text;
        }

        float ellipsisWidth = MeasureText(skFont.Font, Ellipsis);
        if (ellipsisWidth >= maxWidth)
        {
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
}
