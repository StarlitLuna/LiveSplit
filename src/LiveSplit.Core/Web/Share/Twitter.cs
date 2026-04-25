using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using LiveSplit.Model;
using LiveSplit.Options;

namespace LiveSplit.Web.Share;

public class Twitter : IRunUploadPlatform
{
    public ISettings Settings { get; set; }

    protected static readonly Twitter _Instance = new();
    public static Twitter Instance => _Instance;

    public static readonly Uri BaseUri = new("https://twitter.com/intent/tweet");

    protected Twitter() { }

    public string PlatformName => "X (Twitter)";

    public string Description
=> @"X (Twitter) allows you to share your run with the world. 
When sharing, a screenshot of LiveSplit is automatically copied to the clipboard.
When you click share, LiveSplit opens a Tweet composition window in your default browser.";

    public bool VerifyLogin()
    {
        return true;
    }

    public bool SubmitRun(IRun run, Func<Image> screenShotFunction = null, TimingMethod method = TimingMethod.RealTime, string comment = "", params string[] additionalParams)
    {
        ImageToClipboard(screenShotFunction());
        string uri = MakeUri(comment);
        Process.Start(uri);

        return true;
    }

    private void ImageToClipboard(Image image)
    {
        if (image is null)
        {
            return;
        }

        // Image-on-clipboard via System.Windows.Forms.Clipboard.SetDataObject is Windows-only.
        // The Avalonia front-end can copy via TopLevel.Clipboard once we plumb the screenshot
        // bytes through the share-result path.
    }
    private string MakeUri(string text)
    {
        string intentText = "";
        if (!string.IsNullOrEmpty(text))
        {
            intentText = "?text=" + Uri.EscapeDataString(text);
        }

        return BaseUri + intentText;
    }
}
