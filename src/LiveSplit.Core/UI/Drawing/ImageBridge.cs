using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace LiveSplit.UI.Drawing;

/// <summary>
/// Converts a <see cref="System.Drawing.Image"/> (which the layout XML loader still produces
/// for run-icons / split-icons / game-icons) into an <see cref="IImage"/> that the active
/// <see cref="IDrawingContext"/> can draw. The conversion path is "encode to PNG in memory,
/// hand the bytes to the factory's LoadImage". It's not the fastest, but it's only invoked
/// once per icon-change (the call sites cache the result), and it lets us draw user-supplied
/// PNG/JPEG icons through the Skia backend without depending on System.Drawing's
/// Bitmap.LockBits + pixel-format gymnastics on Linux.
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
        // PNG round-trips alpha cleanly; that matters for split icons with transparent borders.
        src.Save(ms, ImageFormat.Png);
        ms.Position = 0;
        return DrawingApi.Factory.LoadImage(ms);
    }
}
