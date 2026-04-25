using LiveSplit.Model;

using SpeedrunComSharp;

namespace LiveSplit.Web.Share;

/// <summary>
/// Read-only speedrun.com client wrapper. The original Windows build also drove an interactive
/// run-submission flow from here (SubmitRun, ValidateRun, MakeSureUserIsAuthenticated, etc.);
/// the Avalonia front-end doesn't expose run submission, so only the lookup helpers used by
/// <c>RunMetadata</c>, <c>CompositeGameList</c>, and <c>WorldRecordComponent</c> remain.
/// </summary>
public static class SpeedrunCom
{
    public static SpeedrunComClient Client { get; private set; }

    static SpeedrunCom()
    {
        Client = new SpeedrunComClient(Updates.UpdateHelper.UserAgent, WebCredentials.SpeedrunComAccessToken);
    }

    public static Time ToTime(this RunTimes times)
    {
        var time = new Time(realTime: times.RealTime);

        if (times.GameTime.HasValue)
        {
            time.GameTime = times.GameTime.Value;
        }
        else if (times.RealTimeWithoutLoads.HasValue)
        {
            time.GameTime = times.RealTimeWithoutLoads.Value;
        }

        return time;
    }

    public static Model.TimingMethod ToLiveSplitTimingMethod(this SpeedrunComSharp.TimingMethod timingMethod)
    {
        return timingMethod switch
        {
            SpeedrunComSharp.TimingMethod.RealTime => Model.TimingMethod.RealTime,
            SpeedrunComSharp.TimingMethod.GameTime or SpeedrunComSharp.TimingMethod.RealTimeWithoutLoads => Model.TimingMethod.GameTime,
            _ => throw new System.ArgumentException("timingMethod"),
        };
    }
}
