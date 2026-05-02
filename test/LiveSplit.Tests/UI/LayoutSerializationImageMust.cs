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
        XmlNode legacyNode = doc.SelectSingleNode("//BackgroundImage");
        Assert.NotNull(legacyNode);
        Assert.True(string.IsNullOrWhiteSpace(legacyNode.InnerText));

        XmlNode crossPlatformNode = doc.SelectSingleNode("//BackgroundImageData");
        Assert.NotNull(crossPlatformNode);
        Assert.False(string.IsNullOrWhiteSpace(crossPlatformNode.InnerText));

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

        SettingsHelper.CreateLegacyImageSetting(document, parent, "BackgroundImage", legacyBlob);
        SettingsHelper.CreateSetting(document, parent, "BackgroundImageData", new byte[] { 8, 9 });

        XmlElement node = parent["BackgroundImage"];
        Assert.NotNull(node);
        Assert.Equal(base64, node.InnerText);
        Assert.Equal(Convert.ToBase64String(new byte[] { 8, 9 }), parent["BackgroundImageData"].InnerText);
    }

    [Fact]
    public void PreferCrossPlatformBackgroundImageDataOverLegacyBlob()
    {
        byte[] legacyBlob = { 0, 1, 2, 3, 4, 5 };
        byte[] encodedImage = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M/wHwAFAgL/ojzFkgAAAABJRU5ErkJggg==");

        string xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Layout version="1.6.1">
              <Mode>Vertical</Mode>
              <X>0</X>
              <Y>0</Y>
              <VerticalWidth>200</VerticalWidth>
              <VerticalHeight>100</VerticalHeight>
              <HorizontalWidth>300</HorizontalWidth>
              <HorizontalHeight>80</HorizontalHeight>
              <Settings>
                <TextColor>FFFFFFFF</TextColor>
                <BackgroundColor>00000000</BackgroundColor>
                <BackgroundColor2>00000000</BackgroundColor2>
                <ThinSeparatorsColor>FFFFFFFF</ThinSeparatorsColor>
                <SeparatorsColor>FFFFFFFF</SeparatorsColor>
                <PersonalBestColor>FFFFFFFF</PersonalBestColor>
                <AheadGainingTimeColor>FFFFFFFF</AheadGainingTimeColor>
                <AheadLosingTimeColor>FFFFFFFF</AheadLosingTimeColor>
                <BehindGainingTimeColor>FFFFFFFF</BehindGainingTimeColor>
                <BehindLosingTimeColor>FFFFFFFF</BehindLosingTimeColor>
                <BestSegmentColor>FFFFFFFF</BestSegmentColor>
                <UseRainbowColor>False</UseRainbowColor>
                <NotRunningColor>FFFFFFFF</NotRunningColor>
                <PausedColor>FF7A7A7A</PausedColor>
                <TextOutlineColor>00000000</TextOutlineColor>
                <ShadowsColor>80000000</ShadowsColor>
                <AlwaysOnTop>False</AlwaysOnTop>
                <ShowBestSegments>False</ShowBestSegments>
                <AntiAliasing>True</AntiAliasing>
                <DropShadows>True</DropShadows>
                <BackgroundType>Image</BackgroundType>
                <BackgroundImage>{Convert.ToBase64String(legacyBlob)}</BackgroundImage>
                <BackgroundImageData>{Convert.ToBase64String(encodedImage)}</BackgroundImageData>
                <ImageOpacity>1</ImageOpacity>
                <ImageBlur>0</ImageBlur>
                <Opacity>1</Opacity>
                <MousePassThroughWhileRunning>False</MousePassThroughWhileRunning>
                <AllowResizing>True</AllowResizing>
                <AllowMoving>True</AllowMoving>
              </Settings>
              <Components />
            </Layout>
            """;

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));

        ILayout loaded = new XMLLayoutFactory(stream).Create(null);

        Assert.Equal(encodedImage, loaded.Settings.BackgroundImage);
        Assert.Equal(legacyBlob, loaded.Settings.LegacyBackgroundImage);
    }

    private static XmlElement CreateElement(string innerText)
    {
        var document = new XmlDocument();
        XmlElement element = document.CreateElement("BackgroundImage");
        element.InnerText = innerText;
        return element;
    }
}
