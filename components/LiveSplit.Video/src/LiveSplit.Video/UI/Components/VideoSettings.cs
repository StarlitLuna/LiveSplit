using System;
using System.Text.RegularExpressions;
using System.Xml;

using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.TimeFormatters;

namespace LiveSplit.UI.Components;

public class VideoSettings
{
    private readonly ITimeFormatter _timeFormatter = new ShortTimeFormatter();

    public string VideoPath { get; set; }
    public TimeSpan Offset { get; set; }
    public float Height { get; set; }
    public float Width { get; set; }
    public LayoutMode Mode { get; set; }

    public string OffsetString
    {
        get => _timeFormatter.Format(Offset);
        set
        {
            if (Regex.IsMatch(value ?? string.Empty, "[^0-9:.,-]"))
            {
                return;
            }

            try
            {
                Offset = TimeSpanParser.Parse(value);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }
    }

    public VideoSettings()
    {
        VideoPath = string.Empty;
        Width = 200;
        Height = 200;
        Offset = TimeSpan.Zero;
    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        VideoPath = SettingsHelper.ParseString(element["VideoPath"]);
        OffsetString = SettingsHelper.ParseString(element["Offset"]);
        Height = SettingsHelper.ParseFloat(element["Height"], 200);
        Width = SettingsHelper.ParseFloat(element["Width"], 200);
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        XmlElement parent = document.CreateElement("Settings");
        CreateSettingsNode(document, parent);
        return parent;
    }

    public int GetSettingsHashCode()
        => CreateSettingsNode(null, null);

    private int CreateSettingsNode(XmlDocument document, XmlElement parent)
        => SettingsHelper.CreateSetting(document, parent, "Version", "1.4") ^
        SettingsHelper.CreateSetting(document, parent, "VideoPath", VideoPath) ^
        SettingsHelper.CreateSetting(document, parent, "Offset", OffsetString) ^
        SettingsHelper.CreateSetting(document, parent, "Height", Height) ^
        SettingsHelper.CreateSetting(document, parent, "Width", Width);
}
