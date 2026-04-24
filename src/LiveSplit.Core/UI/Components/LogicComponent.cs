using System;
using System.Collections.Generic;

using LiveSplit.UI.Drawing;

namespace LiveSplit.UI.Components;

public abstract class LogicComponent : IComponent
{
    public abstract string ComponentName
    {
        get;
    }

    public float HorizontalWidth => 0;

    public float MinimumHeight => 0;

    public float VerticalHeight => 0;

    public float MinimumWidth => 0;

    public float PaddingTop => 0;

    public float PaddingBottom => 0;

    public float PaddingLeft => 0;

    public float PaddingRight => 0;

    public IDictionary<string, Action> ContextMenuControls
    {
        get;
        protected set;
    }

    public void DrawHorizontal(IDrawingContext ctx, Model.LiveSplitState state, float height, System.Drawing.Region clipRegion)
    {
    }

    public void DrawVertical(IDrawingContext ctx, Model.LiveSplitState state, float width, System.Drawing.Region clipRegion)
    {
    }

    public abstract System.Windows.Forms.Control GetSettingsControl(LayoutMode mode);

    public abstract System.Xml.XmlNode GetSettings(System.Xml.XmlDocument document);

    public abstract void SetSettings(System.Xml.XmlNode settings);

    public abstract void Update(IInvalidator invalidator, Model.LiveSplitState state, float width, float height, LayoutMode mode);

    public abstract void Dispose();
}
