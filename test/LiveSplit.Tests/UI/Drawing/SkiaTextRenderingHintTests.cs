using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

using LiveSplit.UI.Drawing.Skia;

using SkiaSharp;

using Xunit;

namespace LiveSplit.Tests.UI.Drawing;

public class SkiaTextRenderingHintTests
{
    [Fact]
    public void ReadsGdiCellMetricsFromOpenTypeOs2WinAscentDescent()
    {
        byte[] os2 = new byte[72];
        os2[68] = 0x07;
        os2[69] = 0xE8;
        os2[70] = 0x01;
        os2[71] = 0xC2;

        bool parsed = SkiaFont.TryReadGdiCellMetrics(os2, unitsPerEm: 2048, pixelSize: 43.75f, out float ascent, out float descent);

        Assert.True(parsed);
        Assert.Equal(43.23730f, ascent, precision: 4);
        Assert.Equal(9.61304f, descent, precision: 4);
    }

    [Theory]
    [InlineData(TextRenderingHint.AntiAlias, SKFontEdging.Antialias, SKFontHinting.None, false)]
    [InlineData(TextRenderingHint.ClearTypeGridFit, SKFontEdging.SubpixelAntialias, SKFontHinting.Full, true)]
    public void MapsGdiTextRenderingHintsToSkiaFontSettings(
        TextRenderingHint hint,
        SKFontEdging expectedEdging,
        SKFontHinting expectedHinting,
        bool expectedSubpixel)
    {
        using var source = new SkiaFont("Arial", 16, FontStyle.Regular, GraphicsUnit.Pixel);
        using SKFont renderFont = SkiaDrawingContext.CreateRenderFont(source, hint);

        Assert.Equal(expectedEdging, renderFont.Edging);
        Assert.Equal(expectedHinting, renderFont.Hinting);
        Assert.Equal(expectedSubpixel, renderFont.Subpixel);
    }

    [Fact]
    public void SaveRestoresGdiQualitySettings()
    {
        using SKSurface surface = SKSurface.Create(new SKImageInfo(16, 16));
        var context = new SkiaDrawingContext(surface.Canvas)
        {
            SmoothingMode = SmoothingMode.AntiAlias,
            TextRenderingHint = TextRenderingHint.AntiAlias,
            InterpolationMode = InterpolationMode.Bilinear,
            CompositingQuality = CompositingQuality.GammaCorrected,
            CompositingMode = CompositingMode.SourceOver,
            PixelOffsetMode = PixelOffsetMode.Half,
        };

        using (context.Save())
        {
            context.SmoothingMode = SmoothingMode.Default;
            context.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            context.InterpolationMode = InterpolationMode.NearestNeighbor;
            context.CompositingQuality = CompositingQuality.HighSpeed;
            context.CompositingMode = CompositingMode.SourceCopy;
            context.PixelOffsetMode = PixelOffsetMode.None;
        }

        Assert.Equal(SmoothingMode.AntiAlias, context.SmoothingMode);
        Assert.Equal(TextRenderingHint.AntiAlias, context.TextRenderingHint);
        Assert.Equal(InterpolationMode.Bilinear, context.InterpolationMode);
        Assert.Equal(CompositingQuality.GammaCorrected, context.CompositingQuality);
        Assert.Equal(CompositingMode.SourceOver, context.CompositingMode);
        Assert.Equal(PixelOffsetMode.Half, context.PixelOffsetMode);
    }
}
