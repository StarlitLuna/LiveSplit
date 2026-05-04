using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;

using LiveSplit.ASL;

namespace LiveSplit.UI.Components;

/// <summary>
/// Data-only port of the original WinForms-hosted scriptable-autosplit settings. The legacy
/// version owned a <c>TreeView</c> of custom-setting checkboxes and a few Start/Reset/Split
/// toggles wired to <c>CheckBox</c> controls; that UI lived in
/// <c>ComponentSettings.Designer.cs</c>, which has been removed for the linux-port.
///
/// The state we round-trip via XML is the same — the basic setting flags and the dictionary of
/// custom setting id → bool — so existing layouts load unchanged. The Avalonia settings panel
/// renders the script-path field via reflection; the per-script custom toggles aren't exposed in
/// the panel yet, but the values still load from / save to disk so re-opening a layout doesn't
/// drop them.
/// </summary>
public class ComponentSettings
{
    public string ScriptPath { get; set; }

    // If true, the next path loaded from settings will be ignored. Set when the constructor
    // receives an explicit script path so the layout's stored ScriptPath doesn't override it.
    private bool _ignore_next_path_setting;

    // Start/Reset/Split toggles — keyed by the lowercased XML element name.
    private readonly Dictionary<string, bool> _basic_settings_state = [];

    // Custom (per-script) settings: id → enabled.
    private Dictionary<string, bool> _custom_settings_state = [];
    private ASLSettings _current_asl_settings;
    private StackPanel _basicSettingsPanel;
    private StackPanel _customSettingsPanel;
    private readonly Dictionary<string, CheckBox> _customCheckBoxes = [];
    private readonly Dictionary<string, TreeViewItem> _customTreeItems = [];
    private bool _refreshing_custom_controls;

    public ComponentSettings()
    {
        ScriptPath = string.Empty;
    }

    public ComponentSettings(string scriptPath) : this()
    {
        ScriptPath = scriptPath;
        _ignore_next_path_setting = true;
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        XmlElement settings_node = document.CreateElement("Settings");

        settings_node.AppendChild(SettingsHelper.ToElement(document, "Version", "1.5"));
        settings_node.AppendChild(SettingsHelper.ToElement(document, "ScriptPath", ScriptPath));
        AppendBasicSettingsToXml(document, settings_node);
        AppendCustomSettingsToXml(document, settings_node);

        return settings_node;
    }

    public void SetSettings(XmlNode settings)
    {
        var element = (XmlElement)settings;
        if (!element.IsEmpty)
        {
            if (!_ignore_next_path_setting)
            {
                ScriptPath = SettingsHelper.ParseString(element["ScriptPath"], string.Empty);
            }

            _ignore_next_path_setting = false;
            ParseBasicSettingsFromXml(element);
            ParseCustomSettingsFromXml(element);
            ApplyCustomSettingsStateToCurrentSettings();
            RefreshSettingsControls();
        }
    }

    /// <summary>
    /// Synchronizes the stored custom-setting state with the script's setting tree. Called by
    /// <see cref="LiveSplit.UI.Components.ASLComponent"/> when the script reloads.
    /// </summary>
    public void SetASLSettings(ASLSettings settings)
    {
        if (string.IsNullOrWhiteSpace(ScriptPath))
        {
            _basic_settings_state.Clear();
            _custom_settings_state.Clear();
        }

        foreach (KeyValuePair<string, ASLSetting> item in settings.BasicSettings)
        {
            if (_basic_settings_state.TryGetValue(item.Key, out bool stored))
            {
                item.Value.Value = stored;
            }
            else
            {
                _basic_settings_state[item.Key] = item.Value.Value;
            }
        }

        var values = new Dictionary<string, bool>();
        foreach (ASLSetting setting in settings.OrderedSettings)
        {
            bool value = _custom_settings_state.TryGetValue(setting.Id, out bool stored)
                ? stored
                : setting.Value;
            setting.Value = value;
            values[setting.Id] = value;
        }

        _custom_settings_state = values;
        _current_asl_settings = settings;
        RefreshSettingsControls();
    }

    public void ResetASLSettings()
    {
        if (string.IsNullOrWhiteSpace(ScriptPath))
        {
            _custom_settings_state.Clear();
        }

        _current_asl_settings = null;
        RefreshSettingsControls();
    }

    public void SetGameVersion(string version)
    {
        // No-op on the linux-port; the original showed the version next to the script-path
        // textbox in the WinForms designer. The Avalonia panel doesn't surface it.
    }

    public Control BuildSettingsControl()
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(7),
            Spacing = 7,
        };

        var pathGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,81"),
        };

        var scriptPathBox = new TextBox
        {
            Name = "ScriptPathTextBox",
            Text = ScriptPath ?? string.Empty,
            Margin = new Thickness(0, 2, 6, 2),
        };
        scriptPathBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.TextProperty)
            {
                ScriptPath = scriptPathBox.Text ?? string.Empty;
            }
        };

        var browseButton = new Button
        {
            Name = "BrowseScriptButton",
            Content = "Browse...",
            Width = 75,
            Margin = new Thickness(0, 2),
        };
        browseButton.Click += async (_, _) => await BrowseScript(scriptPathBox, browseButton);

        Grid.SetColumn(scriptPathBox, 0);
        Grid.SetColumn(browseButton, 1);
        pathGrid.Children.Add(scriptPathBox);
        pathGrid.Children.Add(browseButton);
        panel.Children.Add(pathGrid);

        var basicPanel = new StackPanel
        {
            Name = "BasicSettingsPanel",
            Spacing = 3,
        };
        _basicSettingsPanel = basicPanel;
        AddBasicSettingControls(basicPanel);
        panel.Children.Add(basicPanel);

        var customPanel = new StackPanel
        {
            Name = "CustomSettingsPanel",
            Spacing = 3,
        };
        _customSettingsPanel = customPanel;
        AddCustomSettingControls(customPanel);
        panel.Children.Add(customPanel);

        return new ScrollViewer { Content = panel };
    }

    private void AppendBasicSettingsToXml(XmlDocument document, XmlNode settings_node)
    {
        foreach (KeyValuePair<string, bool> item in _basic_settings_state)
        {
            // Capitalize for the XML element name to match the legacy on-disk format.
            string elementName = char.ToUpperInvariant(item.Key[0]) + item.Key.Substring(1);
            settings_node.AppendChild(SettingsHelper.ToElement(document, elementName, item.Value));
        }
    }

    private void AppendCustomSettingsToXml(XmlDocument document, XmlNode parent)
    {
        XmlElement asl_parent = document.CreateElement("CustomSettings");

        foreach (KeyValuePair<string, bool> setting in _custom_settings_state)
        {
            XmlElement element = SettingsHelper.ToElement(document, "Setting", setting.Value);
            XmlAttribute id = SettingsHelper.ToAttribute(document, "id", setting.Key);
            // In case there are other setting types in the future
            XmlAttribute type = SettingsHelper.ToAttribute(document, "type", "bool");

            element.Attributes.Append(id);
            element.Attributes.Append(type);
            asl_parent.AppendChild(element);
        }

        parent.AppendChild(asl_parent);
    }

    private void ParseBasicSettingsFromXml(XmlElement element)
    {
        foreach (string key in new[] { "Start", "Reset", "Split" })
        {
            if (element[key] != null)
            {
                bool value = bool.Parse(element[key].InnerText);
                _basic_settings_state[key.ToLowerInvariant()] = value;
            }
        }
    }

    private void ParseCustomSettingsFromXml(XmlElement data)
    {
        XmlElement custom_settings_node = data["CustomSettings"];
        if (custom_settings_node == null || !custom_settings_node.HasChildNodes)
        {
            return;
        }

        foreach (XmlElement element in custom_settings_node.ChildNodes)
        {
            if (element.Name != "Setting")
            {
                continue;
            }

            string id = element.Attributes["id"]?.Value;
            string type = element.Attributes["type"]?.Value;

            if (id != null && type == "bool")
            {
                _custom_settings_state[id] = SettingsHelper.ParseBool(element);
            }
        }
    }

    private void AddBasicSettingControls(Panel panel)
    {
        string[] keys = _current_asl_settings?.BasicSettings.Keys.ToArray() ?? ["start", "reset", "split"];
        foreach (string key in keys)
        {
            bool value = _current_asl_settings?.BasicSettings.TryGetValue(key, out ASLSetting basicSetting) == true
                ? basicSetting.Value
                : !_basic_settings_state.TryGetValue(key, out bool stored) || stored;
            _basic_settings_state[key] = value;

            var checkBox = new CheckBox
            {
                Name = "Basic" + char.ToUpperInvariant(key[0]) + key.Substring(1) + "CheckBox",
                Content = char.ToUpperInvariant(key[0]) + key.Substring(1),
                IsChecked = value,
            };
            checkBox.PropertyChanged += (_, args) =>
            {
                if (args.Property == CheckBox.IsCheckedProperty)
                {
                    bool checkedValue = checkBox.IsChecked == true;
                    _basic_settings_state[key] = checkedValue;
                    if (_current_asl_settings?.BasicSettings.TryGetValue(key, out ASLSetting setting) == true)
                    {
                        setting.Value = checkedValue;
                    }
                }
            };
            panel.Children.Add(checkBox);
        }
    }

    private void AddCustomSettingControls(Panel panel)
    {
        _customCheckBoxes.Clear();
        _customTreeItems.Clear();

        if (_current_asl_settings is null)
        {
            return;
        }

        var commandGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            Margin = new Thickness(0, 4, 0, 2),
        };
        commandGrid.Children.Add(CreateCommandButton("CheckAllCustomSettingsButton", "Check All", () => SetAllCustomSettings(true), 0));
        commandGrid.Children.Add(CreateCommandButton("UncheckAllCustomSettingsButton", "Uncheck All", () => SetAllCustomSettings(false), 1));
        commandGrid.Children.Add(CreateCommandButton("ResetAllCustomSettingsButton", "Reset to Default", ResetAllCustomSettings, 2));
        panel.Children.Add(commandGrid);

        var tree = new TreeView
        {
            Name = "CustomSettingsTree",
        };

        Dictionary<string, List<ASLSetting>> childrenByParent = _current_asl_settings.OrderedSettings
            .Where(setting => setting.Parent != null)
            .GroupBy(setting => setting.Parent)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (ASLSetting setting in _current_asl_settings.OrderedSettings)
        {
            bool hasChildren = childrenByParent.ContainsKey(setting.Id);
            TreeViewItem item = CreateCustomSettingTreeItem(setting, hasChildren);
            _customTreeItems[setting.Id] = item;

            if (setting.Parent != null && _customTreeItems.TryGetValue(setting.Parent, out TreeViewItem parent))
            {
                parent.Items.Add(item);
            }
            else
            {
                tree.Items.Add(item);
            }
        }

        panel.Children.Add(tree);
        RefreshCustomSettingEnabledStates();
    }

    private Button CreateCommandButton(string name, string content, Action action, int column)
    {
        var button = new Button
        {
            Name = name,
            Content = content,
            Command = new ActionCommand(action),
            Margin = new Thickness(column == 0 ? 0 : 4, 0, 0, 0),
        };
        Grid.SetColumn(button, column);
        return button;
    }

    private TreeViewItem CreateCustomSettingTreeItem(ASLSetting setting, bool hasChildren)
    {
        var checkBox = new CheckBox
        {
            Name = "CustomSetting" + setting.Id + "CheckBox",
            Content = setting.Label,
            IsChecked = setting.Value,
        };
        if (!string.IsNullOrEmpty(setting.ToolTip))
        {
            ToolTip.SetTip(checkBox, setting.ToolTip);
        }

        checkBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == CheckBox.IsCheckedProperty)
            {
                UpdateCustomSettingFromCheckBox(setting, checkBox);
            }
        };

        _customCheckBoxes[setting.Id] = checkBox;

        return new TreeViewItem
        {
            Name = "CustomSetting" + setting.Id + "TreeItem",
            Header = checkBox,
            IsExpanded = true,
            ContextMenu = CreateCustomSettingContextMenu(setting, hasChildren),
        };
    }

    private ContextMenu CreateCustomSettingContextMenu(ASLSetting setting, bool hasChildren)
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("CustomSetting" + setting.Id + "ExpandTreeMenuItem", "Expand Tree", ExpandTree));
        menu.Items.Add(CreateMenuItem("CustomSetting" + setting.Id + "CollapseTreeMenuItem", "Collapse Tree", CollapseTree));
        menu.Items.Add(CreateMenuItem("CustomSetting" + setting.Id + "CollapseTreeToSelectionMenuItem", "Collapse Tree to Selection", () => CollapseTreeToSelection(setting)));

        if (hasChildren)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateMenuItem("CustomSetting" + setting.Id + "ExpandBranchMenuItem", "Expand Branch", () => ExpandBranch(setting)));
            menu.Items.Add(CreateMenuItem("CustomSetting" + setting.Id + "CollapseBranchMenuItem", "Collapse Branch", () => CollapseBranch(setting)));
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateMenuItem("CustomSetting" + setting.Id + "CheckBranchMenuItem", "Check Branch", () => SetBranch(setting, true)));
            menu.Items.Add(CreateMenuItem("CustomSetting" + setting.Id + "UncheckBranchMenuItem", "Uncheck Branch", () => SetBranch(setting, false)));
            menu.Items.Add(CreateMenuItem("CustomSetting" + setting.Id + "ResetBranchToDefaultMenuItem", "Reset Branch to Default", () => ResetBranch(setting)));
        }
        else
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateMenuItem("CustomSetting" + setting.Id + "ResetSettingToDefaultMenuItem", "Reset Setting to Default", () => SetCustomSettingValue(setting, setting.DefaultValue)));
        }

        return menu;
    }

    private static MenuItem CreateMenuItem(string name, string header, Action action)
        => new()
        {
            Name = name,
            Header = header,
            Command = new ActionCommand(action),
        };

    private void UpdateCustomSettingFromCheckBox(ASLSetting setting, CheckBox checkBox)
    {
        if (_refreshing_custom_controls)
        {
            return;
        }

        if (!IsCustomSettingInteractive(setting))
        {
            SetCheckBoxState(checkBox, setting.Value);
            return;
        }

        SetCustomSettingValue(setting, checkBox.IsChecked == true);
    }

    private void SetAllCustomSettings(bool value)
    {
        if (_current_asl_settings is null)
        {
            return;
        }

        foreach (ASLSetting setting in _current_asl_settings.OrderedSettings)
        {
            setting.Value = value;
            _custom_settings_state[setting.Id] = value;
        }

        RefreshCustomSettingCheckBoxes();
    }

    private void ResetAllCustomSettings()
    {
        if (_current_asl_settings is null)
        {
            return;
        }

        foreach (ASLSetting setting in _current_asl_settings.OrderedSettings)
        {
            setting.Value = setting.DefaultValue;
            _custom_settings_state[setting.Id] = setting.Value;
        }

        RefreshCustomSettingCheckBoxes();
    }

    private void SetBranch(ASLSetting root, bool value)
    {
        foreach (ASLSetting setting in Branch(root))
        {
            setting.Value = value;
            _custom_settings_state[setting.Id] = value;
        }

        RefreshCustomSettingCheckBoxes();
    }

    private void ResetBranch(ASLSetting root)
    {
        foreach (ASLSetting setting in Branch(root))
        {
            setting.Value = setting.DefaultValue;
            _custom_settings_state[setting.Id] = setting.Value;
        }

        RefreshCustomSettingCheckBoxes();
    }

    private void SetCustomSettingValue(ASLSetting setting, bool value)
    {
        setting.Value = value;
        _custom_settings_state[setting.Id] = value;
        RefreshCustomSettingCheckBoxes();
    }

    private IEnumerable<ASLSetting> Branch(ASLSetting root)
    {
        yield return root;

        if (_current_asl_settings is null)
        {
            yield break;
        }

        foreach (ASLSetting child in _current_asl_settings.OrderedSettings.Where(setting => setting.Parent == root.Id))
        {
            foreach (ASLSetting descendant in Branch(child))
            {
                yield return descendant;
            }
        }
    }

    private bool IsCustomSettingInteractive(ASLSetting setting)
    {
        if (_current_asl_settings is null)
        {
            return true;
        }

        string parentId = setting.Parent;
        while (parentId != null)
        {
            if (!_current_asl_settings.Settings.TryGetValue(parentId, out ASLSetting parent))
            {
                return true;
            }

            if (!parent.Value)
            {
                return false;
            }

            parentId = parent.Parent;
        }

        return true;
    }

    private void RefreshSettingsControls()
    {
        if (_basicSettingsPanel != null)
        {
            _basicSettingsPanel.Children.Clear();
            AddBasicSettingControls(_basicSettingsPanel);
        }

        if (_customSettingsPanel != null)
        {
            _customSettingsPanel.Children.Clear();
            AddCustomSettingControls(_customSettingsPanel);
        }
    }

    private void RefreshCustomSettingCheckBoxes()
    {
        if (_current_asl_settings is null)
        {
            return;
        }

        _refreshing_custom_controls = true;
        try
        {
            foreach (ASLSetting setting in _current_asl_settings.OrderedSettings)
            {
                if (_customCheckBoxes.TryGetValue(setting.Id, out CheckBox checkBox))
                {
                    checkBox.IsChecked = setting.Value;
                }
            }
        }
        finally
        {
            _refreshing_custom_controls = false;
        }

        RefreshCustomSettingEnabledStates();
    }

    private void RefreshCustomSettingEnabledStates()
    {
        if (_current_asl_settings is null)
        {
            return;
        }

        foreach (ASLSetting setting in _current_asl_settings.OrderedSettings)
        {
            if (_customCheckBoxes.TryGetValue(setting.Id, out CheckBox checkBox))
            {
                bool enabled = IsCustomSettingInteractive(setting);
                checkBox.IsEnabled = enabled;
                checkBox.Opacity = enabled ? 1.0 : 0.45;
            }
        }
    }

    private void SetCheckBoxState(CheckBox checkBox, bool value)
    {
        _refreshing_custom_controls = true;
        try
        {
            checkBox.IsChecked = value;
        }
        finally
        {
            _refreshing_custom_controls = false;
        }

        RefreshCustomSettingEnabledStates();
    }

    private void ApplyCustomSettingsStateToCurrentSettings()
    {
        if (_current_asl_settings is null)
        {
            return;
        }

        foreach (ASLSetting setting in _current_asl_settings.OrderedSettings)
        {
            if (_custom_settings_state.TryGetValue(setting.Id, out bool value))
            {
                setting.Value = value;
            }
        }
    }

    private void ExpandTree()
    {
        foreach (TreeViewItem item in _customTreeItems.Values)
        {
            item.IsExpanded = true;
        }
    }

    private void CollapseTree()
    {
        foreach (TreeViewItem item in _customTreeItems.Values)
        {
            item.IsExpanded = false;
        }
    }

    private void CollapseTreeToSelection(ASLSetting setting)
    {
        CollapseTree();

        string id = setting.Id;
        while (id != null && _current_asl_settings?.Settings.TryGetValue(id, out ASLSetting current) == true)
        {
            if (_customTreeItems.TryGetValue(current.Id, out TreeViewItem item))
            {
                item.IsExpanded = true;
            }

            id = current.Parent;
        }
    }

    private void ExpandBranch(ASLSetting setting)
    {
        foreach (ASLSetting branchSetting in Branch(setting))
        {
            if (_customTreeItems.TryGetValue(branchSetting.Id, out TreeViewItem item))
            {
                item.IsExpanded = true;
            }
        }
    }

    private void CollapseBranch(ASLSetting setting)
    {
        foreach (ASLSetting branchSetting in Branch(setting))
        {
            if (_customTreeItems.TryGetValue(branchSetting.Id, out TreeViewItem item))
            {
                item.IsExpanded = false;
            }
        }
    }

    private async Task BrowseScript(TextBox scriptPathBox, Control ownerControl)
    {
        TopLevel top = TopLevel.GetTopLevel(ownerControl);
        if (top is null)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Script",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Auto Splitter Scripts")
                {
                    Patterns = ["*.asl"],
                },
                FilePickerFileTypes.All,
            ],
        });

        if (files?.FirstOrDefault()?.Path?.LocalPath is not { Length: > 0 } path)
        {
            return;
        }

        ScriptPath = path;
        scriptPathBox.Text = path;
    }

    private sealed class ActionCommand : ICommand
    {
        private readonly Action _action;

        public ActionCommand(Action action)
        {
            _action = action;
        }

        public event EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            _action();
        }
    }
}
