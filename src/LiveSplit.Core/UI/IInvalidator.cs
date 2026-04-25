using System.Numerics;

namespace LiveSplit.UI;

public interface IInvalidator
{
    /// <summary>
    /// Current coordinate-space transform applied by the layout renderer. Components don't
    /// touch this directly; <see cref="LiveSplit.UI.Components.ComponentRenderer"/> shifts it
    /// via <c>Matrix3x2.CreateTranslation</c> as it advances the cursor between components, and
    /// the implementation uses it to map invalidated rectangles into the host's coordinate
    /// system.
    /// </summary>
    Matrix3x2 Transform { get; set; }

    void Invalidate(float x, float y, float width, float height);
}
