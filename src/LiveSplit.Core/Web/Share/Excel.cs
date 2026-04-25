using System;
using System.Drawing;

using LiveSplit.Model;
using LiveSplit.Options;

namespace LiveSplit.Web.Share;

/// <summary>
/// Excel export. The Windows path popped a <c>SaveFileDialog</c> from inside
/// <see cref="SubmitRun"/>; on the linux-port the Avalonia front-end picks the destination via
/// its own file picker and calls <see cref="LiveSplit.Model.RunSavers.ExcelRunSaver"/> directly,
/// so this stub implementation is registered only so the share-target list and platform metadata
/// stay in sync.
/// </summary>
public class Excel : IRunUploadPlatform
{
    protected static Excel _Instance = new();

    public static Excel Instance => _Instance;

    public string PlatformName => "Excel";

    public string Description
=> @"Export your splits as an Excel Sheet to analyze your splits.
This includes your whole history of all the runs you ever did.";

    public ISettings Settings { get; set; }

    protected Excel() { }

    bool IRunUploadPlatform.VerifyLogin()
    {
        return true;
    }

    public bool SubmitRun(IRun run, Func<Image> screenShotFunction = null, TimingMethod method = TimingMethod.RealTime, string comment = "", params string[] additionalParams)
    {
        // The host UI handles destination selection now. See ExcelRunSaver for the
        // serialization itself.
        return false;
    }
}
