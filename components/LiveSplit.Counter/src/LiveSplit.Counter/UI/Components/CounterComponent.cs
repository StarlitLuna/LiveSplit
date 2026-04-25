using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using LiveSplit.Model;
using LiveSplit.UI.Drawing;

namespace LiveSplit.UI.Components;

[GlobalFontConsumer(GlobalFont.TextFont)]
public class CounterComponent : IComponent
{
    public CounterComponent(LiveSplitState state)
    {
        VerticalHeight = 10;
        Settings = new CounterComponentSettings();
        Cache = new GraphicsCache();
        CounterNameLabel = new SimpleLabel();
        Counter = new Counter();
        this.state = state;
        Settings.CounterReinitialiseRequired += Settings_CounterReinitialiseRequired;
        Settings.IncrementUpdateRequired += Settings_IncrementUpdateRequired;
    }

    public ICounter Counter { get; set; }
    public CounterComponentSettings Settings { get; set; }

    public GraphicsCache Cache { get; set; }

    public float VerticalHeight { get; set; }

    public float MinimumHeight { get; set; }

    public float MinimumWidth => CounterNameLabel.X + CounterValueLabel.ActualWidth;

    public float HorizontalWidth { get; set; }

    public IDictionary<string, Action> ContextMenuControls => null;

    public float PaddingTop { get; set; }
    public float PaddingLeft => 7f;
    public float PaddingBottom { get; set; }
    public float PaddingRight => 7f;

    protected SimpleLabel CounterNameLabel = new();
    protected SimpleLabel CounterValueLabel = new();

    protected Font CounterFont { get; set; }

    private LiveSplitState state;

    private void DrawGeneral(IDrawingContext ctx, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        BackgroundHelper.DrawBackground(ctx,
            Settings.BackgroundColor, Settings.BackgroundColor2,
            width, height, Settings.BackgroundGradient);

        // Set Font.
        CounterFont = Settings.OverrideCounterFont ? Settings.CounterFont : state.LayoutSettings.TextFont;

        // Calculate Height from Font.
        using IFont counterIFont = DrawingApi.Factory.CreateFont(
            CounterFont.FontFamily.Name, CounterFont.Size, CounterFont.Style, CounterFont.Unit);
        ITextFormat measureFormat = DrawingApi.Factory.CreateTextFormat();
        float textHeight = ctx.MeasureString("A", counterIFont, 9999, measureFormat).Height;
        VerticalHeight = 1.2f * textHeight;
        MinimumHeight = MinimumHeight;

        PaddingTop = Math.Max(0, (VerticalHeight - (0.75f * textHeight)) / 2f);
        PaddingBottom = PaddingTop;

        // Assume most users won't count past four digits (will cause a layout resize in Horizontal Mode).
        float fourCharWidth = ctx.MeasureString("1000", counterIFont, 9999, measureFormat).Width;
        HorizontalWidth = CounterNameLabel.X + CounterNameLabel.ActualWidth + (fourCharWidth > CounterValueLabel.ActualWidth ? fourCharWidth : CounterValueLabel.ActualWidth) + 5;

        // Set Counter Name Label
        CounterNameLabel.HorizontalAlignment = mode == LayoutMode.Horizontal ? StringAlignment.Near : StringAlignment.Near;
        CounterNameLabel.VerticalAlignment = StringAlignment.Center;
        CounterNameLabel.X = 5;
        CounterNameLabel.Y = 0;
        CounterNameLabel.Width = width - fourCharWidth - 5;
        CounterNameLabel.Height = height;
        CounterNameLabel.Font = CounterFont;
        CounterNameLabel.Brush = new SolidBrush(Settings.OverrideTextColor ? Settings.CounterTextColor : state.LayoutSettings.TextColor);
        CounterNameLabel.HasShadow = state.LayoutSettings.DropShadows;
        CounterNameLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
        CounterNameLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;
        CounterNameLabel.Draw(ctx);

        // Set Counter Value Label.
        CounterValueLabel.HorizontalAlignment = mode == LayoutMode.Horizontal ? StringAlignment.Far : StringAlignment.Far;
        CounterValueLabel.VerticalAlignment = StringAlignment.Center;
        CounterValueLabel.X = 5;
        CounterValueLabel.Y = 0;
        CounterValueLabel.Width = width - 10;
        CounterValueLabel.Height = height;
        CounterValueLabel.Font = CounterFont;
        CounterValueLabel.Brush = new SolidBrush(Settings.OverrideTextColor ? Settings.CounterValueColor : state.LayoutSettings.TextColor);
        CounterValueLabel.HasShadow = state.LayoutSettings.DropShadows;
        CounterValueLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
        CounterValueLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;
        CounterValueLabel.Draw(ctx);
    }

    public void DrawHorizontal(IDrawingContext ctx, LiveSplitState state, float height, Region clipRegion)
    {
        DrawGeneral(ctx, state, HorizontalWidth, height, LayoutMode.Horizontal);
    }

    public void DrawVertical(IDrawingContext ctx, LiveSplitState state, float width, Region clipRegion)
    {
        DrawGeneral(ctx, state, width, VerticalHeight, LayoutMode.Vertical);
    }

    public string ComponentName => "Counter";

    public Control GetSettingsControl(LayoutMode mode)
    {
        return Settings;
    }

    public Avalonia.Controls.Control GetSettingsControlAvalonia(LayoutMode mode)
    {
        return LiveSplit.UI.AvaloniaSettingsBuilder.Build(Settings, "Counter");
    }

    public System.Xml.XmlNode GetSettings(System.Xml.XmlDocument document)
    {
        return Settings.GetSettings(document);
    }

    public void SetSettings(System.Xml.XmlNode settings)
    {
        Settings.SetSettings(settings);

        // Initialise Counter from settings.
        Counter = new Counter(Settings.InitialValue, Settings.Increment);
    }

    public void MigrateFontOverrides(Options.FontOverrides overrides)
    {
        if (Settings.OverrideCounterFont && Settings.CounterFont != null)
        {
            overrides.OverrideTextFont = true;
            overrides.TextFont = (Font)Settings.CounterFont.Clone();
            Settings.OverrideCounterFont = false;
        }
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        this.state = state;

        CounterNameLabel.Text = Settings.CounterText;
        CounterValueLabel.Text = Counter.Count.ToString();

        Cache.Restart();
        Cache["CounterNameLabel"] = CounterNameLabel.Text;
        Cache["CounterValueLabel"] = CounterValueLabel.Text;

        if (invalidator != null && Cache.HasChanged)
        {
            invalidator.Invalidate(0, 0, width, height);
        }
    }

    public void Dispose()
    {
    }

    public int GetSettingsHashCode()
    {
        return Settings.GetSettingsHashCode();
    }

    /// <summary>
    /// Handles the CounterReinitialiseRequired event of the Settings control.
    /// </summary>
    private void Settings_CounterReinitialiseRequired(object sender, EventArgs e)
    {
        Counter = new Counter(Settings.InitialValue, Settings.Increment);
    }

    private void Settings_IncrementUpdateRequired(object sender, EventArgs e)
    {
        Counter.SetIncrement(Settings.Increment);
    }

}
