using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using global::Avalonia.Controls;

using LiveSplit.UI;

using Xunit;

namespace LiveSplit.Tests.UI;

public class AvaloniaSettingsBuilderMust
{
    [Fact]
    public void DisableOverrideControlledColorRowsUntilOverrideIsChecked()
    {
        var settings = new ColorOverrideSettings();

        Control control = AvaloniaSettingsBuilder.Build(settings);

        CheckBox overrideCheckBox = Find<CheckBox>(control, "OverrideTextColorCheckBox");
        Control textColorRow = Find<Control>(control, "TextColorRow");
        Control backgroundColorRow = Find<Control>(control, "BackgroundColorRow");

        Assert.False(textColorRow.IsEnabled);
        Assert.True(backgroundColorRow.IsEnabled);

        overrideCheckBox.IsChecked = true;

        Assert.True(settings.OverrideTextColor);
        Assert.True(textColorRow.IsEnabled);
    }

    [Fact]
    public void DisableOverrideControlledFontRowsUntilOverrideIsChecked()
    {
        var settings = new FontOverrideSettings();

        Control control = AvaloniaSettingsBuilder.Build(settings);

        CheckBox overrideCheckBox = Find<CheckBox>(control, "OverrideTimerFontCheckBox");
        Control fontRow = Find<Control>(control, "TimerFontRow");

        Assert.False(fontRow.IsEnabled);

        overrideCheckBox.IsChecked = true;

        Assert.True(settings.OverrideTimerFont);
        Assert.True(fontRow.IsEnabled);
    }

    private static T Find<T>(Control root, string name) where T : Control
        => Descendants<T>(root).First(x => x.Name == name);

    private static IEnumerable<T> Descendants<T>(Control root) where T : Control
    {
        if (root is T typed)
        {
            yield return typed;
        }

        if (root is Decorator decorator && decorator.Child is Control decoratorChild)
        {
            foreach (T child in Descendants<T>(decoratorChild))
            {
                yield return child;
            }
        }

        if (root is ContentControl { Content: Control content })
        {
            foreach (T child in Descendants<T>(content))
            {
                yield return child;
            }
        }

        if (root is Panel panel)
        {
            foreach (Control childControl in panel.Children.OfType<Control>())
            {
                foreach (T child in Descendants<T>(childControl))
                {
                    yield return child;
                }
            }
        }
    }

    private sealed class ColorOverrideSettings
    {
        public bool OverrideTextColor { get; set; }
        public Color TextColor { get; set; } = Color.White;
        public Color BackgroundColor { get; set; } = Color.Black;
    }

    private sealed class FontOverrideSettings
    {
        public bool OverrideTimerFont { get; set; }
        public FontDescriptor TimerFont { get; set; } = new("Arial", 12f);
    }
}
