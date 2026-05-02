using System.Diagnostics;

namespace LiveSplit.Web.SRL.RaceViewers;

internal static class RaceViewerLauncher
{
    public static void Open(string url)
    {
        Process.Start(new ProcessStartInfo(url)
        {
            UseShellExecute = true
        });
    }
}
