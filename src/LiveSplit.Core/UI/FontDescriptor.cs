using System;
using System.Drawing;

namespace LiveSplit.UI;

/// <summary>
/// Cross-platform replacement for the <see cref="System.Drawing.Font"/> field type that used to
/// be stored on <c>LayoutSettings</c>, <c>FontOverrides</c>, and per-component settings.
/// <see cref="System.Drawing.Font"/>'s constructor and <c>Clone</c> throw
/// <see cref="PlatformNotSupportedException"/> on non-Windows .NET 8, so we hold the values the
/// renderer (Skia) actually consumes (family/size/style/unit) and the renderer materializes a
/// real font handle on demand via <see cref="LiveSplit.UI.Drawing.IDrawingFactory.CreateFont"/>.
/// </summary>
public sealed class FontDescriptor : ICloneable
{
    public string FamilyName { get; set; } = "Arial";
    public float Size { get; set; } = 12f;
    public FontStyle Style { get; set; } = FontStyle.Regular;
    public GraphicsUnit Unit { get; set; } = GraphicsUnit.Point;

    public FontDescriptor() { }

    public FontDescriptor(string familyName, float size, FontStyle style = FontStyle.Regular, GraphicsUnit unit = GraphicsUnit.Point)
    {
        FamilyName = familyName;
        Size = size;
        Style = style;
        Unit = unit;
    }

    /// <summary><see cref="System.Drawing.Font"/> exposes the family as <c>Name</c>; this alias
    /// keeps call sites that read <c>.Name</c> compiling after the field-type swap.</summary>
    public string Name => FamilyName;

    public FontDescriptor Clone() => new(FamilyName, Size, Style, Unit);

    object ICloneable.Clone() => Clone();

    public override string ToString() => $"{FamilyName}, {Size}{Unit switch
    {
        GraphicsUnit.Point => "pt",
        GraphicsUnit.Pixel => "px",
        _ => "",
    }}, {Style}";

    public override bool Equals(object obj)
        => obj is FontDescriptor o
            && o.FamilyName == FamilyName
            && o.Size == Size
            && o.Style == Style
            && o.Unit == Unit;

    public override int GetHashCode()
        => HashCode.Combine(FamilyName, Size, (int)Style, (int)Unit);
}
