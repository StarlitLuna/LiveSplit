using System;
using System.Collections.Generic;
using System.Drawing;
using System.Xml;

using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.UI.Drawing;

namespace LiveSplit.UI.Components;

public class HotkeyIndicatorComponent : IComponent
{
    private const float Thickness = 5f;
    private static readonly Color EnabledColor = Color.FromArgb(0, 192, 0);
    private static readonly Color DisabledColor = Color.FromArgb(192, 0, 0);

    private bool? _lastEnabled;

    public string ComponentName => "Hotkey Indicator";

    public float HorizontalWidth => Thickness;

    public float MinimumHeight => Thickness;

    public float VerticalHeight => Thickness;

    public float MinimumWidth => Thickness;

    public float PaddingTop => 0;

    public float PaddingBottom => 0;

    public float PaddingLeft => 0;

    public float PaddingRight => 0;

    public IDictionary<string, Action> ContextMenuControls => null;

    public static bool AreGlobalHotkeysEnabled(LiveSplitState state)
    {
        if (state?.Settings?.HotkeyProfiles is null || string.IsNullOrEmpty(state.CurrentHotkeyProfile))
        {
            return false;
        }

        return state.Settings.HotkeyProfiles.TryGetValue(state.CurrentHotkeyProfile, out HotkeyProfile profile)
            && profile?.GlobalHotkeysEnabled == true;
    }

    public void DrawHorizontal(IDrawingContext ctx, LiveSplitState state, float height)
    {
        Draw(ctx, state, HorizontalWidth, height);
    }

    public void DrawVertical(IDrawingContext ctx, LiveSplitState state, float width)
    {
        Draw(ctx, state, width, VerticalHeight);
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        return document.CreateElement("Settings");
    }

    public void SetSettings(XmlNode settings)
    {
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        bool enabled = AreGlobalHotkeysEnabled(state);
        if (_lastEnabled != enabled)
        {
            invalidator?.Invalidate(0, 0, width, height);
            _lastEnabled = enabled;
        }
    }

    public void Dispose()
    {
    }

    private static void Draw(IDrawingContext ctx, LiveSplitState state, float width, float height)
    {
        Color color = AreGlobalHotkeysEnabled(state) ? EnabledColor : DisabledColor;
        using ISolidBrush brush = DrawingApi.Factory.CreateSolidBrush(color);
        ctx.FillRectangle(brush, 0, 0, width, height);
    }
}
