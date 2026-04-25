using System.Drawing;
using System.IO;

using LiveSplit.UI;
using LiveSplit.UI.Drawing;
using LiveSplit.UI.Drawing.Skia;

using SkiaSharp;

using Xunit;

namespace LiveSplit.Tests.UI.Drawing;

/// <summary>
/// Pins the PNG-bytes-to-IImage pipeline that the live render path now uses for game and split
/// icons. Without this the components fall back to silent black boxes the moment Skia changes
/// behavior or the model stops carrying raw bytes.
/// </summary>
public class IconRoundTripTests
{
    public IconRoundTripTests()
    {
        // Tests run in any order; ensure a Skia factory is active so DrawingApi.Factory.LoadImage
        // resolves regardless of whether AvaloniaProgram has booted.
        DrawingApi.Register(new SkiaDrawingFactory());
    }

    private static byte[] MakeRedSquarePng(int size)
    {
        var info = new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Red);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [Fact]
    public void LoadImage_RoundsTripDimensions()
    {
        byte[] png = MakeRedSquarePng(32);
        using var stream = new MemoryStream(png);

        IImage image = DrawingApi.Factory.LoadImage(stream);

        Assert.NotNull(image);
        Assert.Equal(32, image.Width);
        Assert.Equal(32, image.Height);
    }

    [Fact]
    public void IconShadow_GeneratePng_ProducesSamePixelDimsAsKernel()
    {
        byte[] iconPng = MakeRedSquarePng(48);

        byte[] shadowPng = IconShadow.GeneratePng(iconPng, Color.Black);

        Assert.NotNull(shadowPng);
        using var stream = new MemoryStream(shadowPng);
        IImage shadow = DrawingApi.Factory.LoadImage(stream);
        Assert.NotNull(shadow);
        // Shadow uses ScaledSize (24) + 2*Padding (3) = 30 px on a side regardless of source.
        Assert.Equal(30, shadow.Width);
        Assert.Equal(30, shadow.Height);
    }

    [Fact]
    public void IconShadow_GeneratePng_ReturnsNullForEmptyBytes()
    {
        Assert.Null(IconShadow.GeneratePng(null, Color.Black));
        Assert.Null(IconShadow.GeneratePng([], Color.Black));
    }
}
