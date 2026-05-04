using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Xml;

using global::Avalonia.Controls;

using LiveSplit.Avalonia.Dialogs;
using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.UI.Drawing;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class LayoutSettingsDialogMust
{
    [Fact]
    public void UseMasterPaddedTabTableMetrics()
    {
        object spec = LayoutSpec();

        Assert.Equal(new[] { -1, 81 }, IntList(spec, "ColumnWidths"));
        Assert.Equal(new[] { -1, 29 }, IntList(spec, "RowHeights"));
        Assert.Equal(2, Int(spec, "TabColumnSpan"));
        Assert.Equal(504, Int(spec, "InitialClientWidth"));
        Assert.Equal(665, Int(spec, "InitialClientHeight"));
        Assert.Equal(520, Int(spec, "MinimumWindowWidth"));
        Assert.Equal(674, Int(spec, "MinimumWindowHeight"));
        Assert.Equal(520, Int(spec, "MaximumWindowWidth"));
        Assert.Equal(7, Int(spec, "Padding"));
        Assert.Equal(75, Int(spec, "ButtonWidth"));
        Assert.Equal(23, Int(spec, "ButtonHeight"));
        Assert.Equal(new[] { "Tabs", "OK", "Cancel" }, StringList(spec, "StructuralOrder"));

        string source = ReadSource();
        Assert.DoesNotContain("new DockPanel", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildMasterStyleTabsForLayoutAndComponentSettings()
    {
        var component = new ModeTrackingComponent();
        var layout = CreateLayout();
        layout.Mode = LayoutMode.Horizontal;
        layout.LayoutComponents.Add(new LayoutComponent("tracking.dll", component));

        TabControl tabs = LayoutSettingsDialog.CreateTabs(layout.Settings, layout, component);
        var items = tabs.Items.Cast<TabItem>().ToList();

        Assert.Equal(["Layout", "Tracking"], items.Select(x => x.Header?.ToString()));
        Assert.Equal(LayoutMode.Horizontal, component.LastMode);
        Assert.Equal(1, tabs.SelectedIndex);
    }

    [Fact]
    public void RestoreLayoutComponentSettingsAndFontOverridesOnCancel()
    {
        var component = new MutableSettingsComponent();
        var layoutComponent = new LayoutComponent("mutable.dll", component)
        {
            FontOverrides = new FontOverrides
            {
                OverrideTextFont = true,
                TextFont = new FontDescriptor("Arial", 12f),
            },
        };
        var layout = CreateLayout();
        layout.LayoutComponents.Add(layoutComponent);

        LayoutSettings settingsSnapshot = layout.Settings.Clone() as LayoutSettings;
        var componentSnapshots = new List<LayoutSettingsDialog.ComponentSnapshot>();
        LayoutSettingsDialog.CreateTabs(layout.Settings, layout, snapshots: componentSnapshots);
        component.Value = "mutated";
        layout.Settings.TextColor = Color.Red;
        layoutComponent.FontOverrides.OverrideTextFont = false;
        layoutComponent.FontOverrides.TextFont = new FontDescriptor("Consolas", 18f);

        LayoutSettingsDialog.RestoreSnapshotsForCancel(layout.Settings, settingsSnapshot, componentSnapshots);

        Assert.Equal("initial", component.Value);
        Assert.Equal(Color.Blue.ToArgb(), layout.Settings.TextColor.ToArgb());
        Assert.True(layoutComponent.FontOverrides.OverrideTextFont);
        Assert.Equal("Arial", layoutComponent.FontOverrides.TextFont.FamilyName);
    }

    [Fact]
    public void ComponentTabsExposeEditableFontOverrideRowsForConsumedFonts()
    {
        var component = new FontConsumerComponent();
        var layoutComponent = new LayoutComponent("font.dll", component);
        var layout = CreateLayout();
        layout.LayoutComponents.Add(layoutComponent);

        TabControl tabs = LayoutSettingsDialog.CreateTabs(layout.Settings, layout, component);
        var tab = tabs.Items.Cast<TabItem>().Single(x => x.Header?.ToString() == "Font Consumer");
        List<string> checkboxLabels = FindDescendants<CheckBox>((Control)tab.Content)
            .Select(x => x.Content?.ToString())
            .Where(x => x != null)
            .ToList();

        Assert.DoesNotContain("Override Timer Font", checkboxLabels);
        Assert.Contains("Override Times Font", checkboxLabels);
        Assert.Contains("Override Text Font", checkboxLabels);

        CheckBox textOverride = FindDescendants<CheckBox>((Control)tab.Content)
            .Single(x => x.Content?.ToString() == "Override Text Font");
        textOverride.IsChecked = true;

        Assert.True(layoutComponent.FontOverrides.OverrideTextFont);
        Assert.Equal(layout.Settings.TextFont.FamilyName, layoutComponent.FontOverrides.TextFont.FamilyName);
    }

    [Fact]
    public void FontOverrideRowsEditFontDescriptorFields()
    {
        var component = new FontConsumerComponent();
        var layoutComponent = new LayoutComponent("font.dll", component);
        var layout = CreateLayout();
        layout.LayoutComponents.Add(layoutComponent);

        TabControl tabs = LayoutSettingsDialog.CreateTabs(layout.Settings, layout, component);
        var tab = tabs.Items.Cast<TabItem>().Single(x => x.Header?.ToString() == "Font Consumer");
        Control content = (Control)tab.Content;
        CheckBox textOverride = FindDescendants<CheckBox>(content)
            .Single(x => x.Content?.ToString() == "Override Text Font");
        TextBox familyBox = FindDescendants<TextBox>(content).Single(x => x.Name == "OverrideTextFontFamily");
        TextBox sizeBox = FindDescendants<TextBox>(content).Single(x => x.Name == "OverrideTextFontSize");
        ComboBox styleBox = FindDescendants<ComboBox>(content).Single(x => x.Name == "OverrideTextFontStyle");

        textOverride.IsChecked = true;
        familyBox.Text = "Consolas";
        sizeBox.Text = "18.5";
        styleBox.SelectedItem = FontStyle.Bold;

        Assert.Equal("Consolas", layoutComponent.FontOverrides.TextFont.FamilyName);
        Assert.Equal(18.5f, layoutComponent.FontOverrides.TextFont.Size);
        Assert.Equal(FontStyle.Bold, layoutComponent.FontOverrides.TextFont.Style);
    }

    private static Layout CreateLayout()
    {
        LayoutSettings settings = new StandardLayoutSettingsFactory().Create();
        settings.TextColor = Color.Blue;
        return new Layout
        {
            Settings = settings,
            Mode = LayoutMode.Vertical,
        };
    }

    private static object LayoutSpec()
    {
        Type type = Type.GetType("LiveSplit.Avalonia.Dialogs.LayoutSettingsDialogLayoutSpec, LiveSplit");
        Assert.NotNull(type);
        object value = type.GetProperty("Master", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(null);
        Assert.NotNull(value);
        return value;
    }

    private static IReadOnlyList<int> IntList(object instance, string propertyName)
        => Assert.IsAssignableFrom<IEnumerable<int>>(
            instance.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(instance)).ToList();

    private static IReadOnlyList<string> StringList(object instance, string propertyName)
        => Assert.IsAssignableFrom<IEnumerable<string>>(
            instance.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(instance)).ToList();

    private static int Int(object instance, string propertyName)
        => Assert.IsType<int>(
            instance.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(instance));

    private static string ReadSource()
        => System.IO.File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/LayoutSettingsDialog.cs"));

    private static string FindRepoFile(string relativePath)
    {
        System.IO.DirectoryInfo directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = System.IO.Path.Combine(directory.FullName, relativePath);
            if (System.IO.File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new System.IO.FileNotFoundException(relativePath);
    }

    private sealed class ModeTrackingComponent : IComponent
    {
        public LayoutMode? LastMode { get; private set; }

        public string ComponentName => "Tracking";
        public float HorizontalWidth => 0;
        public float MinimumHeight => 0;
        public float VerticalHeight => 0;
        public float MinimumWidth => 0;
        public float PaddingTop => 0;
        public float PaddingBottom => 0;
        public float PaddingLeft => 0;
        public float PaddingRight => 0;
        public IDictionary<string, Action> ContextMenuControls => null;

        public void DrawHorizontal(IDrawingContext ctx, LiveSplitState state, float height) { }
        public void DrawVertical(IDrawingContext ctx, LiveSplitState state, float width) { }
        public Control GetSettingsControl(LayoutMode mode)
        {
            LastMode = mode;
            return new Panel();
        }

        public XmlNode GetSettings(XmlDocument document) => document.CreateElement("Settings");
        public void SetSettings(XmlNode settings) { }
        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode) { }
        public void Dispose() { }
    }

    private sealed class MutableSettingsComponent : IComponent
    {
        public string Value { get; set; } = "initial";
        public string ComponentName => "Mutable";
        public float HorizontalWidth => 0;
        public float MinimumHeight => 0;
        public float VerticalHeight => 0;
        public float MinimumWidth => 0;
        public float PaddingTop => 0;
        public float PaddingBottom => 0;
        public float PaddingLeft => 0;
        public float PaddingRight => 0;
        public IDictionary<string, Action> ContextMenuControls => null;

        public void DrawHorizontal(IDrawingContext ctx, LiveSplitState state, float height) { }
        public void DrawVertical(IDrawingContext ctx, LiveSplitState state, float width) { }
        public Control GetSettingsControl(LayoutMode mode) => new Panel();
        public XmlNode GetSettings(XmlDocument document)
        {
            XmlElement settings = document.CreateElement("Settings");
            XmlElement value = document.CreateElement("Value");
            value.InnerText = Value;
            settings.AppendChild(value);
            return settings;
        }

        public void SetSettings(XmlNode settings) => Value = settings["Value"]?.InnerText;
        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode) { }
        public void Dispose() { }
    }

    [GlobalFontConsumer(GlobalFont.TimesFont | GlobalFont.TextFont)]
    private sealed class FontConsumerComponent : IComponent
    {
        public string ComponentName => "Font Consumer";
        public float HorizontalWidth => 0;
        public float MinimumHeight => 0;
        public float VerticalHeight => 0;
        public float MinimumWidth => 0;
        public float PaddingTop => 0;
        public float PaddingBottom => 0;
        public float PaddingLeft => 0;
        public float PaddingRight => 0;
        public IDictionary<string, Action> ContextMenuControls => null;

        public void DrawHorizontal(IDrawingContext ctx, LiveSplitState state, float height) { }
        public void DrawVertical(IDrawingContext ctx, LiveSplitState state, float width) { }
        public Control GetSettingsControl(LayoutMode mode) => new Panel();
        public XmlNode GetSettings(XmlDocument document) => document.CreateElement("Settings");
        public void SetSettings(XmlNode settings) { }
        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode) { }
        public void Dispose() { }
    }

    private static IEnumerable<T> FindDescendants<T>(Control root)
        where T : Control
    {
        if (root is T match)
        {
            yield return match;
        }

        foreach (Control child in LogicalChildren(root))
        {
            foreach (T descendant in FindDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<Control> LogicalChildren(Control root)
    {
        switch (root)
        {
            case Panel panel:
                return panel.Children.OfType<Control>();
            case Decorator decorator when decorator.Child is Control child:
                return [child];
            case ContentControl contentControl when contentControl.Content is Control child:
                return [child];
            default:
                return [];
        }
    }
}
