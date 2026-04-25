using System;
using System.Drawing;
using System.Linq;
using System.Xml;

using LiveSplit.Model;
using LiveSplit.Model.Comparisons;

namespace LiveSplit.UI.Components;

public class GraphSettings : ICloneable
{
    public float GraphHeight { get; set; }
    public float GraphHeightScaled { get => GraphHeight / 5; set => GraphHeight = value * 5; }
    public float GraphWidth { get; set; }
    public float GraphWidthScaled { get => GraphWidth / 10; set => GraphWidth = value * 10; }

    public Color BehindGraphColor { get; set; }
    public Color AheadGraphColor { get; set; }
    public Color GridlinesColor { get; set; }
    public Color PartialFillColorBehind { get; set; }
    public Color CompleteFillColorBehind { get; set; }
    public Color PartialFillColorAhead { get; set; }
    public Color CompleteFillColorAhead { get; set; }
    public Color GraphColor { get; set; }
    public Color GraphGoldColor { get; set; }
    public Color ShadowsColor { get; set; }
    public Color GraphLinesColor { get; set; }

    public bool IsLiveGraph { get; set; }
    public bool FlipGraph { get; set; }
    public bool ShowBestSegments { get; set; }

    public LayoutMode Mode { get; set; }

    public string Comparison { get; set; }
    public LiveSplitState CurrentState { get; set; }

    public GraphSettings()
    {
        GraphHeight = 120;
        GraphWidth = 180;
        BehindGraphColor = Color.FromArgb(115, 40, 40);
        AheadGraphColor = Color.FromArgb(40, 115, 52);
        GridlinesColor = Color.FromArgb(0x50, 0x0, 0x0, 0x0);
        PartialFillColorBehind = Color.FromArgb(25, 255, 255, 255);
        CompleteFillColorBehind = Color.FromArgb(50, 255, 255, 255);
        PartialFillColorAhead = Color.FromArgb(25, 255, 255, 255);
        CompleteFillColorAhead = Color.FromArgb(50, 255, 255, 255);
        GraphColor = Color.White;
        GraphGoldColor = Color.FromArgb(216, 175, 31);
        ShadowsColor = Color.FromArgb(0x38, 0x0, 0x0, 0x0);
        GraphLinesColor = Color.White;
        IsLiveGraph = true;
        FlipGraph = false;
        ShowBestSegments = false;
        Comparison = "Current Comparison";

    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        Version version = SettingsHelper.ParseVersion(element["Version"]);

        GraphHeight = SettingsHelper.ParseFloat(element["Height"]);
        GraphWidth = SettingsHelper.ParseFloat(element["Width"]);
        BehindGraphColor = SettingsHelper.ParseColor(element["BehindGraphColor"]);
        AheadGraphColor = SettingsHelper.ParseColor(element["AheadGraphColor"]);
        GridlinesColor = SettingsHelper.ParseColor(element["GridlinesColor"]);
        FlipGraph = SettingsHelper.ParseBool(element["FlipGraph"], false);
        Comparison = SettingsHelper.ParseString(element["Comparison"], "Current Comparison");
        ShowBestSegments = SettingsHelper.ParseBool(element["ShowBestSegments"], false);
        GraphGoldColor = SettingsHelper.ParseColor(element["GraphGoldColor"], Color.Gold);
        GraphColor = SettingsHelper.ParseColor(element["GraphColor"]);
        ShadowsColor = SettingsHelper.ParseColor(element["ShadowsColor"]);
        GraphLinesColor = SettingsHelper.ParseColor(element["GraphLinesColor"]);
        IsLiveGraph = SettingsHelper.ParseBool(element["LiveGraph"]);

        if (version >= new Version(1, 2))
        {
            PartialFillColorBehind = SettingsHelper.ParseColor(element["PartialFillColorBehind"]);
            CompleteFillColorBehind = SettingsHelper.ParseColor(element["CompleteFillColorBehind"]);
            PartialFillColorAhead = SettingsHelper.ParseColor(element["PartialFillColorAhead"]);
            CompleteFillColorAhead = SettingsHelper.ParseColor(element["CompleteFillColorAhead"]);
        }
        else
        {
            PartialFillColorAhead = SettingsHelper.ParseColor(element["PartialFillColor"]);
            PartialFillColorBehind = SettingsHelper.ParseColor(element["PartialFillColor"]);
            CompleteFillColorAhead = SettingsHelper.ParseColor(element["CompleteFillColor"]);
            CompleteFillColorBehind = SettingsHelper.ParseColor(element["CompleteFillColor"]);
        }
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
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.5") ^
        SettingsHelper.CreateSetting(document, parent, "Height", GraphHeight) ^
        SettingsHelper.CreateSetting(document, parent, "Width", GraphWidth) ^
        SettingsHelper.CreateSetting(document, parent, "BehindGraphColor", BehindGraphColor) ^
        SettingsHelper.CreateSetting(document, parent, "AheadGraphColor", AheadGraphColor) ^
        SettingsHelper.CreateSetting(document, parent, "GridlinesColor", GridlinesColor) ^
        SettingsHelper.CreateSetting(document, parent, "PartialFillColorBehind", PartialFillColorBehind) ^
        SettingsHelper.CreateSetting(document, parent, "CompleteFillColorBehind", CompleteFillColorBehind) ^
        SettingsHelper.CreateSetting(document, parent, "PartialFillColorAhead", PartialFillColorAhead) ^
        SettingsHelper.CreateSetting(document, parent, "CompleteFillColorAhead", CompleteFillColorAhead) ^
        SettingsHelper.CreateSetting(document, parent, "GraphColor", GraphColor) ^
        SettingsHelper.CreateSetting(document, parent, "ShadowsColor", ShadowsColor) ^
        SettingsHelper.CreateSetting(document, parent, "GraphLinesColor", GraphLinesColor) ^
        SettingsHelper.CreateSetting(document, parent, "LiveGraph", IsLiveGraph) ^
        SettingsHelper.CreateSetting(document, parent, "FlipGraph", FlipGraph) ^
        SettingsHelper.CreateSetting(document, parent, "Comparison", Comparison) ^
        SettingsHelper.CreateSetting(document, parent, "ShowBestSegments", ShowBestSegments) ^
        SettingsHelper.CreateSetting(document, parent, "GraphGoldColor", GraphGoldColor);
    }

    public object Clone()
    {
        return new GraphSettings()
        {
            GraphHeight = GraphHeight,
            BehindGraphColor = BehindGraphColor,
            AheadGraphColor = AheadGraphColor,
            GridlinesColor = GridlinesColor,
            PartialFillColorBehind = PartialFillColorBehind,
            CompleteFillColorBehind = CompleteFillColorBehind,
            PartialFillColorAhead = PartialFillColorAhead,
            CompleteFillColorAhead = CompleteFillColorAhead,
            GraphColor = GraphColor,
            ShadowsColor = ShadowsColor,
            GraphLinesColor = GraphLinesColor
        };
    }

}
