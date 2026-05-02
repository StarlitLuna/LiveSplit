using System;
using System.IO;
using System.Xml;

using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.UI.LayoutFactories;
using LiveSplit.UI.LayoutSavers;

using Xunit;

namespace LiveSplit.Tests.UI;

public class LayoutSerializationImageMust
{
    [Fact]
    public void RoundTripBackgroundImageBytes()
    {
        byte[] image = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M/wHwAFAgL/ojzFkgAAAABJRU5ErkJggg==");
        var layout = new Layout
        {
            Mode = LayoutMode.Vertical,
            Settings = new StandardLayoutSettingsFactory().Create()
        };
        layout.Settings.BackgroundType = BackgroundType.Image;
        layout.Settings.BackgroundImage = image;

        using var stream = new MemoryStream();
        new XMLLayoutSaver().Save(layout, stream);
        stream.Position = 0;

        var doc = new XmlDocument();
        doc.Load(stream);
        XmlNode node = doc.SelectSingleNode("//BackgroundImage");
        Assert.NotNull(node);
        Assert.False(string.IsNullOrWhiteSpace(node.InnerText));

        stream.Position = 0;
        ILayout loaded = new XMLLayoutFactory(stream).Create(null);

        Assert.Equal(image, loaded.Settings.BackgroundImage);
    }

    [Fact]
    public void PreserveLegacyBackgroundImageBlob()
    {
        byte[] legacyBlob = { 0, 1, 2, 3, 4, 5 };
        string base64 = Convert.ToBase64String(legacyBlob);
        var document = new XmlDocument();
        XmlElement parent = document.CreateElement("Settings");
        document.AppendChild(parent);

        SettingsHelper.CreateSetting(document, parent, "BackgroundImage", SettingsHelper.GetImageFromElement(CreateElement(base64)));

        XmlElement node = parent["BackgroundImage"];
        Assert.NotNull(node);
        Assert.Equal(base64, node.InnerText);
    }

    private static XmlElement CreateElement(string innerText)
    {
        var document = new XmlDocument();
        XmlElement element = document.CreateElement("BackgroundImage");
        element.InnerText = innerText;
        return element;
    }
}
