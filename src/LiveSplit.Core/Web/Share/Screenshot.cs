using System;

using LiveSplit.Model;
using LiveSplit.Options;

namespace LiveSplit.Web.Share;

/// <summary>
/// Save-screenshot share target. The Windows path popped a <c>SaveFileDialog</c> and called
/// <c>Image.Save</c>. On the linux-port the Avalonia host shows its own file picker and renders
/// via SkiaSharp (<c>SkiaRenderControl.SnapshotPng</c>), so this stub is kept only so the
/// share-target list keeps a "Screenshot" entry that the UI can hand off to.
/// </summary>
public class Screenshot : IRunUploadPlatform
{
    public ISettings Settings { get; set; }
    protected static readonly Screenshot _Instance = new();

    public static Screenshot Instance => _Instance;

    protected Screenshot() { }

    public string PlatformName => "Screenshot";

    public string Description => "Sharing will save a screenshot of LiveSplit.";

    public bool VerifyLogin()
    {
        return true;
    }

    public bool SubmitRun(IRun run, Func<System.Drawing.Image> screenShotFunction = null, TimingMethod method = TimingMethod.RealTime, string comment = "", params string[] additionalParams)
    {
        // The Avalonia front-end captures + saves screenshots directly via SkiaSharp.
        return false;
    }
}
