using LiveSplit.Model;

namespace LiveSplit.Web.SRL.RaceViewers;

public class SRLRaceViewer : IRaceViewer
{
    public string Name => "SpeedRunsLive";

    public void ShowRace(IRaceInfo race)
    {
        RaceViewerLauncher.Open($"http://speedrunslive.com/race/{race.Id}");
    }
}
