using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input.Platform;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Platform.Storage;

using LiveSplit.Model;
using LiveSplit.Model.RunSavers;
using LiveSplit.Options;
using LiveSplit.Web;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>
/// Avalonia-native share dialog. Generates run screenshots via Skia (no System.Drawing
/// dependency), and offers four destinations that are functional on Linux:
///   - Save Screenshot…   → PNG file via Avalonia storage picker
///   - Tweet…             → opens browser to twitter.com/intent/tweet (no clipboard image)
///   - Upload to Imgur    → multipart POST via HttpClient; URL copied via Avalonia clipboard
///   - Export to Excel    → calls <see cref="ExcelRunSaver"/> against the run
///
/// Paths that require Twitch OAuth or speedrun.com credentials are surfaced as info
/// messages — those flows still depend on WinForms-based prompt dialogs and are tracked
/// as follow-up work.
/// </summary>
public sealed class ShareRunDialog : Window
{
    private const string ImgurClientId = "63e6ae2de8601ef";

    private readonly Func<byte[]> _screenshotPng;
    private readonly LiveSplitState _state;
    private readonly TaskCompletionSource<bool> _result = new();
    private TextBlock _statusBlock;

    public ShareRunDialog(LiveSplitState state, ISettings _, Func<byte[]> screenshotPng)
    {
        _state = state;
        _screenshotPng = screenshotPng;

        Title = "Share Run";
        Width = 480;
        Height = 320;
        CanResize = false;

        var msg = new TextBlock
        {
            Text = "Share or export the current run.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
        };

        Button save = MakeAction("Save Screenshot…", SaveScreenshot);
        Button tweet = MakeAction("Tweet…", Tweet);
        Button imgur = MakeAction("Upload to Imgur", UploadToImgur);
        Button excel = MakeAction("Export to Excel…", ExportExcel);

        _statusBlock = new TextBlock { Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap };

        var close = new Button { Content = "Close", Width = 80, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 12, 12) };
        close.Click += (_, _) => { _result.TrySetResult(false); Close(); };

        var stack = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 6,
            Children = { msg, save, tweet, imgur, excel, _statusBlock },
        };

        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(close, Dock.Bottom);
        root.Children.Add(close);
        root.Children.Add(stack);
        Content = root;

        Closed += (_, _) =>
        {
            if (!_result.Task.IsCompleted)
            {
                _result.TrySetResult(false);
            }
        };
    }

    private Button MakeAction(string label, Func<Task> handler)
    {
        var btn = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Stretch };
        btn.Click += async (_, _) =>
        {
            try
            {
                await handler();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                SetStatus($"Failed: {ex.Message}");
            }
        };
        return btn;
    }

    private void SetStatus(string text)
    {
        if (_statusBlock != null)
        {
            _statusBlock.Text = text;
        }
    }

    private async Task SaveScreenshot()
    {
        byte[] png = _screenshotPng?.Invoke();
        if (png is null)
        {
            SetStatus("No screenshot is available yet.");
            return;
        }

        IStorageFile file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Screenshot",
            DefaultExtension = "png",
            SuggestedFileName = "livesplit.png",
        });
        if (file is null)
        {
            return;
        }

        await using Stream stream = await file.OpenWriteAsync();
        await stream.WriteAsync(png);
        SetStatus($"Saved to {file.Path?.LocalPath ?? file.Name}.");
    }

    private async Task Tweet()
    {
        string title = BuildRunTitle();
        var uri = new Uri("https://twitter.com/intent/tweet?text=" + Uri.EscapeDataString(title));
        Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
        SetStatus("Opened browser to Twitter compose. (Screenshot upload via Twitter's API requires OAuth and is not supported here.)");
        await Task.CompletedTask;
    }

    private async Task UploadToImgur()
    {
        byte[] png = _screenshotPng?.Invoke();
        if (png is null)
        {
            SetStatus("No screenshot is available yet.");
            return;
        }

        SetStatus("Uploading to Imgur…");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Client-ID", ImgurClientId);

        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(png);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "splits.png");
        content.Add(new StringContent(BuildRunTitle()), "title");

        HttpResponseMessage resp = await http.PostAsync("https://api.imgur.com/3/image", content);
        string body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            SetStatus($"Imgur upload failed: {resp.StatusCode}");
            return;
        }

        try
        {
            dynamic json = JSON.FromString(body);
            string id = json?.data?.id?.ToString();
            if (!string.IsNullOrEmpty(id))
            {
                string url = "https://imgur.com/" + id;
                IClipboard clipboard = Clipboard;
                if (clipboard is not null)
                {
                    await clipboard.SetTextAsync(url);
                }

                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                SetStatus($"Uploaded: {url} (URL copied to clipboard).");
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        SetStatus("Imgur upload succeeded but the response did not include an image id.");
    }

    private async Task ExportExcel()
    {
        IStorageFile file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export to Excel",
            DefaultExtension = "xlsx",
            SuggestedFileName = $"{_state.Run.GameName}-{_state.Run.CategoryName}.xlsx",
        });
        if (file is null)
        {
            return;
        }

        await using Stream stream = await file.OpenWriteAsync();
        new ExcelRunSaver().Save(_state.Run, stream);
        SetStatus($"Exported to {file.Path?.LocalPath ?? file.Name}.");
    }

    private string BuildRunTitle()
    {
        IRun run = _state.Run;
        bool hasGame = !string.IsNullOrEmpty(run.GameName);
        bool hasCategory = !string.IsNullOrEmpty(run.CategoryName);
        if (hasGame && hasCategory)
        {
            return $"{run.GameName} - {run.CategoryName}";
        }

        if (hasGame)
        {
            return run.GameName;
        }

        return hasCategory ? run.CategoryName : "LiveSplit run";
    }

    public async Task<bool> ShowDialogAsync(Window owner)
    {
        if (owner is not null)
        {
            await ShowDialog(owner);
        }
        else
        {
            Show();
        }

        return await _result.Task;
    }
}
