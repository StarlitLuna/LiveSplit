using System;

using LiveSplit.Model;
using LiveSplit.Options;

namespace LiveSplit.Web.Share;

public class Twitter : IRunUploadPlatform
{
    protected static readonly Twitter _Instance = new();
    public static readonly Uri BaseUri = new("https://twitter.com/intent/tweet");

    protected Twitter()
    {
    }

    public static Twitter Instance => _Instance;
    public ISettings Settings { get; set; }
    public string PlatformName => "X (Twitter)";
    public string Description =>
        "X (Twitter) opens a browser compose window and copies a screenshot of LiveSplit to the clipboard.";

    public bool VerifyLogin()
    {
        return true;
    }

    public bool SubmitRun(
        IRun run,
        Func<byte[]> screenShotFunction = null,
        TimingMethod method = TimingMethod.RealTime,
        string comment = "",
        params string[] additionalParams)
    {
        PlatformLauncher.Open(MakeUri(comment));
        return true;
    }

    public static string MakeUri(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return BaseUri.ToString();
        }

        return BaseUri + "?text=" + Uri.EscapeDataString(text);
    }
}
