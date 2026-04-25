using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace LiveSplit.UI.Drawing;

/// <summary>
/// Encodes a <see cref="System.Drawing.Image"/> to PNG and reloads it through the active
/// <see cref="IDrawingFactory"/>, producing an <see cref="IImage"/> that any
/// <see cref="IDrawingContext"/> backend can draw. Intended for one-shot conversion at
/// icon-change time; PNG is used so transparent borders round-trip cleanly.
/// </summary>
public static class ImageBridge
{
    public static IImage ToIImage(this Image src)
    {
        if (src is null)
        {
            return null;
        }

        using var ms = new MemoryStream();
        src.Save(ms, ImageFormat.Png);
        ms.Position = 0;
        return DrawingApi.Factory.LoadImage(ms);
    }
}
