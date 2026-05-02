using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Web;

using LiveSplit.Model;
using LiveSplit.Options;

namespace LiveSplit.Web.Share;

public class Twitch : IRunUploadPlatform
{
    internal const string ClientId = "lkz3x9qaxaeujde1tvq21r8d7cdr40x";

    protected static readonly Twitch _Instance = new();

    protected string AccessToken { get; set; }

    protected Twitch()
    {
    }

    public static Twitch Instance => _Instance;
    public static readonly Uri BaseUri = new("https://api.twitch.tv/helix/");
    public string ChannelName { get; protected set; }
    public string ChannelId { get; protected set; }
    public bool IsLoggedIn => !string.IsNullOrEmpty(ChannelId);
    public string PlatformName => "Twitch";
    public string Description => "Sharing to Twitch updates your stream title and game from the run.";
    public ISettings Settings { get; set; }

    public bool VerifyLogin()
    {
        return VerifyLogin(true);
    }

    public bool VerifyLogin(bool promptIfNoToken)
    {
        AccessToken = WebCredentials.TwitchAccessToken;
        return VerifyAccessToken();
    }

    public bool VerifyAccessToken()
    {
        if (string.IsNullOrEmpty(AccessToken))
        {
            return false;
        }

        try
        {
            dynamic verificationInfo = CurlAbsolute(new Uri("https://id.twitch.tv/oauth2/validate"));
            ChannelName = verificationInfo.login;
            ChannelId = verificationInfo.user_id;
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        return false;
    }

    public void ClearAccessToken()
    {
        ChannelName = null;
        ChannelId = null;
        AccessToken = null;
    }

    public IEnumerable<TwitchGame> FindGame(string name)
    {
        dynamic result = SearchGame(name);
        var games = (IEnumerable<dynamic>)result.data;
        return games.Select(static x => new TwitchGame(x.name, x.id));
    }

    public dynamic SearchGame(string name)
    {
        return Curl($"search/categories?query={HttpUtility.UrlEncode(name)}");
    }

    public void SetStreamTitleAndGame(string title, TwitchGame game = null)
    {
        Curl(
            $"channels?broadcaster_id={HttpUtility.UrlEncode(ChannelId)}",
            "PATCH",
            "{" +
            $"\"title\":\"{JSON.Escape(title)}\"" +
            (game == null ? string.Empty : $",\"game_id\":\"{JSON.Escape(game.Id)}\"") +
            "}");
    }

    public bool SubmitRun(
        IRun run,
        Func<byte[]> screenShotFunction = null,
        TimingMethod method = TimingMethod.RealTime,
        string comment = "",
        params string[] additionalParams)
    {
        if (!IsLoggedIn && !VerifyLogin(false))
        {
            return false;
        }

        TwitchGame game = FindGame(run.GameName).FirstOrDefault(x => x.Name == run.GameName);
        SetStreamTitleAndGame(comment, game);
        return true;
    }

    protected dynamic Curl(string subUri, string method = "GET", string data = "")
    {
        Uri uri = new(BaseUri, subUri);
        return CurlAbsolute(uri, method, data);
    }

    protected dynamic CurlAbsolute(Uri uri, string method = "GET", string data = "")
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), uri);
        request.Headers.Add("Client-ID", ClientId);
        if (!string.IsNullOrEmpty(AccessToken))
        {
            request.Headers.Add("Authorization", "Bearer " + AccessToken);
        }

        if (!string.IsNullOrEmpty(data))
        {
            request.Content = new StringContent(data, System.Text.Encoding.UTF8, "application/json");
        }

        using var http = new HttpClient();
        using HttpResponseMessage response = http.Send(request);
        response.EnsureSuccessStatusCode();
        using Stream stream = response.Content.ReadAsStream();
        return JSON.FromStream(stream);
    }

    public sealed class TwitchGame
    {
        public TwitchGame(string name, string id)
        {
            Name = name;
            Id = id;
        }

        public string Name { get; }
        public string Id { get; }

        public override string ToString()
        {
            return Name;
        }
    }
}
