using System;
using System.Drawing;
using System.IO;

using LiveSplit.UI.Drawing;

using SkiaSharp;

namespace LiveSplit.UI;

/// <summary>
/// Generates a soft drop-shadow image from an icon. The implementation reads/writes raw PNG
/// bytes via SkiaSharp so it works on every backing (no System.Drawing.Bitmap dependency,
/// no libgdiplus on Linux). The result is loaded back through <see cref="DrawingApi.Factory"/>
/// so callers can draw it via <see cref="IDrawingContext.DrawImage(IImage, RectangleF)"/>.
/// </summary>
public static class IconShadow
{
    public static readonly float[] Kernel = [0.398942f, 0.241971f, 0.053991f, 0.00443185f];

    private const int ScaledSize = 24;
    private const int Padding = 3;
    private const int OutputSize = ScaledSize + (2 * Padding);
    private const float ShadowStrength = 0.8f;

    /// <summary>
    /// Build a shadow PNG from the given icon PNG bytes and shadow color.
    /// Returns null when <paramref name="iconPng"/> is null/empty or cannot be decoded.
    /// </summary>
    public static byte[] GeneratePng(byte[] iconPng, Color shadowColor)
    {
        if (iconPng is not { Length: > 0 })
        {
            return null;
        }

        using SKBitmap source = SKBitmap.Decode(iconPng);
        if (source == null)
        {
            return null;
        }

        // Downsample to a fixed size so the kernel produces a consistent shadow regardless of
        // the source icon resolution. Uses High quality Lanczos-style resampling.
        var scaledInfo = new SKImageInfo(ScaledSize, ScaledSize, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var scaled = new SKBitmap(scaledInfo);
        source.ScalePixels(scaled, SKFilterQuality.High);

        byte[] sourceAlpha = ExtractAlpha(scaled, ScaledSize, ScaledSize);

        byte red = shadowColor.R;
        byte green = shadowColor.G;
        byte blue = shadowColor.B;
        double alphaPeak = 255f * (Math.Pow((shadowColor.A / 255f) - 1f, 3) + 1f);

        var temp = new float[OutputSize * OutputSize];
        var result = new float[OutputSize * OutputSize];

        // Horizontal pass: read padded source alpha, write into temp.
        for (int x = 0; x < OutputSize; x++)
        {
            for (int y = 0; y < OutputSize; y++)
            {
                float sum = Kernel[0] * SampleSourceAlpha(sourceAlpha, x, y);
                for (int i = 1; i < Kernel.Length; i++)
                {
                    float weight = Kernel[i];
                    sum += weight * (SampleSourceAlpha(sourceAlpha, x - i, y) + SampleSourceAlpha(sourceAlpha, x + i, y));
                }

                temp[(y * OutputSize) + x] = sum;
            }
        }

        // Vertical pass: blur temp, write into result.
        for (int x = 0; x < OutputSize; x++)
        {
            for (int y = 0; y < OutputSize; y++)
            {
                float sum = Kernel[0] * SampleArray(temp, x, y);
                for (int i = 1; i < Kernel.Length; i++)
                {
                    float weight = Kernel[i];
                    sum += weight * (SampleArray(temp, x, y - i) + SampleArray(temp, x, y + i));
                }

                result[(y * OutputSize) + x] = sum;
            }
        }

        var outputInfo = new SKImageInfo(OutputSize, OutputSize, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var output = new SKBitmap(outputInfo);
        WriteShadowPixels(output, result, alphaPeak, red, green, blue);

        using SKImage image = SKImage.FromBitmap(output);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// Convenience overload that decodes the resulting PNG into an <see cref="IImage"/> via
    /// the active drawing factory.
    /// </summary>
    public static IImage GenerateImage(byte[] iconPng, Color shadowColor)
    {
        byte[] png = GeneratePng(iconPng, shadowColor);
        if (png == null)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(png, writable: false);
            return DrawingApi.Factory.LoadImage(stream);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] ExtractAlpha(SKBitmap bitmap, int width, int height)
    {
        var alpha = new byte[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                alpha[(y * width) + x] = bitmap.GetPixel(x, y).Alpha;
            }
        }

        return alpha;
    }

    private static float SampleSourceAlpha(byte[] alpha, int x, int y)
    {
        x -= Padding;
        y -= Padding;
        if (x < 0 || y < 0 || x >= ScaledSize || y >= ScaledSize)
        {
            return 0f;
        }

        return alpha[(y * ScaledSize) + x] / 255f;
    }

    private static float SampleArray(float[] values, int x, int y)
    {
        if (x < 0 || y < 0 || x >= OutputSize || y >= OutputSize)
        {
            return 0f;
        }

        return values[(y * OutputSize) + x];
    }

    private static void WriteShadowPixels(SKBitmap bitmap, float[] values, double alphaPeak, byte r, byte g, byte b)
    {
        for (int y = 0; y < OutputSize; y++)
        {
            for (int x = 0; x < OutputSize; x++)
            {
                int rawAlpha = (int)((ShadowStrength * alphaPeak * values[(y * OutputSize) + x]) + 0.5f);
                if (rawAlpha < 0)
                {
                    rawAlpha = 0;
                }
                else if (rawAlpha > 255)
                {
                    rawAlpha = 255;
                }

                bitmap.SetPixel(x, y, new SKColor(r, g, b, (byte)rawAlpha));
            }
        }
    }
}
