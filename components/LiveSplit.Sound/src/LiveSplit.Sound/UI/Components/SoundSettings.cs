using System;
using System.Xml;

using NAudio.Wave;

namespace LiveSplit.UI.Components;

public class SoundSettings
{
    public string Split { get; set; }
    public string SplitAheadGaining { get; set; }
    public string SplitAheadLosing { get; set; }
    public string SplitBehindGaining { get; set; }
    public string SplitBehindLosing { get; set; }
    public string BestSegment { get; set; }
    public string UndoSplit { get; set; }
    public string SkipSplit { get; set; }
    public string PersonalBest { get; set; }
    public string NotAPersonalBest { get; set; }
    public string Reset { get; set; }
    public string Pause { get; set; }
    public string Resume { get; set; }
    public string StartTimer { get; set; }

    public int OutputDevice { get; set; }

    public int GeneralVolume { get; set; }
    public int SplitVolume { get; set; }
    public int SplitAheadGainingVolume { get; set; }
    public int SplitAheadLosingVolume { get; set; }
    public int SplitBehindGainingVolume { get; set; }
    public int SplitBehindLosingVolume { get; set; }
    public int BestSegmentVolume { get; set; }
    public int UndoSplitVolume { get; set; }
    public int SkipSplitVolume { get; set; }
    public int PersonalBestVolume { get; set; }
    public int NotAPersonalBestVolume { get; set; }
    public int ResetVolume { get; set; }
    public int PauseVolume { get; set; }
    public int ResumeVolume { get; set; }
    public int StartTimerVolume { get; set; }

    public SoundSettings()
    {

        Split =
        SplitAheadGaining =
        SplitAheadLosing =
        SplitBehindGaining =
        SplitBehindLosing =
        BestSegment =
        UndoSplit =
        SkipSplit =
        PersonalBest =
        NotAPersonalBest =
        Reset =
        Pause =
        Resume =
        StartTimer = "";

        OutputDevice = 0;

        GeneralVolume =
        SplitVolume =
        SplitAheadGainingVolume =
        SplitAheadLosingVolume =
        SplitBehindGainingVolume =
        SplitBehindLosingVolume =
        BestSegmentVolume =
        UndoSplitVolume =
        SkipSplitVolume =
        PersonalBestVolume =
        NotAPersonalBestVolume =
        ResetVolume =
        PauseVolume =
        ResumeVolume =
        StartTimerVolume = 100;

        for (int i = 0; i < WaveOut.DeviceCount; ++i)
        {
            cbOutputDevice.Items.Add(WaveOut.GetCapabilities(i));
        }

    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;

        Split = SettingsHelper.ParseString(element["Split"]);
        SplitAheadGaining = SettingsHelper.ParseString(element["SplitAheadGaining"]);
        SplitAheadLosing = SettingsHelper.ParseString(element["SplitAheadLosing"]);
        SplitBehindGaining = SettingsHelper.ParseString(element["SplitBehindGaining"]);
        SplitBehindLosing = SettingsHelper.ParseString(element["SplitBehindLosing"]);
        BestSegment = SettingsHelper.ParseString(element["BestSegment"]);
        UndoSplit = SettingsHelper.ParseString(element["UndoSplit"]);
        SkipSplit = SettingsHelper.ParseString(element["SkipSplit"]);
        PersonalBest = SettingsHelper.ParseString(element["PersonalBest"]);
        NotAPersonalBest = SettingsHelper.ParseString(element["NotAPersonalBest"]);
        Reset = SettingsHelper.ParseString(element["Reset"]);
        Pause = SettingsHelper.ParseString(element["Pause"]);
        Resume = SettingsHelper.ParseString(element["Resume"]);
        StartTimer = SettingsHelper.ParseString(element["StartTimer"]);

        OutputDevice = SettingsHelper.ParseInt(element["OutputDevice"]);

        SplitVolume = SettingsHelper.ParseInt(element["SplitVolume"], 100);
        SplitAheadGainingVolume = SettingsHelper.ParseInt(element["SplitAheadGainingVolume"], 100);
        SplitAheadLosingVolume = SettingsHelper.ParseInt(element["SplitAheadLosingVolume"], 100);
        SplitBehindGainingVolume = SettingsHelper.ParseInt(element["SplitBehindGainingVolume"], 100);
        SplitBehindLosingVolume = SettingsHelper.ParseInt(element["SplitBehindLosingVolume"], 100);
        BestSegmentVolume = SettingsHelper.ParseInt(element["BestSegmentVolume"], 100);
        UndoSplitVolume = SettingsHelper.ParseInt(element["UndoSplitVolume"], 100);
        SkipSplitVolume = SettingsHelper.ParseInt(element["SkipSplitVolume"], 100);
        PersonalBestVolume = SettingsHelper.ParseInt(element["PersonalBestVolume"], 100);
        NotAPersonalBestVolume = SettingsHelper.ParseInt(element["NotAPersonalBestVolume"], 100);
        ResetVolume = SettingsHelper.ParseInt(element["ResetVolume"], 100);
        PauseVolume = SettingsHelper.ParseInt(element["PauseVolume"], 100);
        ResumeVolume = SettingsHelper.ParseInt(element["ResumeVolume"], 100);
        StartTimerVolume = SettingsHelper.ParseInt(element["StartTimerVolume"], 100);
        GeneralVolume = SettingsHelper.ParseInt(element["GeneralVolume"], 100);
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
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.6") ^
        SettingsHelper.CreateSetting(document, parent, "Split", Split) ^
        SettingsHelper.CreateSetting(document, parent, "SplitAheadGaining", SplitAheadGaining) ^
        SettingsHelper.CreateSetting(document, parent, "SplitAheadLosing", SplitAheadLosing) ^
        SettingsHelper.CreateSetting(document, parent, "SplitBehindGaining", SplitBehindGaining) ^
        SettingsHelper.CreateSetting(document, parent, "SplitBehindLosing", SplitBehindLosing) ^
        SettingsHelper.CreateSetting(document, parent, "BestSegment", BestSegment) ^
        SettingsHelper.CreateSetting(document, parent, "UndoSplit", UndoSplit) ^
        SettingsHelper.CreateSetting(document, parent, "SkipSplit", SkipSplit) ^
        SettingsHelper.CreateSetting(document, parent, "PersonalBest", PersonalBest) ^
        SettingsHelper.CreateSetting(document, parent, "NotAPersonalBest", NotAPersonalBest) ^
        SettingsHelper.CreateSetting(document, parent, "Reset", Reset) ^
        SettingsHelper.CreateSetting(document, parent, "Pause", Pause) ^
        SettingsHelper.CreateSetting(document, parent, "Resume", Resume) ^
        SettingsHelper.CreateSetting(document, parent, "StartTimer", StartTimer) ^
        SettingsHelper.CreateSetting(document, parent, "OutputDevice", OutputDevice) ^
        SettingsHelper.CreateSetting(document, parent, "SplitVolume", SplitVolume) ^
        SettingsHelper.CreateSetting(document, parent, "SplitAheadGainingVolume", SplitAheadGainingVolume) ^
        SettingsHelper.CreateSetting(document, parent, "SplitAheadLosingVolume", SplitAheadLosingVolume) ^
        SettingsHelper.CreateSetting(document, parent, "SplitBehindGainingVolume", SplitBehindGainingVolume) ^
        SettingsHelper.CreateSetting(document, parent, "SplitBehindLosingVolume", SplitBehindLosingVolume) ^
        SettingsHelper.CreateSetting(document, parent, "BestSegmentVolume", BestSegmentVolume) ^
        SettingsHelper.CreateSetting(document, parent, "UndoSplitVolume", UndoSplitVolume) ^
        SettingsHelper.CreateSetting(document, parent, "SkipSplitVolume", SkipSplitVolume) ^
        SettingsHelper.CreateSetting(document, parent, "PersonalBestVolume", PersonalBestVolume) ^
        SettingsHelper.CreateSetting(document, parent, "NotAPersonalBestVolume", NotAPersonalBestVolume) ^
        SettingsHelper.CreateSetting(document, parent, "ResetVolume", ResetVolume) ^
        SettingsHelper.CreateSetting(document, parent, "PauseVolume", PauseVolume) ^
        SettingsHelper.CreateSetting(document, parent, "ResumeVolume", ResumeVolume) ^
        SettingsHelper.CreateSetting(document, parent, "StartTimerVolume", StartTimerVolume) ^
        SettingsHelper.CreateSetting(document, parent, "GeneralVolume", GeneralVolume);
    }

    protected string BrowseForPath(TextBox textBox, Action<string> callback)
    {
        string path = textBox.Text;
        var fileDialog = new OpenFileDialog()
        {
            FileName = path,
            Filter = "Audio Files|*.mp3;*.wav;*.aiff;*.wma|All Files|*.*"
        };

        DialogResult result = fileDialog.ShowDialog();

        if (result == DialogResult.OK)
        {
            path = fileDialog.FileName;
        }

        textBox.Text = path;
        callback(path);

        return path;
    }

    private void VolumeTrackBarScrollHandler(object sender, EventArgs e)
    {
        var trackBar = (TrackBar)sender;

        ttVolume.SetToolTip(trackBar, trackBar.Value.ToString());
    }
}
