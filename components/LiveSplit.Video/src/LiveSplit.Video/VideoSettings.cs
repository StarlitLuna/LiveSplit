using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;

using LiveSplit.Localization;
using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.TimeFormatters;
using LiveSplit.UI;

namespace LiveSplit.Video;

public class VideoSettings
{
    private static string T(string source) => UiLocalizer.Translate(source, LanguageResolver.ResolveCurrentCultureLanguage());

    public string MRL => HttpUtility.UrlPathEncode("file:///" + VideoPath.Replace('\\', '/').Replace("%", "%25"));
    public string VideoPath { get; set; }
    public TimeSpan Offset { get; set; }
    public new float Height { get; set; }
    public new float Width { get; set; }
    public LayoutMode Mode { get; set; }

    protected ITimeFormatter TimeFormatter { get; set; }

    public string OffsetString
    {
        get => TimeFormatter.Format(Offset);
        set
        {
            if (Regex.IsMatch(value, "[^0-9:.,-]"))
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

        TimeFormatter = new ShortTimeFormatter();

        VideoPath = "";
        Width = 200;
        Height = 200;
        Offset = TimeSpan.Zero;

    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        VideoPath = SettingsHelper.ParseString(element["VideoPath"]);
        OffsetString = SettingsHelper.ParseString(element["Offset"]);
        Height = SettingsHelper.ParseFloat(element["Height"]);
        Width = SettingsHelper.ParseFloat(element["Width"]);
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        XmlElement parent = document.CreateElement("Settings");
        CreateSettingsNode(document, parent);
        return parent;
    }

    public int GetSettingsHashCode()
    {
        return CreateSettingsNode(null, null);
    }

    private int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.4") ^
        SettingsHelper.CreateSetting(document, parent, "VideoPath", VideoPath) ^
        SettingsHelper.CreateSetting(document, parent, "Offset", OffsetString) ^
        SettingsHelper.CreateSetting(document, parent, "Height", Height) ^
        SettingsHelper.CreateSetting(document, parent, "Width", Width);
    }

}
