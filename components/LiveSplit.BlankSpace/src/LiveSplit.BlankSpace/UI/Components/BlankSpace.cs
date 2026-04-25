using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

using LiveSplit.Model;
using LiveSplit.UI.Drawing;

namespace LiveSplit.UI.Components;

public class BlankSpace : IComponent
{
    public BlankSpaceSettings Settings { get; set; }

    public string ComponentName => "Blank Space";

    public float VerticalHeight => Settings.SpaceHeight;

    public float MinimumWidth => 20;

    public float HorizontalWidth => Settings.SpaceWidth;

    public float MinimumHeight => 20;

    public float PaddingTop => 0f;
    public float PaddingLeft => 0f;
    public float PaddingBottom => 0f;
    public float PaddingRight => 0f;

    public IDictionary<string, Action> ContextMenuControls => null;

    public BlankSpace()
    {
        Settings = new BlankSpaceSettings();
    }

    public static void DrawBackground(IDrawingContext ctx, Color settingsColor1, Color settingsColor2,
        float width, float height, GradientType gradientType)
    {
        if (settingsColor1.A > 0
        || (gradientType != GradientType.Plain
        && settingsColor2.A > 0))
        {
            if (gradientType == GradientType.Plain)
            {
                using ISolidBrush brush = DrawingApi.Factory.CreateSolidBrush(settingsColor1);
                ctx.FillRectangle(brush, 0, 0, width, height);
            }
            else
            {
                var endPoint = gradientType == GradientType.Horizontal
                    ? new PointF(width, 0)
                    : new PointF(0, height);
                using ILinearGradientBrush brush = DrawingApi.Factory.CreateLinearGradientBrush(
                    new PointF(0, 0), endPoint, settingsColor1, settingsColor2);
                ctx.FillRectangle(brush, 0, 0, width, height);
            }
        }
    }

    private void DrawGeneral(IDrawingContext ctx, LiveSplitState state, float width, float height)
    {
        DrawBackground(ctx, Settings.BackgroundColor, Settings.BackgroundColor2, width, height, Settings.BackgroundGradient);
    }

    public void DrawVertical(IDrawingContext ctx, LiveSplitState state, float width, Region clipRegion)
    {
        DrawGeneral(ctx, state, width, VerticalHeight);
    }

    public void DrawHorizontal(IDrawingContext ctx, LiveSplitState state, float height, Region clipRegion)
    {
        DrawGeneral(ctx, state, HorizontalWidth, height);
    }

    public Control GetSettingsControl(LayoutMode mode)
    {
        Settings.Mode = mode;
        return Settings;
    }

    public Avalonia.Controls.Control GetSettingsControlAvalonia(LayoutMode mode)
    {
        Settings.Mode = mode;
        return LiveSplit.UI.AvaloniaSettingsBuilder.Build(Settings, "Blank Space");
    }

    public void SetSettings(System.Xml.XmlNode settings)
    {
        Settings.SetSettings(settings);
    }

    public System.Xml.XmlNode GetSettings(System.Xml.XmlDocument document)
    {
        return Settings.GetSettings(document);
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
    }

    public void Dispose()
    {
    }

    public int GetSettingsHashCode()
    {
        return Settings.GetSettingsHashCode();
    }
}
