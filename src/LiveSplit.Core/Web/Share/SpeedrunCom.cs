using System;
using System.Collections.Generic;
using System.Linq;

using LiveSplit.Model;
using LiveSplit.Options;

using SpeedrunComSharp;

namespace LiveSplit.Web.Share;

public static class SpeedrunCom
{
    public static SpeedrunComClient Client { get; private set; }

    public static ISpeedrunComAuthenticator Authenticator { private get; set; }

    static SpeedrunCom()
    {
        Client = new SpeedrunComClient(Updates.UpdateHelper.UserAgent, WebCredentials.SpeedrunComAccessToken);
    }

    public static bool MakeSureUserIsAuthenticated()
    {
        if (Client.IsAccessTokenValid)
        {
            return true;
        }

        string accessToken = Authenticator?.GetAccessToken();
        if (string.IsNullOrEmpty(accessToken))
        {
            return false;
        }

        Client.AccessToken = accessToken;
        bool isTokenValid = Client.IsAccessTokenValid;
        if (isTokenValid)
        {
            try
            {
                WebCredentials.SpeedrunComAccessToken = accessToken;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        return isTokenValid;
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
            _ => throw new ArgumentException("timingMethod"),
        };
    }

    public static void PatchRun(this IRun run, SpeedrunComSharp.Run srdcRun)
    {
        run.GameName = srdcRun.Game.Name;
        run.CategoryName = srdcRun.Category.Name;
        run.Metadata.PlatformName = srdcRun.System.Platform.Name;
        run.Metadata.RegionName = srdcRun.System.Region?.Name;
        run.Metadata.UsesEmulator = srdcRun.System.IsEmulated;
        run.Metadata.VariableValueNames = srdcRun.VariableValues.ToDictionary(x => x.Name, x => x.Value);
        run.Metadata.RunID = srdcRun.ID;
    }

    public static DateTime? FindPersonalBestAttemptDate(IRun run)
    {
        Time runTime = run.Last().PersonalBestSplitTime;

        Attempt attempt = run.AttemptHistory.FirstOrDefault(x =>
            x.Time.GameTime == runTime.GameTime
            && x.Time.RealTime == runTime.RealTime);

        return attempt.Ended?.Time;
    }

    public static string FindMissingMandatoryVariableName(IDictionary<Variable, VariableValue> variableValues)
    {
        return variableValues?
            .FirstOrDefault(x => x.Key?.IsMandatory == true && x.Value == null)
            .Key?
            .Name;
    }

    public static bool ValidateRun(IRun run, out string reasonForRejection)
    {
        try
        {
            RunMetadata metadata = run.Metadata;

            if (!string.IsNullOrEmpty(metadata.RunID))
            {
                reasonForRejection = "This run already exists on speedrun.com.";
                return false;
            }

            if (!MakeSureUserIsAuthenticated())
            {
                reasonForRejection = "You can't submit a run without being authenticated.";
                return false;
            }

            if (string.IsNullOrEmpty(run.GameName))
            {
                reasonForRejection = "You need to specify a game.";
                return false;
            }

            if (metadata.Game == null)
            {
                reasonForRejection = "The game could not be found on speedrun.com.";
                return false;
            }

            if (string.IsNullOrEmpty(run.CategoryName))
            {
                reasonForRejection = "You need to specify a category.";
                return false;
            }

            if (metadata.Category == null)
            {
                reasonForRejection = "The category could not be found on speedrun.com.";
                return false;
            }

            if (metadata.Category.Players.Value > 1 && metadata.Category.Players.Type == PlayersType.Exactly)
            {
                reasonForRejection = "Submitting runs for more than the currently authenticated user is not implemented yet.";
                return false;
            }

            if (metadata.Platform == null)
            {
                reasonForRejection = "You need to specify the platform of the game.";
                return false;
            }

            Time runTime = run.Last().PersonalBestSplitTime;
            if (!runTime.RealTime.HasValue)
            {
                reasonForRejection = "You can't submit a run without a Real Time.";
                return false;
            }

            string missingVariableName = FindMissingMandatoryVariableName(metadata.VariableValues);
            if (!string.IsNullOrEmpty(missingVariableName))
            {
                reasonForRejection = $"You need to specify a value for the variable \"{missingVariableName}\".";
                return false;
            }

            reasonForRejection = null;
            return true;
        }
        catch (Exception ex)
        {
            reasonForRejection = "The run could not be validated for an unknown reason.";
            Log.Error(ex);
            return false;
        }
    }

    public static void ClearAccessToken()
    {
        Client.AccessToken = null;
    }

    public static bool SubmitRun(
        IRun run,
        out string reasonForRejection,
        string comment = null,
        Uri videoUri = null,
        DateTime? date = null,
        TimeSpan? withoutLoads = null)
    {
        try
        {
            RunMetadata metadata = run.Metadata;

            if (!ValidateRun(run, out reasonForRejection))
            {
                return false;
            }

            if (metadata.Game.Ruleset.RequiresVideo && videoUri == null && !metadata.Game.ModeratorUsers.Contains(Client.Profile))
            {
                reasonForRejection = "Runs of this game require a video.";
                return false;
            }

            var timingMethods = metadata.Game.Ruleset.TimingMethods;
            Time runTime = run.Last().PersonalBestSplitTime;

            date ??= FindPersonalBestAttemptDate(run);
            if (date.HasValue && date.Value.ToUniversalTime().Date > DateTime.UtcNow.Date)
            {
                reasonForRejection = "The date of the run can't be in the future.";
                return false;
            }

            if (date.HasValue && metadata.Game.YearOfRelease.HasValue && date.Value.ToUniversalTime().Date.Year < metadata.Game.YearOfRelease)
            {
                reasonForRejection = "The date of the run can't be before the release date of the game.";
                return false;
            }

            try
            {
                string categoryId = metadata.Category.ID;
                string platformId = metadata.Platform.ID;
                string regionId = metadata.Region?.ID;
                TimeSpan? realTime = timingMethods.Contains(SpeedrunComSharp.TimingMethod.RealTime) ? runTime.RealTime : null;
                TimeSpan? realTimeWithoutLoads = timingMethods.Contains(SpeedrunComSharp.TimingMethod.RealTimeWithoutLoads)
                    ? runTime.GameTime
                    : null;

                if (withoutLoads.HasValue)
                {
                    realTimeWithoutLoads = withoutLoads;
                }

                TimeSpan? gameTime = timingMethods.Contains(SpeedrunComSharp.TimingMethod.GameTime) ? runTime.GameTime : null;
                bool emulated = metadata.Game.Ruleset.EmulatorsAllowed && metadata.UsesEmulator;
                var variables = metadata.VariableValues.Values.Where(x => x != null);

                SpeedrunComSharp.Run submittedRun = Client.Runs.Submit(
                    categoryId: categoryId,
                    platformId: platformId,
                    regionId: regionId,
                    realTime: realTime,
                    realTimeWithoutLoads: realTimeWithoutLoads,
                    gameTime: gameTime,
                    comment: comment,
                    videoUri: videoUri,
                    date: date,
                    emulated: emulated,
                    verify: false,
                    variables: variables);

                run.Metadata.Run = submittedRun;
            }
            catch (APIException ex)
            {
                reasonForRejection = string.Join(Environment.NewLine, ex.Errors);
                return false;
            }

            reasonForRejection = null;
            return true;
        }
        catch (Exception ex)
        {
            reasonForRejection = "The run could not be submitted for an unknown reason.";
            Log.Error(ex);
            return false;
        }
    }
}
