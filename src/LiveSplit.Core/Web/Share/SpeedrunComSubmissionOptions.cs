using System;
using System.Linq;

using LiveSplit.Model;

namespace LiveSplit.Web.Share;

public static class SpeedrunComSubmissionOptions
{
    public static bool TryNormalizeVideoUri(string videoText, out Uri uri, out string error)
    {
        uri = null;
        error = null;

        if (string.IsNullOrWhiteSpace(videoText))
        {
            return true;
        }

        videoText = videoText.Trim();
        if (!videoText.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            videoText = "http://" + videoText;
        }

        if (videoText.Any(char.IsWhiteSpace)
            || !Uri.TryCreate(videoText, UriKind.Absolute, out uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host)
            || Uri.CheckHostName(uri.Host) == UriHostNameType.Unknown)
        {
            uri = null;
            error = "You didn't provide a valid Video URL.";
            return false;
        }

        return true;
    }

    public static bool TryParseOptionalTime(string text, out TimeSpan? time, out string error)
    {
        time = null;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        try
        {
            time = TimeSpanParser.ParseNullable(text);
            return true;
        }
        catch
        {
            error = "You didn't enter a valid time.";
            return false;
        }
    }

    public static void PatchGameTime(IRun run, TimeSpan? gameTime)
    {
        if (!gameTime.HasValue)
        {
            return;
        }

        ISegment lastSplit = run.Last();
        Time runTime = lastSplit.PersonalBestSplitTime;

        if (runTime.GameTime.HasValue)
        {
            return;
        }

        Attempt attempt = run.AttemptHistory.FirstOrDefault(x =>
            x.Time.GameTime == runTime.GameTime
            && x.Time.RealTime == runTime.RealTime);

        runTime.GameTime = gameTime;

        if (attempt.Time.RealTime.HasValue)
        {
            run.AttemptHistory.Remove(attempt);

            attempt.Time = runTime;

            run.AttemptHistory = [.. run.AttemptHistory.Concat(new[] { attempt }).OrderBy(x => x.Index)];
        }

        lastSplit.PersonalBestSplitTime = runTime;
    }
}
