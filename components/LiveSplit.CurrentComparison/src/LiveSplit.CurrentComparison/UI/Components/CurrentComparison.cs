using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

using LiveSplit.Localization;
using LiveSplit.Model;
using LiveSplit.UI.Drawing;

namespace LiveSplit.UI.Components;

[GlobalFontConsumer(GlobalFont.TextFont)]
public class CurrentComparison : IComponent
{
    private static string T(string source) => UiLocalizer.Translate(source, LanguageResolver.ResolveCurrentCultureLanguage());

    protected InfoTextComponent InternalComponent { get; set; }
    public CurrentComparisonSettings Settings { get; set; }

    public float PaddingTop => InternalComponent.PaddingTop;
    public float PaddingLeft => InternalComponent.PaddingLeft;
    public float PaddingBottom => InternalComponent.PaddingBottom;
    public float PaddingRight => InternalComponent.PaddingRight;

    public IDictionary<string, Action> ContextMenuControls => null;

    public CurrentComparison(LiveSplitState state)
    {
        Settings = new CurrentComparisonSettings()
        {
            CurrentState = state
        };
        InternalComponent = new InfoTextComponent(T("Comparing Against"), "")
        {
            AlternateNameText = new[]
            {
                T("Comparison"),
                T("Comp.")
            }
        };
    }

    private void PrepareDraw(LiveSplitState state, LayoutMode mode)
    {
        InternalComponent.DisplayTwoRows = Settings.Display2Rows;

        InternalComponent.NameLabel.HasShadow
            = InternalComponent.ValueLabel.HasShadow
            = state.LayoutSettings.DropShadows;

        InternalComponent.NameLabel.ForeColor = Settings.OverrideTextColor ? Settings.TextColor : state.LayoutSettings.TextColor;
        InternalComponent.ValueLabel.ForeColor = Settings.OverrideTimeColor ? Settings.TimeColor : state.LayoutSettings.TextColor;
    }

    private void DrawBackground(IDrawingContext ctx, LiveSplitState state, float width, float height)
    {
        LiveSplit.UI.Drawing.BackgroundHelper.DrawBackground(ctx,
            Settings.BackgroundColor, Settings.BackgroundColor2,
            width, height, Settings.BackgroundGradient);
    }

    public void DrawVertical(IDrawingContext ctx, LiveSplitState state, float width, Region clipRegion)
    {
        DrawBackground(ctx, state, width, VerticalHeight);
        PrepareDraw(state, LayoutMode.Vertical);
        InternalComponent.DrawVertical(ctx, state, width, clipRegion);
    }

    public void DrawHorizontal(IDrawingContext ctx, LiveSplitState state, float height, Region clipRegion)
    {
        DrawBackground(ctx, state, HorizontalWidth, height);
        PrepareDraw(state, LayoutMode.Horizontal);
        InternalComponent.DrawHorizontal(ctx, state, height, clipRegion);
    }

    public float VerticalHeight => InternalComponent.VerticalHeight;

    public float MinimumWidth => InternalComponent.MinimumWidth;

    public float HorizontalWidth => InternalComponent.HorizontalWidth;

    public float MinimumHeight => InternalComponent.MinimumHeight;

    public string ComponentName => T("Current Comparison");

    public Control GetSettingsControl(LayoutMode mode)
    {
        Settings.Mode = mode;
        return Settings;
    }

    public Avalonia.Controls.Control GetSettingsControlAvalonia(LayoutMode mode)
    {
        Settings.Mode = mode;
        return LiveSplit.UI.AvaloniaSettingsBuilder.Build(Settings, T("Current Comparison"));
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
        InternalComponent.LongestString = InternalComponent.InformationName;
        InternalComponent.InformationValue = state.CurrentComparison;

        InternalComponent.Update(invalidator, state, width, height, mode);
    }

    public void Dispose()
    {
    }

    public int GetSettingsHashCode()
    {
        return Settings.GetSettingsHashCode();
    }
}
