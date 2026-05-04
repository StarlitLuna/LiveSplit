namespace LiveSplit.Web;

public static class WebCredentials
{
    private const string SpeedrunCom = "LiveSplit_SpeedrunComAccessToken";
    private const string Twitch = "LiveSplit_TwitchAccessToken";
    private const string SpeedRunsLiveIRC = "LiveSplit_SpeedRunsLiveIRCCredentials";
    private const string RacetimeAccess = "LiveSplit_RacetimeAccessToken";
    private const string RacetimeRefresh = "LiveSplit_RacetimeRefreshToken";
    private const string LegacySpeedRunsLiveIRC = "LiveSplit_SpeedRunsLiveIRC";
    private const string LegacyRacetimeAccess = "LiveSplit_racetimegg_accesstoken";
    private const string LegacyRacetimeRefresh = "LiveSplit_racetimegg_refreshtoken";

    private static readonly string[] AllCredentials =
    [
        SpeedrunCom,
        Twitch,
        SpeedRunsLiveIRC,
        RacetimeAccess,
        RacetimeRefresh,
        LegacySpeedRunsLiveIRC,
        LegacyRacetimeAccess,
        LegacyRacetimeRefresh,
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
        get => ReadCredentialOrMigrateLegacy(
            SpeedRunsLiveIRC,
            LegacySpeedRunsLiveIRC,
            legacy => string.IsNullOrEmpty(legacy.UserName)
                ? legacy.Password
                : string.IsNullOrEmpty(legacy.Password)
                    ? legacy.UserName
                    : $"{legacy.UserName}:{legacy.Password}");
        set => CredentialManager.WriteCredential(SpeedRunsLiveIRC, "", value);
    }

    public static string RacetimeAccessToken
    {
        get => ReadCredentialOrMigrateLegacy(RacetimeAccess, LegacyRacetimeAccess, legacy => legacy.Password);
        set => CredentialManager.WriteCredential(RacetimeAccess, "", value);
    }

    public static string RacetimeRefreshToken
    {
        get => ReadCredentialOrMigrateLegacy(RacetimeRefresh, LegacyRacetimeRefresh, legacy => legacy.Password);
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

    private static string ReadCredentialOrMigrateLegacy(
        string currentName,
        string legacyName,
        System.Func<Credential, string> readLegacySecret)
    {
        Credential current = CredentialManager.ReadCredential(currentName);
        if (current != null)
        {
            return current.Password;
        }

        Credential legacy = CredentialManager.ReadCredential(legacyName);
        if (legacy == null)
        {
            return null;
        }

        string secret = readLegacySecret(legacy);
        CredentialManager.WriteCredential(currentName, "", secret);
        CredentialManager.DeleteCredential(legacyName);
        return secret;
    }
}
