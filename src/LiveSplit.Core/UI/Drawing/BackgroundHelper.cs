using System.Drawing;

namespace LiveSplit.UI.Drawing;

/// <summary>
/// Shared component-background painter. Many components configure their background as a
/// <c>BackgroundColor / BackgroundColor2 / BackgroundGradient</c> triple; this helper paints
/// that triple into a <c>(0, 0, width, height)</c> rect through either drawing backend.
/// </summary>
public static class BackgroundHelper
{
    /// <summary>
    /// Fill the component rectangle <c>(0, 0, width, height)</c> with the configured background.
    /// No-op when both colors have alpha 0 (transparent background).
    /// </summary>
    public static void DrawBackground(IDrawingContext ctx, Color color1, Color color2,
        float width, float height, GradientType gradientType)
    {
        if (color1.A > 0 || (gradientType != GradientType.Plain && color2.A > 0))
        {
            if (gradientType == GradientType.Plain)
            {
                using ISolidBrush brush = DrawingApi.Factory.CreateSolidBrush(color1);
                ctx.FillRectangle(brush, 0, 0, width, height);
            }
            else
            {
                PointF endPoint = gradientType == GradientType.Horizontal
                    ? new PointF(width, 0)
                    : new PointF(0, height);
                using ILinearGradientBrush brush = DrawingApi.Factory.CreateLinearGradientBrush(
                    new PointF(0, 0), endPoint, color1, color2);
                ctx.FillRectangle(brush, 0, 0, width, height);
            }
        }
    }
}
