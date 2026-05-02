using System;
using System.Web;

namespace LiveSplit.Web.Share;

public static class TwitchAccessTokenPrompt
{
    internal const string RedirectUrl = "http://livesplit.org/twitch/";

    public static string BuildOAuthUri()
    {
        return string.Format(
            "https://id.twitch.tv/oauth2/authorize?response_type=token&client_id={0}&redirect_uri={1}&scope={2}",
            Twitch.ClientId,
            Uri.EscapeDataString(RedirectUrl),
            Uri.EscapeDataString("channel:manage:broadcast"));
    }

    public static string ExtractAccessToken(string urlOrToken)
    {
        if (string.IsNullOrWhiteSpace(urlOrToken))
        {
            return null;
        }

        string value = urlOrToken.Trim();
        if (!value.Contains("access_token=", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("://", StringComparison.Ordinal))
        {
            return value;
        }

        string queryLike = value;
        int fragmentIndex = queryLike.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            queryLike = queryLike[(fragmentIndex + 1)..];
        }
        else
        {
            int queryIndex = queryLike.IndexOf('?');
            if (queryIndex >= 0)
            {
                queryLike = queryLike[(queryIndex + 1)..];
            }
        }

        var values = HttpUtility.ParseQueryString(queryLike);
        string token = values["access_token"];
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }
}
