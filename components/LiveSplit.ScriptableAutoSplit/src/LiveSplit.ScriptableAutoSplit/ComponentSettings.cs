using System.Collections.Generic;
using System.Xml;

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
    }

    public void ResetASLSettings()
    {
        _custom_settings_state.Clear();
    }

    public void SetGameVersion(string version)
    {
        // No-op on the linux-port; the original showed the version next to the script-path
        // textbox in the WinForms designer. The Avalonia panel doesn't surface it.
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
}
