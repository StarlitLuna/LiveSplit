using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

using LiveSplit.UI;
using LiveSplit.UI.Drawing;
using LiveSplit.UI.Drawing.Skia;

using SkiaSharp;

using Xunit;

namespace LiveSplit.Tests.UI.Drawing;

[Collection("DrawingApi")]
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

    [Fact]
    public void SimpleLabelWithoutEffectsMatchesSingleDrawString()
    {
        DrawingApi.Register(new SkiaDrawingFactory());
        var bounds = new RectangleF(4f, 4f, 160f, 60f);
        var font = new FontDescriptor("Arial", 32f, FontStyle.Bold, GraphicsUnit.Pixel);
        Color color = Color.FromArgb(172, 172, 172);

        using SKBitmap direct = RenderText(ctx =>
        {
            using IFont drawFont = DrawingApi.Factory.CreateFont(font.FamilyName, font.Size, font.Style, font.Unit);
            using IBrush brush = DrawingApi.Factory.CreateSolidBrush(color);
            ITextFormat format = CreateLabelTextFormat();
            ctx.DrawString("0.00", drawFont, brush, bounds, format);
        });

        using SKBitmap label = RenderText(ctx =>
        {
            using IBrush brush = DrawingApi.Factory.CreateSolidBrush(color);
            var simpleLabel = new SimpleLabel("0.00", bounds.X, bounds.Y, font, bounds.Width, bounds.Height)
            {
                HasShadow = false,
                OutlineColor = Color.Transparent,
                Brush = brush,
            };
            simpleLabel.Draw(ctx);
        });

        AssertSamePixels(direct, label);
    }

    [Fact]
    public void DefaultTextFontMeasureStringHeightMatchesGdiOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var font = new FontDescriptor("Segoe UI", 16f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var bitmap = new Bitmap(200, 80);
        using Graphics graphics = Graphics.FromImage(bitmap);
        using var gdiFont = new Font(font.FamilyName, font.Size, font.Style, font.Unit);
        using var format = new StringFormat
        {
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter,
        };
        SizeF gdiSize = graphics.MeasureString("A", gdiFont, 9999, format);

        DrawingApi.Register(new SkiaDrawingFactory());
        using SKSurface surface = SKSurface.Create(new SKImageInfo(200, 80));
        var context = new SkiaDrawingContext(surface.Canvas);
        using IFont skiaFont = DrawingApi.Factory.CreateFont(font.FamilyName, font.Size, font.Style, font.Unit);
        ITextFormat skiaFormat = DrawingApi.Factory.CreateTextFormat();
        skiaFormat.FormatFlags = StringFormatFlags.NoWrap;
        skiaFormat.Trimming = StringTrimming.EllipsisCharacter;
        SizeF skiaSize = context.MeasureString("A", skiaFont, 9999, skiaFormat);

        Assert.InRange(skiaSize.Height, gdiSize.Height - 0.25f, gdiSize.Height + 0.25f);
    }

    private static SKBitmap RenderText(Action<IDrawingContext> draw)
    {
        using SKSurface surface = SKSurface.Create(new SKImageInfo(180, 80, SKColorType.Bgra8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Black);
        var context = new SkiaDrawingContext(surface.Canvas)
        {
            SmoothingMode = SmoothingMode.AntiAlias,
            TextRenderingHint = TextRenderingHint.AntiAlias,
            InterpolationMode = InterpolationMode.Bilinear,
            CompositingQuality = CompositingQuality.GammaCorrected,
            CompositingMode = CompositingMode.SourceOver,
        };

        draw(context);
        using SKImage image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    private static ITextFormat CreateLabelTextFormat()
    {
        ITextFormat format = DrawingApi.Factory.CreateTextFormat();
        format.FormatFlags = StringFormatFlags.NoWrap;
        format.Trimming = StringTrimming.EllipsisCharacter;
        return format;
    }

    private static void AssertSamePixels(SKBitmap expected, SKBitmap actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        for (int y = 0; y < expected.Height; y++)
        {
            for (int x = 0; x < expected.Width; x++)
            {
                Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y));
            }
        }
    }
}
