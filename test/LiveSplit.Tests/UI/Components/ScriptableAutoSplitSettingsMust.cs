using System.IO;
using System.Linq;
using System.Xml;

using global::Avalonia.Controls;

using LiveSplit.ASL;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.UI.Components;

using Xunit;

namespace LiveSplit.Tests.UI.Components;

public class ScriptableAutoSplitSettingsMust
{
    [Fact]
    public void ApplyStoredBasicAndCustomSettingsToLoadedScriptSettings()
    {
        var settings = new ComponentSettings();
        settings.SetSettings(CreateSettingsXml(scriptPath: "script.asl", start: false, reset: true, split: false, ("boss", false)));

        var aslSettings = new ASLSettings();
        aslSettings.AddBasicSetting("start");
        aslSettings.AddBasicSetting("reset");
        aslSettings.AddBasicSetting("split");
        aslSettings.AddSetting("boss", true, "Boss", null);

        settings.SetASLSettings(aslSettings);

        Assert.False(aslSettings.BasicSettings["start"].Value);
        Assert.True(aslSettings.BasicSettings["reset"].Value);
        Assert.False(aslSettings.BasicSettings["split"].Value);
        Assert.False(aslSettings.Settings["boss"].Value);
    }

    [Fact]
    public void ResetKeepsStoredCustomValuesWhenScriptPathStillExists()
    {
        var settings = new ComponentSettings();
        settings.SetSettings(CreateSettingsXml(scriptPath: "script.asl", start: true, reset: true, split: true, ("boss", false)));

        settings.ResetASLSettings();

        var document = new XmlDocument();
        XmlNode saved = settings.GetSettings(document);
        XmlElement custom = ((XmlElement)saved)["CustomSettings"];

        Assert.Equal("boss", custom["Setting"].Attributes["id"].Value);
        Assert.Equal("False", custom["Setting"].InnerText);
    }

    [Fact]
    public void SettingsControlExposesScriptPickerAndKnownAslSettings()
    {
        var component = new ASLComponent(CreateState());
        Control control = component.GetSettingsControl(LayoutMode.Vertical);

        Assert.NotNull(FindNamed<TextBox>(control, "ScriptPathTextBox"));
        Assert.NotNull(FindNamed<Button>(control, "BrowseScriptButton"));
        Assert.NotNull(FindNamed<StackPanel>(control, "BasicSettingsPanel"));
        Assert.NotNull(FindNamed<StackPanel>(control, "CustomSettingsPanel"));
    }

    [Fact]
    public void SettingsControlRefreshesWhenAslSettingsArriveAfterBuild()
    {
        var settings = new ComponentSettings();
        Control control = settings.BuildSettingsControl();

        Assert.Null(FindNamed<CheckBox>(control, "CustomSettingparentCheckBox"));

        ASLSettings aslSettings = CreateCustomSettings(
            ("parent", true, "Parent", null),
            ("child", true, "Child", "parent"));
        settings.SetASLSettings(aslSettings);

        Assert.NotNull(FindNamed<TreeView>(control, "CustomSettingsTree"));
        Assert.NotNull(FindNamed<CheckBox>(control, "CustomSettingparentCheckBox"));
        Assert.NotNull(FindNamed<CheckBox>(control, "CustomSettingchildCheckBox"));
    }

    [Fact]
    public void ResetClearsVisibleAdvancedSettingsButKeepsStoredValues()
    {
        var settings = new ComponentSettings();
        settings.SetSettings(CreateSettingsXml(scriptPath: "script.asl", start: true, reset: true, split: true, ("boss", false)));
        Control control = settings.BuildSettingsControl();

        settings.SetASLSettings(CreateCustomSettings(("boss", true, "Boss", null)));
        Assert.False(FindNamed<CheckBox>(control, "CustomSettingbossCheckBox")!.IsChecked);

        settings.ResetASLSettings();

        Assert.Null(FindNamed<CheckBox>(control, "CustomSettingbossCheckBox"));

        ASLSettings reloaded = CreateCustomSettings(("boss", true, "Boss", null));
        settings.SetASLSettings(reloaded);

        Assert.False(reloaded.Settings["boss"].Value);
        Assert.False(FindNamed<CheckBox>(control, "CustomSettingbossCheckBox")!.IsChecked);
    }

    [Fact]
    public void CustomSettingsXmlReloadUpdatesLoadedAslSettings()
    {
        var settings = new ComponentSettings();
        ASLSettings aslSettings = CreateCustomSettings(("boss", true, "Boss", null));
        settings.SetASLSettings(aslSettings);
        Control control = settings.BuildSettingsControl();

        settings.SetSettings(CreateSettingsXml(scriptPath: "script.asl", start: true, reset: true, split: true, ("boss", false)));

        Assert.False(aslSettings.Settings["boss"].Value);
        Assert.False(FindNamed<CheckBox>(control, "CustomSettingbossCheckBox")!.IsChecked);
    }

    [Fact]
    public void AdvancedBranchActionsUpdateSelectedBranchOnly()
    {
        var settings = new ComponentSettings();
        ASLSettings aslSettings = CreateCustomSettings(
            ("parent", true, "Parent", null),
            ("child", true, "Child", "parent"),
            ("other", true, "Other", null));
        settings.SetASLSettings(aslSettings);
        Control control = settings.BuildSettingsControl();

        FindNamed<MenuItem>(control, "CustomSettingparentUncheckBranchMenuItem")!.Command!.Execute(null);

        Assert.False(aslSettings.Settings["parent"].Value);
        Assert.False(aslSettings.Settings["child"].Value);
        Assert.True(aslSettings.Settings["other"].Value);

        FindNamed<MenuItem>(control, "CustomSettingparentCheckBranchMenuItem")!.Command!.Execute(null);

        Assert.True(aslSettings.Settings["parent"].Value);
        Assert.True(aslSettings.Settings["child"].Value);
        Assert.True(aslSettings.Settings["other"].Value);
    }

    [Fact]
    public void UncheckedParentDisablesDescendantInteraction()
    {
        var settings = new ComponentSettings();
        ASLSettings aslSettings = CreateCustomSettings(
            ("parent", true, "Parent", null),
            ("child", true, "Child", "parent"));
        settings.SetASLSettings(aslSettings);
        Control control = settings.BuildSettingsControl();

        CheckBox parent = FindNamed<CheckBox>(control, "CustomSettingparentCheckBox")!;
        CheckBox child = FindNamed<CheckBox>(control, "CustomSettingchildCheckBox")!;
        parent.IsChecked = false;

        Assert.False(parent.IsChecked);
        Assert.False(child.IsEnabled);
        Assert.True(child.IsChecked);
        Assert.True(aslSettings.Settings["child"].Value);

        child.IsChecked = false;

        Assert.True(child.IsChecked);
        Assert.True(aslSettings.Settings["child"].Value);

        parent.IsChecked = true;

        Assert.True(child.IsEnabled);
        Assert.True(child.IsChecked);
    }

    [Fact]
    public void AdvancedContextMenuExposesMasterActions()
    {
        var settings = new ComponentSettings();
        settings.SetASLSettings(CreateCustomSettings(
            ("parent", true, "Parent", null),
            ("child", true, "Child", "parent")));
        Control control = settings.BuildSettingsControl();

        string[] branchHeaders = FindNamed<TreeViewItem>(control, "CustomSettingparentTreeItem")!
            .ContextMenu!
            .Items
            .OfType<MenuItem>()
            .Select(item => item.Header?.ToString())
            .ToArray();

        Assert.Contains("Expand Tree", branchHeaders);
        Assert.Contains("Collapse Tree", branchHeaders);
        Assert.Contains("Collapse Tree to Selection", branchHeaders);
        Assert.Contains("Expand Branch", branchHeaders);
        Assert.Contains("Collapse Branch", branchHeaders);
        Assert.Contains("Check Branch", branchHeaders);
        Assert.Contains("Uncheck Branch", branchHeaders);
        Assert.Contains("Reset Branch to Default", branchHeaders);

        string[] leafHeaders = FindNamed<TreeViewItem>(control, "CustomSettingchildTreeItem")!
            .ContextMenu!
            .Items
            .OfType<MenuItem>()
            .Select(item => item.Header?.ToString())
            .ToArray();

        Assert.Contains("Reset Setting to Default", leafHeaders);
    }

    [Fact]
    public void RefreshesSettingsControlsThroughAvaloniaDispatcher()
    {
        string source = File.ReadAllText(FindRepoFile("components/LiveSplit.ScriptableAutoSplit/src/LiveSplit.ScriptableAutoSplit/ComponentSettings.cs"));

        Assert.Contains("Dispatcher.UIThread", source);
        Assert.Contains("RefreshSettingsControlsOnUiThread", source);
    }

    private static XmlNode CreateSettingsXml(string scriptPath, bool start, bool reset, bool split, params (string Id, bool Value)[] customSettings)
    {
        var document = new XmlDocument();
        XmlElement settings = document.CreateElement("Settings");
        settings.AppendChild(SettingsHelper.ToElement(document, "ScriptPath", scriptPath));
        settings.AppendChild(SettingsHelper.ToElement(document, "Start", start));
        settings.AppendChild(SettingsHelper.ToElement(document, "Reset", reset));
        settings.AppendChild(SettingsHelper.ToElement(document, "Split", split));

        XmlElement custom = document.CreateElement("CustomSettings");
        foreach ((string id, bool value) in customSettings)
        {
            XmlElement setting = SettingsHelper.ToElement(document, "Setting", value);
            setting.Attributes.Append(SettingsHelper.ToAttribute(document, "id", id));
            setting.Attributes.Append(SettingsHelper.ToAttribute(document, "type", "bool"));
            custom.AppendChild(setting);
        }

        settings.AppendChild(custom);
        return settings;
    }

    private static ASLSettings CreateCustomSettings(params (string Id, bool DefaultValue, string Label, string Parent)[] settings)
    {
        var aslSettings = new ASLSettings();
        foreach ((string id, bool defaultValue, string label, string parent) in settings)
        {
            aslSettings.AddSetting(id, defaultValue, label, parent);
        }

        return aslSettings;
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

        if (root.ContextMenu is { } contextMenu)
        {
            foreach (object item in contextMenu.Items)
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

        if (root is TreeViewItem treeViewItem && treeViewItem.Header is Control headerControl)
        {
            T match = FindNamed<T>(headerControl, name);
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
}
