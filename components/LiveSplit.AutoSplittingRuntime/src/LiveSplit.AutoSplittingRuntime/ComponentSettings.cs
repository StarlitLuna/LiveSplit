using System;
using System.Globalization;
using System.Xml;

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
}
