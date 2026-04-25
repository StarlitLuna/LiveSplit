using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using LiveSplit.Model;
using LiveSplit.UI.Drawing;

namespace LiveSplit.UI.Components;

public class ThinSeparatorComponent : IComponent
{
    public float PaddingTop => 0f;
    public float PaddingLeft => 0f;
    public float PaddingBottom => 0f;
    public float PaddingRight => 0f;

    public bool LockToBottom { get; set; }

    public GraphicsCache Cache { get; set; }

    protected LineComponent Line { get; set; }

    public float VerticalHeight => 1f;

    public float MinimumWidth => 0f;

    public float HorizontalWidth => 1f;

    public float MinimumHeight => 0f;

    public ThinSeparatorComponent()
    {
        Line = new LineComponent(1, Color.White);
        Cache = new GraphicsCache();
    }

    public void DrawVertical(IDrawingContext ctx, LiveSplitState state, float width, Region clipRegion)
    {
        // See GraphSeparatorComponent: the old `g.Clip = new Region();` clip-widen is dropped —
        // Skia can't express it, and in practice a 1-pixel separator fits inside the clip set
        // by ComponentRenderer. The Save()/Restore scope below captures SmoothingMode as well as
        // transform + clip (Graphics.Save includes it; the Skia state also tracks it).
        using IDrawingState state_ = ctx.Save();
        ctx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
        Line.LineColor = state.LayoutSettings.ThinSeparatorsColor;
        float scale = ctx.GetTransform().M11;
        float newHeight = Math.Max((int)((1f * scale) + 0.5f), 1) / scale;
        Line.VerticalHeight = newHeight;
        if (LockToBottom)
        {
            ctx.TranslateTransform(0, 1f - newHeight);
        }

        Line.DrawVertical(ctx, state, width, clipRegion);
    }

    public void DrawHorizontal(IDrawingContext ctx, LiveSplitState state, float height, Region clipRegion)
    {
        using IDrawingState state_ = ctx.Save();
        ctx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
        Line.LineColor = state.LayoutSettings.ThinSeparatorsColor;
        float scale = ctx.GetTransform().M11;
        float newWidth = Math.Max((int)((1f * scale) + 0.5f), 1) / scale;
        if (LockToBottom)
        {
            ctx.TranslateTransform(1f - newWidth, 0);
        }

        Line.HorizontalWidth = newWidth;
        Line.DrawHorizontal(ctx, state, height, clipRegion);
    }

    public string ComponentName
        => "Thin Separator";

    public Control GetSettingsControl(LayoutMode mode)
    {
        throw new NotImplementedException();
    }

    public void SetSettings(System.Xml.XmlNode settings)
    {
        throw new NotImplementedException();
    }

    public System.Xml.XmlNode GetSettings(System.Xml.XmlDocument document)
    {
        throw new NotImplementedException();
    }

    public string UpdateName => throw new NotSupportedException();

    public string XMLURL => throw new NotSupportedException();

    public string UpdateURL => throw new NotSupportedException();

    public Version Version => throw new NotSupportedException();

    public IDictionary<string, Action> ContextMenuControls
        => null;

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        Cache.Restart();
        Cache["LockToBottom"] = LockToBottom;

        if (invalidator != null && Cache.HasChanged)
        {
            invalidator.Invalidate(0, 0, width, height);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
