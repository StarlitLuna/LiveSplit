using System;
using System.IO;

using LiveSplit.Model;
using LiveSplit.Options;

namespace LiveSplit.Web.Share;

public class Screenshot : IRunUploadPlatform
{
    protected static readonly Screenshot _Instance = new();

    protected Screenshot()
    {
    }

    public static Screenshot Instance => _Instance;
    public ISettings Settings { get; set; }
    public string PlatformName => "Screenshot";
    public string Description => "Sharing saves a screenshot of LiveSplit.";

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
        if (additionalParams is null || additionalParams.Length == 0 || string.IsNullOrEmpty(additionalParams[0]))
        {
            return false;
        }

        byte[] image = screenShotFunction?.Invoke();
        if (image is null || image.Length == 0)
        {
            return false;
        }

        File.WriteAllBytes(additionalParams[0], image);
        return true;
    }
}
