using System.Collections.Generic;
using System.IO;
using System.Linq;

using global::Avalonia.Controls;

using LiveSplit.AutoSplittingRuntime;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;

using Xunit;

namespace LiveSplit.Tests.UI.Components;

public class AutoSplittingRuntimeSettingsMust
{
    [Fact]
    public void SettingsControlExposesWasmPicker()
    {
        var component = new ASRComponent(CreateState());
        Control control = component.GetSettingsControl(LayoutMode.Vertical);

        Assert.NotNull(FindNamed<TextBox>(control, "ScriptPathTextBox"));
        Assert.NotNull(FindNamed<Button>(control, "BrowseScriptButton"));
        Assert.NotNull(FindNamed<StackPanel>(control, "RuntimeSettingsPanel"));
    }

    [Fact]
    public void FixedScriptPathDisablesPicker()
    {
        var component = new ASRComponent(CreateState(), "fixed.wasm");
        Control control = component.GetSettingsControl(LayoutMode.Vertical);

        Assert.False(FindNamed<TextBox>(control, "ScriptPathTextBox")!.IsEnabled);
        Assert.False(FindNamed<Button>(control, "BrowseScriptButton")!.IsEnabled);
    }

    [Fact]
    public void RenderGeneratedBoolChoiceAndFileWidgets()
    {
        var sink = new RecordingRuntimeSettingsSink();
        Control control = AsrSettingsControlFactory.BuildRuntimeSettings(
        [
            AsrWidgetDescriptor.Bool("auto_start", "Auto Start", true),
            AsrWidgetDescriptor.Choice("route", "Route", ["any", "ng"], ["Any%", "New Game+"], 1),
            AsrWidgetDescriptor.FileSelect("image", "Image", "PNG files|*.png|All files (*.*)|*.*"),
        ], sink);

        FindNamed<CheckBox>(control, "Widgetauto_startCheckBox")!.IsChecked = false;
        FindNamed<ComboBox>(control, "WidgetrouteComboBox")!.SelectedIndex = 0;
        Assert.NotNull(FindNamed<TextBox>(control, "WidgetimagePathTextBox"));
        Assert.NotNull(FindNamed<Button>(control, "WidgetimageBrowseButton"));

        Assert.Equal(("auto_start", false), sink.Bools.Single());
        Assert.Equal(("route", "any"), sink.Strings.Single());
    }

    [Fact]
    public void RefreshesRuntimeSettingsControlsThroughAvaloniaDispatcher()
    {
        string source = File.ReadAllText(FindRepoFile("components/LiveSplit.AutoSplittingRuntime/src/LiveSplit.AutoSplittingRuntime/ComponentSettings.cs"));

        Assert.Contains("Dispatcher.UIThread", source);
        Assert.Contains("RefreshRuntimeSettingsControlOnUiThread", source);
    }

    private static LiveSplitState CreateState()
    {
        IRun run = new StandardRunFactory().Create(new StandardComparisonGeneratorsFactory());
        return new LiveSplitState(
            run,
            null,
            new Layout(),
            new StandardLayoutSettingsFactory().Create(),
            new StandardSettingsFactory().Create())
        {
            CurrentComparison = Run.PersonalBestComparisonName,
            CurrentTimingMethod = TimingMethod.RealTime
        };
    }

    private static T FindNamed<T>(Control root, string name)
        where T : Control
    {
        if (root is T typed && root.Name == name)
        {
            return typed;
        }

        if (root is Decorator decorator && decorator.Child is Control decoratorChild)
        {
            T match = FindNamed<T>(decoratorChild, name);
            if (match != null)
            {
                return match;
            }
        }

        if (root is ContentControl contentControl && contentControl.Content is Control content)
        {
            T match = FindNamed<T>(content, name);
            if (match != null)
            {
                return match;
            }
        }

        if (root is Panel panel)
        {
            foreach (Control child in panel.Children.OfType<Control>())
            {
                T match = FindNamed<T>(child, name);
                if (match != null)
                {
                    return match;
                }
            }
        }

        if (root is ItemsControl itemsControl)
        {
            foreach (object item in itemsControl.Items)
            {
                if (item is Control itemControl)
                {
                    T match = FindNamed<T>(itemControl, name);
                    if (match != null)
                    {
                        return match;
                    }
                }
            }
        }

        return null;
    }

    private static string FindRepoFile(string relativePath)
    {
        string dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            string candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException(relativePath);
    }

    private sealed class RecordingRuntimeSettingsSink : IAsrRuntimeSettingsSink
    {
        public List<(string Key, bool Value)> Bools { get; } = [];
        public List<(string Key, string Value)> Strings { get; } = [];

        public void SetBool(string key, bool value)
            => Bools.Add((key, value));

        public void SetString(string key, string value)
            => Strings.Add((key, value));
    }
}
