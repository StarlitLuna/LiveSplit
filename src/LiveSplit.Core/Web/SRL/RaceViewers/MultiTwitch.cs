using LiveSplit.Model;

namespace LiveSplit.Web.SRL.RaceViewers;

public class MultiTwitch : IRaceViewer
{
    public string Name => "MultiTwitch";

    public void ShowRace(IRaceInfo race)
    {
        string streams = string.Join("/", race.LiveStreams ?? []);
        if (string.IsNullOrEmpty(streams))
        {
            return;
        }

        RaceViewerLauncher.Open($"http://multitwitch.tv/{streams}");
    }
}
