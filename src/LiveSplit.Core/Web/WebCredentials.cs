namespace LiveSplit.Web;

public static class WebCredentials
{
    private const string SpeedrunCom = "LiveSplit_SpeedrunComAccessToken";

    public static string SpeedrunComAccessToken
    {
        get => CredentialManager.ReadCredential(SpeedrunCom)?.Password;
        set => CredentialManager.WriteCredential(SpeedrunCom, "", value);
    }

    public static void DeleteAllCredentials()
    {
        CredentialManager.DeleteCredential(SpeedrunCom);
    }

    public static bool AnyCredentialsExist()
    {
        return CredentialManager.CredentialExists(SpeedrunCom);
    }
}
