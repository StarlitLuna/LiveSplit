using System;

using LiveSplit.Model;
using LiveSplit.Options;

namespace LiveSplit.Web.Share;

public interface IRunUploadPlatform
{
    string PlatformName { get; }
    string Description { get; }
    ISettings Settings { get; set; }

    bool VerifyLogin();
    bool SubmitRun(
        IRun run,
        Func<byte[]> screenShotFunction = null,
        TimingMethod method = TimingMethod.RealTime,
        string comment = "",
        params string[] additionalParams);
}
