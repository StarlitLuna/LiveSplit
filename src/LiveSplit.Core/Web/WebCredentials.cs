namespace LiveSplit.Web;

public static class WebCredentials
{
    private const string SpeedrunCom = "LiveSplit_SpeedrunComAccessToken";
    private const string Twitch = "LiveSplit_TwitchAccessToken";
    private const string SpeedRunsLiveIRC = "LiveSplit_SpeedRunsLiveIRCCredentials";
    private const string RacetimeAccess = "LiveSplit_RacetimeAccessToken";
    private const string RacetimeRefresh = "LiveSplit_RacetimeRefreshToken";

    private static readonly string[] AllCredentials =
    [
        SpeedrunCom,
        Twitch,
        SpeedRunsLiveIRC,
        RacetimeAccess,
        RacetimeRefresh,
    ];

    public static string SpeedrunComAccessToken
    {
        get => CredentialManager.ReadCredential(SpeedrunCom)?.Password;
        set => CredentialManager.WriteCredential(SpeedrunCom, "", value);
    }

    public static string TwitchAccessToken
    {
        get => CredentialManager.ReadCredential(Twitch)?.Password;
        set => CredentialManager.WriteCredential(Twitch, "", value);
    }

    public static string SpeedRunsLiveIRCCredentials
    {
        get => CredentialManager.ReadCredential(SpeedRunsLiveIRC)?.Password;
        set => CredentialManager.WriteCredential(SpeedRunsLiveIRC, "", value);
    }

    public static string RacetimeAccessToken
    {
        get => CredentialManager.ReadCredential(RacetimeAccess)?.Password;
        set => CredentialManager.WriteCredential(RacetimeAccess, "", value);
    }

    public static string RacetimeRefreshToken
    {
        get => CredentialManager.ReadCredential(RacetimeRefresh)?.Password;
        set => CredentialManager.WriteCredential(RacetimeRefresh, "", value);
    }

    public static void DeleteAllCredentials()
    {
        foreach (string credential in AllCredentials)
        {
            CredentialManager.DeleteCredential(credential);
        }
    }

    public static bool AnyCredentialsExist()
    {
        foreach (string credential in AllCredentials)
        {
            if (CredentialManager.CredentialExists(credential))
            {
                return true;
            }
        }

        return false;
    }
}
