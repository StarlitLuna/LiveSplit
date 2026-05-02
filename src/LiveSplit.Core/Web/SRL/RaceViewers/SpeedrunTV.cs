using LiveSplit.Model;

namespace LiveSplit.Web.SRL.RaceViewers;

public class SpeedrunTV : IRaceViewer
{
    public string Name => "Speedrun.tv";

    public void ShowRace(IRaceInfo race)
    {
        RaceViewerLauncher.Open($"http://speedrun.tv/race:{race.Id}");
    }
}
