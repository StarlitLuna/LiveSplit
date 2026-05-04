using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using LiveSplit.Options;

namespace LiveSplit.Web.Share;

public sealed class ShareTemplateSettingsStore
{
    public ShareTemplateSettingsStore(string path, string legacySettingsPath = null)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        LegacySettingsPath = legacySettingsPath;
    }

    public string Path { get; }
    public string LegacySettingsPath { get; }

    public static ShareTemplateSettingsStore CreateDefault()
    {
        return new ShareTemplateSettingsStore(
            System.IO.Path.Combine(UserDataPaths.ConfigDir, "share-templates.xml"),
            ResolveLegacySettingsPath());
    }

    public ShareTemplateSettings Load()
    {
        if (!File.Exists(Path))
        {
            return LoadLegacySettings() ?? ShareTemplateSettings.Default;
        }

        try
        {
            XDocument document = XDocument.Load(Path);
            XElement root = document.Root;
            if (root is null)
            {
                return ShareTemplateSettings.Default;
            }

            return new ShareTemplateSettings
            {
                TwitterCompletedFormat = (string)root.Element("TwitterCompletedFormat")
                    ?? ShareTemplateSettings.DefaultTwitterCompletedFormat,
                TwitterRunningFormat = (string)root.Element("TwitterRunningFormat")
                    ?? ShareTemplateSettings.DefaultTwitterRunningFormat,
                TwitchFormat = (string)root.Element("TwitchFormat")
                    ?? ShareTemplateSettings.DefaultTwitchFormatText,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            Log.Error(ex);
            return ShareTemplateSettings.Default;
        }
    }

    public void Save(ShareTemplateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = new XDocument(
            new XElement(
                "ShareTemplates",
                new XElement("TwitterCompletedFormat", settings.TwitterCompletedFormat ?? string.Empty),
                new XElement("TwitterRunningFormat", settings.TwitterRunningFormat ?? string.Empty),
                new XElement("TwitchFormat", settings.TwitchFormat ?? string.Empty)));

        document.Save(Path);
    }

    private ShareTemplateSettings LoadLegacySettings()
    {
        if (string.IsNullOrEmpty(LegacySettingsPath) || !File.Exists(LegacySettingsPath))
        {
            return null;
        }

        try
        {
            XDocument document = XDocument.Load(LegacySettingsPath);
            XElement shareSettings = document
                .Descendants("LiveSplit.Web.Share.ShareSettings")
                .FirstOrDefault();
            if (shareSettings is null)
            {
                return null;
            }

            string completed = ReadLegacySetting(shareSettings, "TwitterFormat");
            string running = ReadLegacySetting(shareSettings, "TwitterFormatRunning");
            string twitch = ReadLegacySetting(shareSettings, "TwitchFormat");
            if (string.IsNullOrEmpty(completed) && string.IsNullOrEmpty(running) && string.IsNullOrEmpty(twitch))
            {
                return null;
            }

            return new ShareTemplateSettings
            {
                TwitterCompletedFormat = string.IsNullOrEmpty(completed)
                    ? ShareTemplateSettings.DefaultTwitterCompletedFormat
                    : completed,
                TwitterRunningFormat = string.IsNullOrEmpty(running)
                    ? ShareTemplateSettings.DefaultTwitterRunningFormat
                    : running,
                TwitchFormat = string.IsNullOrEmpty(twitch)
                    ? ShareTemplateSettings.DefaultTwitchFormatText
                    : twitch,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            Log.Error(ex);
            return null;
        }
    }

    private static string ReadLegacySetting(XElement shareSettings, string name)
    {
        return (string)shareSettings
            .Elements("setting")
            .FirstOrDefault(x => string.Equals((string)x.Attribute("name"), name, StringComparison.Ordinal))
            ?.Element("value");
    }

    private static string ResolveLegacySettingsPath()
    {
        string legacyPath = System.IO.Path.Combine(UserDataPaths.ConfigDir, "user.config");
        return File.Exists(legacyPath) ? legacyPath : null;
    }
}
