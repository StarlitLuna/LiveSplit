using System.Diagnostics;

namespace LiveSplit.Web.Share;

/// <summary>
/// On the Windows build this opened a small <c>InputBox</c> WinForms dialog and asked the user
/// to paste their speedrun.com API key. The Avalonia front-end hosts its own input dialog and
/// calls into <c>SpeedrunCom.Client.Authenticator.GetAccessToken()</c> directly with the
/// resulting string, so the dialog isn't needed here. Keeping the class so existing wireup
/// stays compiled — it falls back to launching the API-key page in the user's browser.
/// </summary>
public class SpeedrunComApiKeyPrompt : ISpeedrunComAuthenticator
{
    public string GetAccessToken()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://www.speedrun.com/settings/api",
            UseShellExecute = true,
        });
        return null;
    }
}
