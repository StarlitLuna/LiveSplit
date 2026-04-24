using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using LiveSplit.Model;
using LiveSplit.UI.Drawing;

namespace LiveSplit.UI.Components;

public class InfoTextComponent : IComponent
{
    public string InformationName { get => NameLabel.Text; set => NameLabel.Text = value; }
    public string InformationValue { get => ValueLabel.Text; set => ValueLabel.Text = value; }

    public GraphicsCache Cache { get; set; }

    public ICollection<string> AlternateNameText { get => NameLabel.AlternateText; set => NameLabel.AlternateText = value; }

    public SimpleLabel NameLabel { get; protected set; }
    public SimpleLabel ValueLabel { get; protected set; }

    public string LongestString { get; set; }
    protected SimpleLabel NameMeasureLabel { get; set; }

    public float PaddingTop { get; set; }
    public float PaddingLeft => 7f;
    public float PaddingBottom { get; set; }
    public float PaddingRight => 7f;

    public bool DisplayTwoRows { get; set; }

    public float VerticalHeight { get; set; }

    public float MinimumWidth => 20;

    public float HorizontalWidth
        => Math.Max(NameMeasureLabel.ActualWidth, ValueLabel.ActualWidth) + 10;

    public float MinimumHeight { get; set; }

    public InfoTextComponent(string informationName, string informationValue)
    {
        Cache = new GraphicsCache();
        NameLabel = new SimpleLabel()
        {
            HorizontalAlignment = StringAlignment.Near,
            Text = informationName
        };
        ValueLabel = new SimpleLabel()
        {
            HorizontalAlignment = StringAlignment.Far,
            Text = informationValue
        };
        NameMeasureLabel = new SimpleLabel();
        MinimumHeight = 25;
        VerticalHeight = 31;
        LongestString = "";
    }

    public virtual void PrepareDraw(LiveSplitState state, LayoutMode mode)
    {
        NameMeasureLabel.Font = state.LayoutSettings.TextFont;
        ValueLabel.Font = state.LayoutSettings.TextFont;
        NameLabel.Font = state.LayoutSettings.TextFont;
        if (mode == LayoutMode.Vertical)
        {
            NameLabel.VerticalAlignment = StringAlignment.Center;
            ValueLabel.VerticalAlignment = StringAlignment.Center;
        }
        else
        {
            NameLabel.VerticalAlignment = StringAlignment.Near;
            ValueLabel.VerticalAlignment = StringAlignment.Far;
        }
    }

    public void DrawVertical(IDrawingContext ctx, LiveSplitState state, float width, Region clipRegion)
    {
        if (DisplayTwoRows)
        {
            VerticalHeight = 0.9f * (MeasureCapLetterHeight(ctx, ValueLabel.Font) + MeasureCapLetterHeight(ctx, NameLabel.Font));
            PaddingTop = PaddingBottom = 0;
            DrawTwoRows(ctx, state, width, VerticalHeight);
        }
        else
        {
            VerticalHeight = 31;
            NameLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
            NameLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;
            ValueLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
            ValueLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;

            float textHeight = 0.75f * Math.Max(MeasureCapLetterHeight(ctx, ValueLabel.Font), MeasureCapLetterHeight(ctx, NameLabel.Font));
            PaddingTop = Math.Max(0, (VerticalHeight - textHeight) / 2f);
            PaddingBottom = PaddingTop;

            NameMeasureLabel.Text = LongestString;
            NameMeasureLabel.SetActualWidth(ctx);
            ValueLabel.SetActualWidth(ctx);

            NameLabel.Width = width - ValueLabel.ActualWidth - 10;
            NameLabel.Height = VerticalHeight;
            NameLabel.X = 5;
            NameLabel.Y = 0;

            ValueLabel.Width = ValueLabel.IsMonospaced ? width - 12 : width - 10;
            ValueLabel.Height = VerticalHeight;
            ValueLabel.Y = 0;
            ValueLabel.X = 5;

            PrepareDraw(state, LayoutMode.Vertical);

            NameLabel.Draw(ctx);
            ValueLabel.Draw(ctx);
        }
    }

    public void DrawHorizontal(IDrawingContext ctx, LiveSplitState state, float height, Region clipRegion)
    {
        DrawTwoRows(ctx, state, HorizontalWidth, height);
    }

    protected void DrawTwoRows(IDrawingContext ctx, LiveSplitState state, float width, float height)
    {
        NameLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
        NameLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;
        ValueLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
        ValueLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;

        if (InformationName != null && LongestString != null && InformationName.Length > LongestString.Length)
        {
            LongestString = InformationName;
            NameMeasureLabel.Text = LongestString;
        }

        NameMeasureLabel.Text = LongestString;
        NameMeasureLabel.Font = state.LayoutSettings.TextFont;
        NameMeasureLabel.SetActualWidth(ctx);

        MinimumHeight = 0.85f * (MeasureCapLetterHeight(ctx, ValueLabel.Font) + MeasureCapLetterHeight(ctx, NameLabel.Font));
        NameLabel.Width = width - 10;
        NameLabel.Height = height;
        NameLabel.X = 5;
        NameLabel.Y = 0;

        ValueLabel.Width = ValueLabel.IsMonospaced ? width - 12 : width - 10;
        ValueLabel.Height = height;
        ValueLabel.Y = 0;
        ValueLabel.X = 5;

        PrepareDraw(state, LayoutMode.Horizontal);

        NameLabel.Draw(ctx);
        ValueLabel.Draw(ctx);
    }

    // Measure the visual height of a single capital letter in <paramref name="font"/>, used for
    // the row-height heuristics above. Replaces the old `g.MeasureString("A", font).Height`
    // now that InfoTextComponent routes through IDrawingContext.
    private static float MeasureCapLetterHeight(IDrawingContext ctx, Font font)
    {
        using IFont iFont = DrawingApi.Factory.CreateFont(
            font.FontFamily.Name, font.Size, font.Style, font.Unit);
        ITextFormat iFormat = DrawingApi.Factory.CreateTextFormat();
        return ctx.MeasureString("A", iFont, 9999, iFormat).Height;
    }

    public string ComponentName => throw new NotSupportedException();

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

    public IDictionary<string, Action> ContextMenuControls => null;

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        Cache.Restart();
        Cache["NameText"] = InformationName;
        Cache["ValueText"] = InformationValue;
        Cache["NameColor"] = NameLabel.ForeColor.ToArgb();
        Cache["ValueColor"] = ValueLabel.ForeColor.ToArgb();
        Cache["DisplayTwoRows"] = DisplayTwoRows;

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
