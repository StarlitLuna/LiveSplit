using System.Drawing;

using LiveSplit.UI.Drawing.Skia;

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
}
