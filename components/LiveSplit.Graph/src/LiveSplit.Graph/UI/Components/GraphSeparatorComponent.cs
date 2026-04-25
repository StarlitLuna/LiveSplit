using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;

using LiveSplit.Model;
using LiveSplit.UI.Drawing;

namespace LiveSplit.UI.Components;

public class GraphSeparatorComponent : IComponent
{
    protected LineComponent Line { get; set; }
    protected GraphSettings Settings { get; set; }

    public bool LockToBottom { get; set; }

    public float PaddingTop => 0f;
    public float PaddingBottom => 0f;
    public float PaddingLeft => 0f;
    public float PaddingRight => 0f;

    public GraphicsCache Cache { get; set; }

    public float VerticalHeight => 1f;

    public float MinimumWidth => 0f;

    public IDictionary<string, Action> ContextMenuControls => null;

    public GraphSeparatorComponent(GraphSettings settings)
    {
        Line = new LineComponent(1, Color.White);
        Settings = settings;
        Cache = new GraphicsCache();
    }

    public void DrawVertical(IDrawingContext ctx, LiveSplitState state, float width, Region clipRegion)
    {
        // No explicit clip-widen: Skia can't express GDI+'s `g.Clip = new Region();` idiom,
        // and the 1-pixel separator already fits inside the parent component's bounds. Save()
        // captures SmoothingMode along with transform + clip on both backends.
        using IDrawingState state_ = ctx.Save();
        ctx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
        Line.LineColor = Settings.GraphLinesColor;
        float scale = ctx.GetTransform().M11;
        float newHeight = Math.Max((int)((1f * scale) + 0.5f), 1) / scale;
        Line.VerticalHeight = newHeight;
        if (LockToBottom)
        {
            ctx.TranslateTransform(0, 1f - newHeight);
        }

        Line.DrawVertical(ctx, state, width, clipRegion);
    }

    public string ComponentName => "Graph Separator";

    public Control GetSettingsControl(LayoutMode mode)
    {
        throw new NotSupportedException();
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        throw new NotSupportedException();
    }

    public void SetSettings(XmlNode settings)
    {
        throw new NotSupportedException();
    }

    public float HorizontalWidth => 1f;

    public float MinimumHeight => 0f;

    public void DrawHorizontal(IDrawingContext ctx, LiveSplitState state, float height, Region clipRegion)
    {
        using IDrawingState state_ = ctx.Save();
        ctx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
        Line.LineColor = Settings.GraphLinesColor;
        float scale = ctx.GetTransform().M11;
        float newWidth = Math.Max((int)((1f * scale) + 0.5f), 1) / scale;
        if (LockToBottom)
        {
            ctx.TranslateTransform(1f - newWidth, 0);
        }

        Line.HorizontalWidth = newWidth;
        Line.DrawHorizontal(ctx, state, height, clipRegion);
    }

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
    }
}
