using System;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;

using LiveSplit.Model.Input;

namespace LiveSplit.UI.Components;

public partial class CounterComponentSettings : UserControl
{
    public CounterComponentSettings()
    {
        InitializeComponent();

        // Set default values.
        GlobalHotkeysEnabled = false;
        CounterTextColor = Color.FromArgb(255, 255, 255, 255);
        CounterValueColor = Color.FromArgb(255, 255, 255, 255);
        OverrideTextColor = false;
        BackgroundColor = Color.Transparent;
        BackgroundColor2 = Color.Transparent;
        BackgroundGradient = GradientType.Plain;
        CounterText = "Counter:";
        InitialValue = 0;
        Increment = 1;

        // Hotkey data (preserved for XML round-trip; no longer wired to live hooks).
        IncrementKey = new KeyOrButton(Key.Add);
        DecrementKey = new KeyOrButton(Key.Subtract);
        ResetKey = new KeyOrButton(Key.NumPad0);

        // Set bindings.
        txtCounterText.DataBindings.Add("Text", this, "CounterText");
        numInitialValue.DataBindings.Add("Value", this, "InitialValue");
        numIncrement.DataBindings.Add("Value", this, "Increment");
        chkGlobalHotKeys.DataBindings.Add("Checked", this, "GlobalHotkeysEnabled", false, DataSourceUpdateMode.OnPropertyChanged);
        chkColor.DataBindings.Add("Checked", this, "OverrideTextColor", false, DataSourceUpdateMode.OnPropertyChanged);
        btnColor.DataBindings.Add("BackColor", this, "CounterTextColor", false, DataSourceUpdateMode.OnPropertyChanged);
        btnColor3.DataBindings.Add("BackColor", this, "CounterValueColor", false, DataSourceUpdateMode.OnPropertyChanged);
        btnColor1.DataBindings.Add("BackColor", this, "BackgroundColor", false, DataSourceUpdateMode.OnPropertyChanged);
        btnColor2.DataBindings.Add("BackColor", this, "BackgroundColor2", false, DataSourceUpdateMode.OnPropertyChanged);
        cmbGradientType.DataBindings.Add("SelectedItem", this, "GradientString", false, DataSourceUpdateMode.OnPropertyChanged);

        // Assign event handlers.
        cmbGradientType.SelectedIndexChanged += cmbGradientType_SelectedIndexChanged;
        chkColor.CheckedChanged += chkColor_CheckedChanged;
        chkGlobalHotKeys.CheckedChanged += chkGlobalHotKeys_CheckedChanged;

        Load += CounterSettings_Load;

        UpdateHotkeyDisplayText();
    }

    public bool GlobalHotkeysEnabled { get; set; }

    public Color CounterTextColor { get; set; }
    public Color CounterValueColor { get; set; }
    public bool OverrideTextColor { get; set; }

    public Color BackgroundColor { get; set; }
    public Color BackgroundColor2 { get; set; }
    public GradientType BackgroundGradient { get; set; }
    public string GradientString
    {
        get => BackgroundGradient.ToString();
        set => BackgroundGradient = (GradientType)Enum.Parse(typeof(GradientType), value);
    }

    public string CounterText { get; set; }
    public int InitialValue { get; set; }
    public int Increment { get; set; }

    // Legacy font override — read from old configs, not written or exposed in UI
    public bool OverrideCounterFont { get; set; }
    public FontDescriptor CounterFont { get; set; }

    public KeyOrButton IncrementKey { get; set; }
    public KeyOrButton DecrementKey { get; set; }
    public KeyOrButton ResetKey { get; set; }

    public event EventHandler CounterReinitialiseRequired;
    public event EventHandler IncrementUpdateRequired;

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        GlobalHotkeysEnabled = SettingsHelper.ParseBool(element["GlobalHotkeysEnabled"]);
        CounterTextColor = SettingsHelper.ParseColor(element["CounterTextColor"]);
        CounterValueColor = SettingsHelper.ParseColor(element["CounterValueColor"]);
        OverrideCounterFont = SettingsHelper.ParseBool(element["OverrideCounterFont"]);
        CounterFont = SettingsHelper.GetFontFromElement(element["CounterFont"]);
        OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"]);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"]);
        GradientString = SettingsHelper.ParseString(element["BackgroundGradient"]);
        CounterText = SettingsHelper.ParseString(element["CounterText"]);
        InitialValue = SettingsHelper.ParseInt(element["InitialValue"]);
        Increment = SettingsHelper.ParseInt(element["Increment"]);

        XmlElement incrementElement = element["IncrementKey"];
        IncrementKey = string.IsNullOrEmpty(incrementElement.InnerText) ? null : new KeyOrButton(incrementElement.InnerText);
        XmlElement decrementElement = element["DecrementKey"];
        DecrementKey = string.IsNullOrEmpty(decrementElement.InnerText) ? null : new KeyOrButton(decrementElement.InnerText);
        XmlElement resetElement = element["ResetKey"];
        ResetKey = string.IsNullOrEmpty(resetElement.InnerText) ? null : new KeyOrButton(resetElement.InnerText);

        UpdateHotkeyDisplayText();
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
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.0") ^
        SettingsHelper.CreateSetting(document, parent, "GlobalHotkeysEnabled", GlobalHotkeysEnabled) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
        SettingsHelper.CreateSetting(document, parent, "CounterTextColor", CounterTextColor) ^
        SettingsHelper.CreateSetting(document, parent, "CounterValueColor", CounterValueColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
        SettingsHelper.CreateSetting(document, parent, "CounterText", CounterText) ^
        SettingsHelper.CreateSetting(document, parent, "InitialValue", InitialValue) ^
        SettingsHelper.CreateSetting(document, parent, "Increment", Increment) ^
        SettingsHelper.CreateSetting(document, parent, "IncrementKey", IncrementKey) ^
        SettingsHelper.CreateSetting(document, parent, "DecrementKey", DecrementKey) ^
        SettingsHelper.CreateSetting(document, parent, "ResetKey", ResetKey);
    }

    private void UpdateHotkeyDisplayText()
    {
        txtIncrement.Text = FormatKey(IncrementKey);
        txtDecrement.Text = FormatKey(DecrementKey);
        txtReset.Text = FormatKey(ResetKey);
    }

    private string FormatKey(KeyOrButton key)
    {
        if (key == null)
        {
            return "None";
        }

        string str = key.ToString();
        if (key.IsButton)
        {
            int length = str.LastIndexOf(' ');
            if (length != -1)
            {
                str = str.Substring(0, length);
            }
        }

        return str;
    }

    private void CounterSettings_Load(object sender, EventArgs e)
    {
        chkColor_CheckedChanged(null, null);
    }

    private void ColorButtonClick(object sender, EventArgs e)
    {
        SettingsHelper.ColorButtonClick((Button)sender, this);
    }

    private void chkColor_CheckedChanged(object sender, EventArgs e)
    {
        label3.Enabled = btnColor.Enabled = label5.Enabled = btnColor3.Enabled = chkColor.Checked;
    }

    private void chkGlobalHotKeys_CheckedChanged(object sender, EventArgs e)
    {
        GlobalHotkeysEnabled = chkGlobalHotKeys.Checked;
    }

    private void cmbGradientType_SelectedIndexChanged(object sender, EventArgs e)
    {
        btnColor1.Visible = cmbGradientType.SelectedItem.ToString() != "Plain";
        btnColor2.DataBindings.Clear();
        btnColor2.DataBindings.Add("BackColor", this, btnColor1.Visible ? "BackgroundColor2" : "BackgroundColor", false, DataSourceUpdateMode.OnPropertyChanged);
        GradientString = cmbGradientType.SelectedItem.ToString();
    }

    // Hotkey binding is gone; these stubs remain so the Designer event wires still resolve.
    private void txtIncrement_Enter(object sender, EventArgs e) { }
    private void txtIncrement_KeyDown(object sender, KeyEventArgs e) => e.SuppressKeyPress = true;
    private void txtDecrement_Enter(object sender, EventArgs e) { }
    private void txtDecrement_KeyDown(object sender, KeyEventArgs e) => e.SuppressKeyPress = true;
    private void txtReset_Enter(object sender, EventArgs e) { }
    private void txtReset_KeyDown(object sender, KeyEventArgs e) => e.SuppressKeyPress = true;

    private void numInitialValue_ValueChanged(object sender, EventArgs e)
    {
        InitialValue = (int)Math.Round(numInitialValue.Value, 0);
        CounterReinitialiseRequired(this, EventArgs.Empty);
    }

    private void numIncrement_ValueChanged(object sender, EventArgs e)
    {
        Increment = (int)Math.Round(numIncrement.Value, 0);
        IncrementUpdateRequired(this, EventArgs.Empty);
    }
}
