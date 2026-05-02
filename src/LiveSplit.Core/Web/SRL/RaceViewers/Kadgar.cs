using LiveSplit.Model;

namespace LiveSplit.Web.SRL.RaceViewers;

public class Kadgar : IRaceViewer
{
    public string Name => "Kadgar";

    public void ShowRace(IRaceInfo race)
    {
        string streams = string.Join(",", race.LiveStreams ?? []);
        if (string.IsNullOrEmpty(streams))
        {
            return;
        }

        RaceViewerLauncher.Open($"http://kadgar.net/live/{streams}");
    }
}
