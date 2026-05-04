using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;

using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.UI;

namespace LiveSplit.AutoSplittingRuntime;

public class ComponentSettings
{
    private string scriptPath;
    public string ScriptPath
    {
        get => scriptPath;
        set
        {
            if (value != scriptPath)
            {
                scriptPath = value;
                ReloadRuntime(null);
            }
        }
    }
    private readonly bool fixedScriptPath = false;

    public Runtime runtime = null;

    public SettingsMap previousMap = null;
    public Widgets previousWidgets = null;
    private StackPanel runtimeSettingsPanel;

    private static readonly LogDelegate log = (messagePtr, messageLen) =>
    {
        string message = ASRString.FromPtrLen(messagePtr, messageLen);
        Log.Info($"[Auto Splitting Runtime] {message}");
    };

    private readonly StateDelegate getState;
    private readonly IndexDelegate getIndex;
    private readonly SegmentSplittedDelegate segmentSplitted;
    private readonly Action start;
    private readonly Action split;
    private readonly Action skipSplit;
    private readonly Action undoSplit;
    private readonly Action reset;
    private readonly SetGameTimeDelegate setGameTime;
    private readonly Action pauseGameTime;
    private readonly Action resumeGameTime;
    private readonly SetCustomVariableDelegate setCustomVariable;

    public ComponentSettings(TimerModel model)
    {
        scriptPath = "";

        getState = () =>
        {
            return model.CurrentState.CurrentPhase switch
            {
                TimerPhase.NotRunning => 0,
                TimerPhase.Running => 1,
                TimerPhase.Paused => 2,
                TimerPhase.Ended => 3,
                _ => 0,
            };
        };
        getIndex = () => model.CurrentState.CurrentSplitIndex;
        segmentSplitted = (idx) =>
        {
            if (!(0 <= idx && idx < model.CurrentState.CurrentSplitIndex))
            {
                return -1;
            }

            return model.CurrentState.Run[idx].SplitTime.RealTime != null ? 1 : 0;
        };
        start = model.Start;
        split = model.Split;
        skipSplit = model.SkipSplit;
        undoSplit = model.UndoSplit;
        reset = model.Reset;
        setGameTime = (ticks) => model.CurrentState.SetGameTime(new TimeSpan(ticks));
        pauseGameTime = () => model.CurrentState.IsGameTimePaused = true;
        resumeGameTime = () => model.CurrentState.IsGameTimePaused = false;
        setCustomVariable = (namePtr, nameLen, valuePtr, valueLen) =>
        {
            string name = ASRString.FromPtrLen(namePtr, nameLen);
            string value = ASRString.FromPtrLen(valuePtr, valueLen);
            model.CurrentState.Run.Metadata.SetCustomVariable(name, value);
        };
    }

    public ComponentSettings(TimerModel model, string scriptPath)
        : this(model)
    {
        ScriptPath = scriptPath;
        fixedScriptPath = true;
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
            IsEnabled = !fixedScriptPath,
            Margin = new Thickness(0, 2, 6, 2),
        };
        scriptPathBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.TextProperty && !fixedScriptPath)
            {
                ScriptPath = scriptPathBox.Text ?? string.Empty;
            }
        };

        var browseButton = new Button
        {
            Name = "BrowseScriptButton",
            Content = "Browse...",
            Width = 75,
            IsEnabled = !fixedScriptPath,
            Margin = new Thickness(0, 2),
        };
        browseButton.Click += async (_, _) => await BrowseScript(scriptPathBox, browseButton);

        Grid.SetColumn(scriptPathBox, 0);
        Grid.SetColumn(browseButton, 1);
        pathGrid.Children.Add(scriptPathBox);
        pathGrid.Children.Add(browseButton);
        panel.Children.Add(pathGrid);

        runtimeSettingsPanel = new StackPanel
        {
            Name = "RuntimeSettingsPanel",
            Spacing = 5,
        };
        panel.Children.Add(runtimeSettingsPanel);
        RefreshRuntimeSettingsControl();

        return new ScrollViewer { Content = panel };
    }

    public bool RefreshRuntimeSettingsControl()
    {
        if (runtimeSettingsPanel is null)
        {
            return false;
        }

        if (runtime is null)
        {
            runtimeSettingsPanel.Children.Clear();
            return false;
        }

        using SettingsMap settingsMap = runtime.GetSettingsMap();
        using Widgets widgets = runtime.GetSettingsWidgets();
        runtimeSettingsPanel.Children.Clear();
        runtimeSettingsPanel.Children.Add(AsrSettingsControlFactory.BuildRuntimeSettings(
            EnumerateWidgets(widgets, settingsMap), new RuntimeSettingsSink(runtime)));

        previousMap?.Dispose();
        previousMap = runtime.GetSettingsMap();
        previousWidgets?.Dispose();
        previousWidgets = runtime.GetSettingsWidgets();
        return true;
    }

    public void ReloadRuntime(SettingsMap settingsMap)
    {
        try
        {
            if (runtime != null)
            {
                runtime.Dispose();
                runtime = null;
                previousMap?.Dispose();
                previousMap = null;
                previousWidgets?.Dispose();
                previousWidgets = null;
            }

            if (!string.IsNullOrEmpty(ScriptPath))
            {
                runtime = new Runtime(
                    ScriptPath,
                    settingsMap,
                    getState,
                    getIndex,
                    segmentSplitted,
                    start,
                    split,
                    skipSplit,
                    undoSplit,
                    reset,
                    setGameTime,
                    pauseGameTime,
                    resumeGameTime,
                    setCustomVariable,
                    log
                );
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        XmlElement settings_node = document.CreateElement("Settings");

        settings_node.AppendChild(SettingsHelper.ToElement(document, "Version", "1.0"));
        if (!fixedScriptPath)
        {
            settings_node.AppendChild(SettingsHelper.ToElement(document, "ScriptPath", scriptPath));
        }

        AppendCustomSettingsToXml(document, settings_node);

        return settings_node;
    }

    // Loads the settings of this component from Xml. This might happen more than once
    // (e.g. when the settings dialog is cancelled, to restore previous settings).
    public void SetSettings(XmlNode settings)
    {
        var element = (XmlElement)settings;
        if (!element.IsEmpty)
        {
            SettingsMap settingsMap = ParseCustomSettingsFromXml(element);
            if (!fixedScriptPath)
            {
                string newScriptPath = SettingsHelper.ParseString(element["ScriptPath"], string.Empty);
                if (newScriptPath != scriptPath)
                {
                    scriptPath = newScriptPath;
                    ReloadRuntime(settingsMap);
                    return;
                }
            }

            if (runtime != null)
            {
                SettingsMap prev = previousMap;
                previousMap = settingsMap ?? new SettingsMap();
                prev?.Dispose();
                runtime.SetSettingsMap(previousMap);
                return;
            }

            settingsMap?.Dispose();
        }
    }

    private void AppendCustomSettingsToXml(XmlDocument document, XmlNode parent)
    {
        XmlElement asrParent = document.CreateElement("CustomSettings");

        if (runtime != null)
        {
            using SettingsMap settingsMap = runtime.GetSettingsMap();
            if (settingsMap != null)
            {
                BuildMap(document, asrParent, settingsMap);
            }
        }

        parent.AppendChild(asrParent);
    }

    private static void BuildMap(XmlDocument document, XmlElement parent, SettingsMapRef settingsMap)
    {
        ulong len = settingsMap.GetLength();
        for (ulong i = 0; i < len; i++)
        {
            XmlElement element = BuildValue(document, settingsMap.GetValue(i));

            if (element != null)
            {
                XmlAttribute id = SettingsHelper.ToAttribute(document, "id", settingsMap.GetKey(i));
                element.Attributes.Prepend(id);
                parent.AppendChild(element);
            }
        }
    }

    private static void BuildList(XmlDocument document, XmlElement parent, SettingsListRef settingsList)
    {
        ulong len = settingsList.GetLength();
        for (ulong i = 0; i < len; i++)
        {
            XmlElement element = BuildValue(document, settingsList.Get(i));

            if (element != null)
            {
                parent.AppendChild(element);
            }
        }
    }

    private static XmlElement BuildValue(XmlDocument document, SettingValueRef value)
    {
        XmlElement element = document.CreateElement("Setting");

        string type = value.GetKind();

        XmlAttribute typeAttr = SettingsHelper.ToAttribute(document, "type", type);
        element.Attributes.Append(typeAttr);

        switch (type)
        {
            case "map":
            {
                BuildMap(document, element, value.GetMap());
                break;
            }
            case "list":
            {
                BuildList(document, element, value.GetList());
                break;
            }
            case "bool":
            {
                element.InnerText = value.GetBool().ToString(CultureInfo.InvariantCulture);
                break;
            }
            case "i64":
            {
                element.InnerText = value.GetI64().ToString(CultureInfo.InvariantCulture);
                break;
            }
            case "f64":
            {
                element.InnerText = value.GetF64().ToString(CultureInfo.InvariantCulture);
                break;
            }
            case "string":
            {
                XmlAttribute attribute = SettingsHelper.ToAttribute(document, "value", value.GetString());
                element.Attributes.Append(attribute);
                break;
            }
            default:
            {
                return null;
            }
        }

        return element;
    }

    /// <summary>
    /// Parses custom settings, stores them and updates the checked state of already added tree nodes.
    /// </summary>
    private SettingsMap ParseCustomSettingsFromXml(XmlElement data)
    {
        try
        {
            XmlElement customSettingsNode = data["CustomSettings"];

            if (customSettingsNode == null)
            {
                return null;
            }

            return ParseMap(customSettingsNode);
        }
        catch
        {
            return null;
        }
    }

    private SettingsMap ParseMap(XmlElement mapNode)
    {
        var map = new SettingsMap();

        foreach (XmlElement element in mapNode.ChildNodes)
        {
            if (element.Name != "Setting")
            {
                return null;
            }

            string id = element.Attributes["id"].Value;

            if (id == null)
            {
                return null;
            }

            SettingValue value = ParseValue(element);
            if (value == null)
            {
                return null;
            }

            map.Insert(id, value);
        }

        return map;
    }

    private SettingsList ParseList(XmlElement listNode)
    {
        var list = new SettingsList();

        foreach (XmlElement element in listNode.ChildNodes)
        {
            if (element.Name != "Setting")
            {
                return null;
            }

            SettingValue value = ParseValue(element);
            if (value == null)
            {
                return null;
            }

            list.Push(value);
        }

        return list;
    }

    private SettingValue ParseValue(XmlElement element)
    {
        string type = element.Attributes["type"].Value;

        if (type == "bool")
        {
            bool value = SettingsHelper.ParseBool(element);
            return new SettingValue(value);
        }
        else if (type == "i64")
        {
            long value = long.Parse(element.InnerText);
            return new SettingValue(value);
        }
        else if (type == "f64")
        {
            double value = SettingsHelper.ParseDouble(element);
            return new SettingValue(value);
        }
        else if (type == "string")
        {
            string value = element.Attributes["value"].Value;
            return new SettingValue(value);
        }
        else if (type == "map")
        {
            SettingsMap value = ParseMap(element);
            if (value == null)
            {
                return null;
            }

            return new SettingValue(value);
        }
        else if (type == "list")
        {
            SettingsList value = ParseList(element);
            if (value == null)
            {
                return null;
            }

            return new SettingValue(value);
        }
        else
        {
            return null;
        }
    }

    private static IReadOnlyList<AsrWidgetDescriptor> EnumerateWidgets(WidgetsRef widgets, SettingsMapRef settingsMap)
    {
        if (widgets is null || settingsMap is null)
        {
            return [];
        }

        var descriptors = new List<AsrWidgetDescriptor>();
        ulong length = widgets.GetLength();
        for (ulong index = 0; index < length; index++)
        {
            string type = widgets.GetType(index);
            string key = widgets.GetKey(index);
            string description = widgets.GetDescription(index);
            string tooltip = widgets.GetTooltip(index);
            uint headingLevel = widgets.GetHeadingLevel(index);
            switch (type)
            {
                case "bool":
                    descriptors.Add(AsrWidgetDescriptor.Bool(key, description, widgets.GetBool(index, settingsMap), tooltip));
                    break;
                case "title":
                    descriptors.Add(AsrWidgetDescriptor.Title(key, description, headingLevel, tooltip));
                    break;
                case "choice":
                {
                    ulong optionLength = widgets.GetChoiceOptionsLength(index);
                    var optionKeys = new string[optionLength];
                    var optionDescriptions = new string[optionLength];
                    for (ulong optionIndex = 0; optionIndex < optionLength; optionIndex++)
                    {
                        optionKeys[optionIndex] = widgets.GetChoiceOptionKey(index, optionIndex);
                        optionDescriptions[optionIndex] = widgets.GetChoiceOptionDescription(index, optionIndex);
                    }

                    descriptors.Add(AsrWidgetDescriptor.Choice(
                        key, description, optionKeys, optionDescriptions,
                        widgets.GetChoiceCurrentIndex(index, settingsMap), tooltip));
                    break;
                }
                case "file-select":
                {
                    string value = settingsMap.KeyGetValue(key)?.GetString() ?? string.Empty;
                    descriptors.Add(AsrWidgetDescriptor.FileSelect(
                        key, description, widgets.GetFileSelectFilter(index), value, tooltip));
                    break;
                }
            }
        }

        return descriptors;
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
            Title = "Select WASM Auto Splitter",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("WebAssembly Auto Splitter")
                {
                    Patterns = ["*.wasm"],
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

    private sealed class RuntimeSettingsSink : IAsrRuntimeSettingsSink
    {
        private readonly Runtime runtime;

        public RuntimeSettingsSink(Runtime runtime)
        {
            this.runtime = runtime;
        }

        public void SetBool(string key, bool value)
            => runtime.SettingsMapSetBool(key, value);

        public void SetString(string key, string value)
            => runtime.SettingsMapSetString(key, value);
    }
}

internal interface IAsrRuntimeSettingsSink
{
    void SetBool(string key, bool value);
    void SetString(string key, string value);
}

internal sealed record AsrWidgetDescriptor(
    string Type,
    string Key,
    string Description,
    string Tooltip,
    uint HeadingLevel,
    bool BoolValue,
    IReadOnlyList<string> ChoiceKeys,
    IReadOnlyList<string> ChoiceDescriptions,
    ulong ChoiceSelectedIndex,
    string FileSelectFilter,
    string StringValue)
{
    public static AsrWidgetDescriptor Bool(string key, string description, bool value, string tooltip = "")
        => new("bool", key, description, tooltip, 0, value, [], [], 0, string.Empty, string.Empty);

    public static AsrWidgetDescriptor Title(string key, string description, uint headingLevel, string tooltip = "")
        => new("title", key, description, tooltip, headingLevel, false, [], [], 0, string.Empty, string.Empty);

    public static AsrWidgetDescriptor Choice(
        string key,
        string description,
        IReadOnlyList<string> optionKeys,
        IReadOnlyList<string> optionDescriptions,
        ulong selectedIndex,
        string tooltip = "")
        => new("choice", key, description, tooltip, 0, false, optionKeys, optionDescriptions, selectedIndex, string.Empty, string.Empty);

    public static AsrWidgetDescriptor FileSelect(
        string key,
        string description,
        string filter,
        string value = "",
        string tooltip = "")
        => new("file-select", key, description, tooltip, 0, false, [], [], 0, filter, value);
}

internal static class AsrSettingsControlFactory
{
    public static Control BuildRuntimeSettings(IReadOnlyList<AsrWidgetDescriptor> widgets, IAsrRuntimeSettingsSink sink)
    {
        var panel = new StackPanel { Spacing = 5 };
        foreach (AsrWidgetDescriptor widget in widgets)
        {
            panel.Children.Add(BuildWidget(widget, sink));
        }

        return panel;
    }

    private static Control BuildWidget(AsrWidgetDescriptor widget, IAsrRuntimeSettingsSink sink)
        => widget.Type switch
        {
            "bool" => BuildBool(widget, sink),
            "title" => new TextBlock { Text = widget.Description },
            "choice" => BuildChoice(widget, sink),
            "file-select" => BuildFileSelect(widget, sink),
            _ => new TextBlock { Text = widget.Description },
        };

    private static Control BuildBool(AsrWidgetDescriptor widget, IAsrRuntimeSettingsSink sink)
    {
        var checkBox = new CheckBox
        {
            Name = "Widget" + widget.Key + "CheckBox",
            Content = widget.Description,
            IsChecked = widget.BoolValue,
        };
        checkBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == CheckBox.IsCheckedProperty)
            {
                sink.SetBool(widget.Key, checkBox.IsChecked == true);
            }
        };
        return checkBox;
    }

    private static Control BuildChoice(AsrWidgetDescriptor widget, IAsrRuntimeSettingsSink sink)
    {
        var comboBox = new ComboBox
        {
            Name = "Widget" + widget.Key + "ComboBox",
            ItemsSource = widget.ChoiceDescriptions,
            SelectedIndex = (int)Math.Min(widget.ChoiceSelectedIndex, (ulong)Math.Max(0, widget.ChoiceKeys.Count - 1)),
        };
        comboBox.SelectionChanged += (_, _) =>
        {
            if (comboBox.SelectedIndex >= 0 && comboBox.SelectedIndex < widget.ChoiceKeys.Count)
            {
                sink.SetString(widget.Key, widget.ChoiceKeys[comboBox.SelectedIndex]);
            }
        };
        return comboBox;
    }

    private static Control BuildFileSelect(AsrWidgetDescriptor widget, IAsrRuntimeSettingsSink sink)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,81"),
        };
        var textBox = new TextBox
        {
            Name = "Widget" + widget.Key + "PathTextBox",
            Text = widget.StringValue ?? string.Empty,
            Margin = new Thickness(0, 2, 6, 2),
        };
        textBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.TextProperty)
            {
                sink.SetString(widget.Key, textBox.Text ?? string.Empty);
            }
        };
        var browseButton = new Button
        {
            Name = "Widget" + widget.Key + "BrowseButton",
            Content = "Browse...",
            Width = 75,
            Margin = new Thickness(0, 2),
        };
        browseButton.Click += async (_, _) => await BrowseWidgetFile(widget, textBox, browseButton, sink);

        Grid.SetColumn(textBox, 0);
        Grid.SetColumn(browseButton, 1);
        grid.Children.Add(textBox);
        grid.Children.Add(browseButton);
        return grid;
    }

    private static async Task BrowseWidgetFile(AsrWidgetDescriptor widget, TextBox textBox, Control ownerControl, IAsrRuntimeSettingsSink sink)
    {
        TopLevel top = TopLevel.GetTopLevel(ownerControl);
        if (top is null)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = widget.Description,
            AllowMultiple = false,
            FileTypeFilter = ParseFileFilter(widget.FileSelectFilter),
        });

        if (files?.FirstOrDefault()?.Path?.LocalPath is not { Length: > 0 } path)
        {
            return;
        }

        textBox.Text = path;
        sink.SetString(widget.Key, path);
    }

    private static IReadOnlyList<FilePickerFileType> ParseFileFilter(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return [FilePickerFileTypes.All];
        }

        string[] parts = filter.Split('|', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<FilePickerFileType>();
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            result.Add(new FilePickerFileType(parts[i])
            {
                Patterns = parts[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries),
            });
        }

        return result.Count > 0 ? result : [FilePickerFileTypes.All];
    }
}
