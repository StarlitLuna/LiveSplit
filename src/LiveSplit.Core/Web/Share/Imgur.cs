using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.TimeFormatters;

namespace LiveSplit.Web.Share;

public class Imgur : IRunUploadPlatform
{
    private const string ClientId = "63e6ae2de8601ef";

    protected static readonly Imgur _Instance = new();

    protected Imgur()
    {
    }

    public static Imgur Instance => _Instance;
    public string PlatformName => "Imgur";
    public string Description => "Sharing to Imgur uploads a screenshot of LiveSplit and returns the public URL.";
    public ISettings Settings { get; set; }

    public bool VerifyLogin()
    {
        return true;
    }

    public bool SubmitRun(
        IRun run,
        Func<byte[]> screenShotFunction = null,
        TimingMethod method = TimingMethod.RealTime,
        string comment = "",
        params string[] additionalParams)
    {
        return SubmitRunAsync(run, screenShotFunction, method, comment).GetAwaiter().GetResult().Success;
    }

    public async Task<ImgurUploadResult> SubmitRunAsync(
        IRun run,
        Func<byte[]> screenShotFunction = null,
        TimingMethod method = TimingMethod.RealTime,
        string comment = "")
    {
        byte[] image = screenShotFunction?.Invoke();
        if (image is null || image.Length == 0)
        {
            return new ImgurUploadResult(false, null, "No screenshot is available.");
        }

        string title = BuildTitle(run, method);
        dynamic result = await UploadImageAsync(image, title, comment);
        string id = result?.data?.id?.ToString();
        if (string.IsNullOrEmpty(id))
        {
            return new ImgurUploadResult(false, null, "Imgur response did not include an image id.");
        }

        return new ImgurUploadResult(true, "https://imgur.com/" + id, null);
    }

    public async Task<dynamic> UploadImageAsync(byte[] png, string title = "", string description = "")
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Client-ID", ClientId);

        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(png);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "splits.png");
        content.Add(new StringContent(title ?? string.Empty), "title");
        content.Add(new StringContent(description ?? string.Empty), "description");

        using HttpResponseMessage response = await http.PostAsync("https://api.imgur.com/3/image", content);
        string body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new IOException($"Imgur upload failed: {response.StatusCode}");
        }

        return JSON.FromString(body);
    }

    public static string BuildTitle(IRun run, TimingMethod method)
    {
        var titleBuilder = new StringBuilder();
        string time = new RegularTimeFormatter(TimeAccuracy.Seconds)
            .Format(run.Last().PersonalBestSplitTime[method]);

        titleBuilder.Append(time);

        bool gameNameEmpty = string.IsNullOrEmpty(run.GameName);
        bool categoryEmpty = string.IsNullOrEmpty(run.CategoryName);
        if (titleBuilder.Length > 0 && (!gameNameEmpty || !categoryEmpty))
        {
            titleBuilder.Append(" in ");
        }

        titleBuilder.Append(run.GameName);
        if (!gameNameEmpty && !categoryEmpty)
        {
            titleBuilder.Append(" - ");
        }

        titleBuilder.Append(run.CategoryName);
        return titleBuilder.ToString();
    }
}

public readonly record struct ImgurUploadResult(bool Success, string Url, string ErrorMessage);
