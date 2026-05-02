using System.Diagnostics;

namespace LiveSplit.Web.Share;

internal static class PlatformLauncher
{
    public static void Open(string uri)
    {
        Process.Start(new ProcessStartInfo(uri)
        {
            UseShellExecute = true
        });
    }
}
