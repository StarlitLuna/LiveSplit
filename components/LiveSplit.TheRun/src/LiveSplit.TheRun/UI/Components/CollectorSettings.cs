using System;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Xml;

namespace LiveSplit.UI.Components;

/// <summary>
/// Data + key-validation logic for the therun.gg uploader. The original WinForms version owned
/// status labels, link buttons, and user-avatar image; that surface is gone on the linux-port.
/// The Avalonia panel reflects on the bool toggles and renders the upload key state via the
/// <see cref="Status"/>/<see cref="ValidatedUsername"/> properties exposed here.
/// </summary>
public class CollectorSettings
{
    public LayoutMode Mode { get; set; }

    private readonly string UploadKeyFile = "Livesplit.TheRun/uploadkey.txt";
    private readonly string UploadKeyFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public string UploadKey => GetUploadKey();
    public string ValidatedUsername { get; private set; }

    public string Path { get; set; } = "";
    public bool IsStatsUploadingEnabled { get; set; } = true;
    public bool IsUploadOnResetEnabled { get; set; } = true;
    public bool IsLiveTrackingEnabled { get; set; } = true;
    public bool IsToastEnabled { get; set; } = true;
    public bool IsLayoutPathUploadEnabled { get; set; }

    public enum ConnectionStatus { None, Validating, Connected, Error }
    public ConnectionStatus Status { get; private set; } = ConnectionStatus.None;

    public CollectorSettings() { }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;

        Path = SettingsHelper.ParseString(element["Path"]);
        IsStatsUploadingEnabled = element["IsStatsUploadingEnabled"] == null || SettingsHelper.ParseBool(element["IsStatsUploadingEnabled"]);
        IsUploadOnResetEnabled = element["IsUploadOnResetEnabled"] == null || SettingsHelper.ParseBool(element["IsUploadOnResetEnabled"]);
        IsLiveTrackingEnabled = element["IsLiveTrackingEnabled"] == null || SettingsHelper.ParseBool(element["IsLiveTrackingEnabled"]);
        IsToastEnabled = element["IsToastEnabled"] == null || SettingsHelper.ParseBool(element["IsToastEnabled"]);
        IsLayoutPathUploadEnabled = element["IsLayoutPathUploadEnabled"] != null && SettingsHelper.ParseBool(element["IsLayoutPathUploadEnabled"]);

        _ = ValidateKeyAsync();
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
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.0.0") ^
            SettingsHelper.CreateSetting(document, parent,
                "IsStatsUploadingEnabled", IsStatsUploadingEnabled) ^
            SettingsHelper.CreateSetting(document, parent,
                "IsUploadOnResetEnabled", IsUploadOnResetEnabled) ^
            SettingsHelper.CreateSetting(document, parent,
                "IsLiveTrackingEnabled", IsLiveTrackingEnabled) ^
            SettingsHelper.CreateSetting(document, parent,
                "IsToastEnabled", IsToastEnabled) ^
            SettingsHelper.CreateSetting(document, parent,
                "IsLayoutPathUploadEnabled", IsLayoutPathUploadEnabled);
    }

    public async Task ValidateKeyAsync()
    {
        string key = GetUploadKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            Status = ConnectionStatus.None;
            ValidatedUsername = null;
            return;
        }

        Status = ConnectionStatus.Validating;
        try
        {
            string url = "https://api.therun.gg/users/uploadKey/validate/" + key;

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            HttpResponseMessage result = await httpClient.GetAsync(url);
            string body = await result.Content.ReadAsStringAsync();

            if (result.IsSuccessStatusCode)
            {
                JsonNode json = JsonNode.Parse(body);
                JsonNode data = json["result"]["data"];
                string username = (string)data["username"];

                ValidatedUsername = username;
                Status = ConnectionStatus.Connected;
            }
            else
            {
                ValidatedUsername = null;
                Status = ConnectionStatus.Error;
            }
        }
        catch
        {
            ValidatedUsername = null;
            Status = ConnectionStatus.Error;
        }
    }

    public string GetUploadKey()
    {
        try
        {
            string filePath = System.IO.Path.Combine(UploadKeyFolder, UploadKeyFile);
            return File.Exists(filePath) ? File.ReadAllText(filePath).Trim() : "";
        }
        catch
        {
            return "";
        }
    }

    private void SaveUploadKey(string key)
    {
        string filePath = System.IO.Path.Combine(UploadKeyFolder, UploadKeyFile);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath));
        File.WriteAllText(filePath, key);
        Path = "";
    }
}
