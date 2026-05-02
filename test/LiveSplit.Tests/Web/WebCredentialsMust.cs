using System.Collections.Generic;

using LiveSplit.Web;

using Xunit;

namespace LiveSplit.Tests.Web;

public class WebCredentialsMust
{
    [Fact]
    public void StoreAndDeleteAllKnownCredentials()
    {
        var store = new MemoryCredentialStore();
        CredentialManager.SetBackendForTesting(store);

        try
        {
            WebCredentials.SpeedrunComAccessToken = "speedrun";
            WebCredentials.TwitchAccessToken = "twitch";
            WebCredentials.SpeedRunsLiveIRCCredentials = "srl";
            WebCredentials.RacetimeAccessToken = "racetime-access";
            WebCredentials.RacetimeRefreshToken = "racetime-refresh";

            Assert.True(WebCredentials.AnyCredentialsExist());
            Assert.Equal("speedrun", WebCredentials.SpeedrunComAccessToken);
            Assert.Equal("twitch", WebCredentials.TwitchAccessToken);
            Assert.Equal("srl", WebCredentials.SpeedRunsLiveIRCCredentials);
            Assert.Equal("racetime-access", WebCredentials.RacetimeAccessToken);
            Assert.Equal("racetime-refresh", WebCredentials.RacetimeRefreshToken);

            WebCredentials.DeleteAllCredentials();

            Assert.False(WebCredentials.AnyCredentialsExist());
            Assert.Null(WebCredentials.SpeedrunComAccessToken);
            Assert.Null(WebCredentials.TwitchAccessToken);
            Assert.Null(WebCredentials.SpeedRunsLiveIRCCredentials);
            Assert.Null(WebCredentials.RacetimeAccessToken);
            Assert.Null(WebCredentials.RacetimeRefreshToken);
        }
        finally
        {
            CredentialManager.ResetBackendForTesting();
        }
    }

    private sealed class MemoryCredentialStore : ICredentialStore
    {
        private readonly Dictionary<string, Credential> _credentials = [];

        public Credential Read(string applicationName)
            => _credentials.TryGetValue(applicationName, out Credential credential) ? credential : null;

        public void Write(string applicationName, string userName, string secret)
            => _credentials[applicationName] = new Credential(CredentialType.Generic, applicationName, userName, secret);

        public void Delete(string applicationName)
            => _credentials.Remove(applicationName);
    }
}
