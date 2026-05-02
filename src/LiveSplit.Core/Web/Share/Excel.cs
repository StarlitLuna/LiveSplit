using System;
using System.IO;

using LiveSplit.Model;
using LiveSplit.Model.RunSavers;
using LiveSplit.Options;

namespace LiveSplit.Web.Share;

public class Excel : IRunUploadPlatform
{
    protected static readonly Excel _Instance = new();

    protected Excel()
    {
    }

    public static Excel Instance => _Instance;
    public string PlatformName => "Excel";
    public string Description => "Export your splits as an Excel sheet to analyze your run history.";
    public ISettings Settings { get; set; }

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

        Save(run, additionalParams[0]);
        return true;
    }

    public void Save(IRun run, string path)
    {
        using FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write);
        Save(run, stream);
    }

    public void Save(IRun run, Stream stream)
    {
        new ExcelRunSaver().Save(run, stream);
    }
}
