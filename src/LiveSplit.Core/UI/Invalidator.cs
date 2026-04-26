using System;
using System.Numerics;

namespace LiveSplit.UI;

/// <summary>
/// Cross-platform replacement for the WinForms-bound invalidator that used to wrap a
/// <c>System.Windows.Forms.Form</c> and call <c>Form.Invalidate(rect)</c>. Coordinates
/// are tracked in <see cref="Matrix3x2"/> (cross-platform) and dirty rectangles are forwarded
/// to a host-supplied callback so the Avalonia surface can decide between coarse
/// <c>InvalidateVisual()</c> and granular invalidation.
/// </summary>
public class Invalidator : IInvalidator
{
    private readonly Action<float, float, float, float> _onInvalidate;

    private const double Offset = 0.535;

    public Matrix3x2 Transform { get; set; } = Matrix3x2.Identity;

    public Invalidator(Action<float, float, float, float> onInvalidate = null)
    {
        _onInvalidate = onInvalidate;
    }

    public void Restart()
    {
        Transform = Matrix3x2.Identity;
    }

    public void Invalidate(float x, float y, float width, float height)
    {
        var topLeft = Vector2.Transform(new Vector2(x, y), Transform);
        var bottomRight = Vector2.Transform(new Vector2(x + width, y + height), Transform);

        double offsetX = topLeft.X - Offset;
        double offsetY = topLeft.Y - Offset;
        float rectX = (float)Math.Ceiling(offsetX);
        float rectY = (float)Math.Ceiling(offsetY);
        float rectW = (float)Math.Ceiling(bottomRight.X - offsetX - Offset);
        float rectH = (float)Math.Ceiling(bottomRight.Y - offsetY - Offset);

        _onInvalidate?.Invoke(rectX, rectY, rectW, rectH);
    }
}
