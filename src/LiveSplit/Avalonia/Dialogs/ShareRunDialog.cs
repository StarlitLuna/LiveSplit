using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input.Platform;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Platform.Storage;

using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.Web;
using LiveSplit.Web.Share;

namespace LiveSplit.Avalonia.Dialogs;

public sealed class ShareRunDialog : Window
{
    private readonly Func<byte[]> _screenshotPng;
    private readonly LiveSplitState _state;
    private readonly IRun _run;
    private readonly TaskCompletionSource<bool> _result = new();
    private readonly IReadOnlyList<SharePlatformChoice> _platforms;
    private readonly List<Button> _insertButtons = [];
    private ComboBox _platformBox;
    private TextBlock _descriptionBlock;
    private TextBox _notesBox;
    private TextBlock _statusBlock;
    private Button _submitButton;
    private Button _previewButton;

    public ShareRunDialog(LiveSplitState state, ISettings settings, Func<byte[]> screenshotPng)
    {
        _state = state;
        _screenshotPng = screenshotPng;
        _run = SelectRunForSharing(state);
        _platforms = BuildPlatformList();

        Title = "Share Run";
        Width = 560;
        Height = 560;
        MinWidth = 460;
        MinHeight = 440;

        Content = BuildContent();
        RefreshPlatform();

        Closed += (_, _) =>
        {
            if (!_result.Task.IsCompleted)
            {
                _result.TrySetResult(false);
            }
        };
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

    private Control BuildContent()
    {
        _platformBox = new ComboBox
        {
            ItemsSource = _platforms,
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _platformBox.SelectionChanged += (_, _) => RefreshPlatform();

        _descriptionBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray
        };

        _notesBox = new TextBox
        {
            AcceptsReturn = true,
            MinHeight = 120,
            TextWrapping = TextWrapping.Wrap
        };

        var insertPanel = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left
        };
        AddInsertButton(insertPanel, "$game");
        AddInsertButton(insertPanel, "$category");
        AddInsertButton(insertPanel, "$title");
        AddInsertButton(insertPanel, "$pb");
        AddInsertButton(insertPanel, "$splitname");
        AddInsertButton(insertPanel, "$splittime");
        AddInsertButton(insertPanel, "$delta");
        AddInsertButton(insertPanel, "$stream");

        _previewButton = new Button { Content = "Preview", Width = 88 };
        _previewButton.Click += async (_, _) => await Preview();

        _submitButton = new Button { Content = "Submit", Width = 88, IsDefault = true };
        _submitButton.Click += async (_, _) => await Submit();

        var close = new Button { Content = "Close", Width = 88, IsCancel = true };
        close.Click += (_, _) =>
        {
            _result.TrySetResult(false);
            Close();
        };

        var buttonBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0),
            Children = { _previewButton, close, _submitButton }
        };

        _statusBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 8, 0, 0)
        };

        return new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Platform:" },
                _platformBox,
                _descriptionBlock,
                new TextBlock { Text = "Notes:" },
                _notesBox,
                insertPanel,
                _statusBlock,
                buttonBar
            }
        };
    }

    private void AddInsertButton(Panel panel, string token)
    {
        var button = new Button
        {
            Content = token,
            Margin = new Thickness(0, 0, 6, 6),
            MinWidth = 68
        };
        button.Click += (_, _) => Insert(token);
        _insertButtons.Add(button);
        panel.Children.Add(button);
    }

    private IReadOnlyList<SharePlatformChoice> BuildPlatformList()
    {
        var platforms = new List<SharePlatformChoice>
        {
            SharePlatformChoice.Twitter
        };

        if (_state.CurrentPhase is TimerPhase.NotRunning or TimerPhase.Ended && HasPersonalBest(_run))
        {
            platforms.Add(SharePlatformChoice.SpeedrunCom);
        }

        platforms.Add(SharePlatformChoice.Twitch);
        platforms.Add(SharePlatformChoice.Screenshot);
        platforms.Add(SharePlatformChoice.Imgur);
        platforms.Add(SharePlatformChoice.Excel);
        return platforms;
    }

    private static IRun SelectRunForSharing(LiveSplitState state)
    {
        if (state.CurrentPhase != TimerPhase.Ended)
        {
            return state.Run;
        }

        var model = new TimerModel
        {
            CurrentState = state
        };
        model.ResetAndSetAttemptAsPB();
        return state.Run;
    }

    private static bool HasPersonalBest(IRun run)
    {
        return run.LastOrDefault()?.PersonalBestSplitTime.RealTime.HasValue == true;
    }

    private SharePlatformChoice CurrentPlatform => _platformBox.SelectedItem as SharePlatformChoice ?? _platforms[0];

    private void RefreshPlatform()
    {
        SharePlatformChoice platform = CurrentPlatform;
        _descriptionBlock.Text = platform.Description;
        _submitButton.Content = platform.SubmitText;

        bool notesEnabled = platform.SupportsNotes;
        _notesBox.IsEnabled = notesEnabled;
        _previewButton.IsEnabled = notesEnabled;
        foreach (Button button in _insertButtons)
        {
            button.IsEnabled = notesEnabled;
        }

        if (platform == SharePlatformChoice.Twitter)
        {
            _notesBox.Text = ShareNotesFormatter.DefaultTwitterFormat(_state.CurrentPhase);
        }
        else if (platform == SharePlatformChoice.Twitch)
        {
            _notesBox.Text = ShareNotesFormatter.DefaultTwitchFormat();
        }
        else if (platform == SharePlatformChoice.Imgur)
        {
            _notesBox.Text = Imgur.BuildTitle(_run, _state.CurrentTimingMethod);
        }
        else if (platform == SharePlatformChoice.SpeedrunCom)
        {
            _notesBox.Text = string.Empty;
        }
        else
        {
            _notesBox.Text = string.Empty;
        }

        _statusBlock.Text = string.Empty;
    }

    private async Task Submit()
    {
        SharePlatformChoice platform = CurrentPlatform;
        if (platform == SharePlatformChoice.Twitter)
        {
            Twitter.Instance.SubmitRun(_run, method: _state.CurrentTimingMethod, comment: FormatNotes());
            SetStatus("Opened browser to Twitter compose.");
        }
        else if (platform == SharePlatformChoice.Twitch)
        {
            await SubmitToTwitch();
        }
        else if (platform == SharePlatformChoice.Screenshot)
        {
            await SaveScreenshot();
        }
        else if (platform == SharePlatformChoice.Imgur)
        {
            await UploadToImgur();
        }
        else if (platform == SharePlatformChoice.Excel)
        {
            await ExportExcel();
        }
        else if (platform == SharePlatformChoice.SpeedrunCom)
        {
            await SubmitToSpeedrunCom();
        }
    }

    private async Task Preview()
    {
        await new MessageDialog("Preview", FormatNotes()).ShowDialogAsync(this);
    }

    private void Insert(string token)
    {
        int caret = Math.Clamp(_notesBox.CaretIndex, 0, _notesBox.Text?.Length ?? 0);
        _notesBox.Text = (_notesBox.Text ?? string.Empty).Insert(caret, token);
        _notesBox.CaretIndex = caret + token.Length;
        _notesBox.Focus();
    }

    private string FormatNotes()
    {
        string template = _notesBox.Text ?? string.Empty;
        return ShareNotesFormatter.Format(
            _run,
            _state.CurrentPhase,
            _state.CurrentSplitIndex,
            _state.CurrentTimingMethod,
            template,
            ResolveStreamLink(template));
    }

    private static string ResolveStreamLink(string template)
    {
        if (template?.Contains("$stream", StringComparison.Ordinal) != true)
        {
            return string.Empty;
        }

        try
        {
            if (Twitch.Instance.IsLoggedIn || Twitch.Instance.VerifyLogin(false))
            {
                return $"http://twitch.tv/{Twitch.Instance.ChannelName}";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        return string.Empty;
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

    private async Task UploadToImgur()
    {
        byte[] png = _screenshotPng?.Invoke();
        if (png is null)
        {
            SetStatus("No screenshot is available yet.");
            return;
        }

        SetStatus("Uploading to Imgur...");

        try
        {
            ImgurUploadResult result = await Imgur.Instance.SubmitRunAsync(
                _run,
                () => png,
                _state.CurrentTimingMethod,
                FormatNotes());

            if (result.Success)
            {
                IClipboard clipboard = Clipboard;
                if (clipboard is not null)
                {
                    await clipboard.SetTextAsync(result.Url);
                }

                Process.Start(new ProcessStartInfo(result.Url) { UseShellExecute = true });
                SetStatus($"Uploaded: {result.Url} (URL copied to clipboard).");
                return;
            }

            SetStatus(result.ErrorMessage);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            SetStatus($"Imgur upload failed: {ex.Message}");
        }
    }

    private async Task ExportExcel()
    {
        IStorageFile file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export to Excel",
            DefaultExtension = "xlsx",
            SuggestedFileName = $"{_run.GameName}-{_run.CategoryName}.xlsx",
        });
        if (file is null)
        {
            return;
        }

        await using Stream stream = await file.OpenWriteAsync();
        Excel.Instance.Save(_run, stream);
        SetStatus($"Exported to {file.Path?.LocalPath ?? file.Name}.");
    }

    private async Task SubmitToTwitch()
    {
        if (!await EnsureTwitchAuthentication())
        {
            SetStatus("Your login information seems to be incorrect.");
            return;
        }

        try
        {
            bool submitted = Twitch.Instance.SubmitRun(
                _run,
                _screenshotPng,
                _state.CurrentTimingMethod,
                FormatNotes());

            SetStatus(submitted
                ? "Your run was successfully shared to Twitch."
                : "The run could not be shared.");
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            SetStatus("The run could not be shared.");
        }
    }

    private async Task SubmitToSpeedrunCom()
    {
        if (!await EnsureSpeedrunComAuthentication())
        {
            SetStatus("Your login information seems to be incorrect.");
            return;
        }

        if (!SpeedrunCom.ValidateRun(_run, out string reason))
        {
            SetStatus(reason);
            await new MessageDialog("Submitting Failed", reason).ShowDialogAsync(this);
            return;
        }

        var dialog = new SpeedrunComSubmitDialog(_run.Metadata, FormatNotes());
        bool submitted = await dialog.ShowDialogAsync(this);
        SetStatus(submitted
            ? "Your run was successfully shared to Speedrun.com."
            : "The run could not be shared.");
    }

    private async Task<bool> EnsureTwitchAuthentication()
    {
        if (Twitch.Instance.IsLoggedIn || Twitch.Instance.VerifyLogin(false))
        {
            return true;
        }

        OpenBrowser(TwitchAccessTokenPrompt.BuildOAuthUri());
        var input = new TextInputDialog(
            "Twitch Authentication",
            "After completing the OAuth flow, paste the full redirected URL or access token:");
        string token = TwitchAccessTokenPrompt.ExtractAccessToken(await input.ShowDialogAsync(this));
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        WebCredentials.TwitchAccessToken = token;
        Twitch.Instance.ClearAccessToken();
        return Twitch.Instance.VerifyLogin(false);
    }

    private async Task<bool> EnsureSpeedrunComAuthentication()
    {
        try
        {
            if (SpeedrunCom.Client.IsAccessTokenValid)
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        OpenBrowser("https://www.speedrun.com/settings/api");
        var input = new TextInputDialog("Speedrun.com Authentication", "Enter your Speedrun.com API Key:");
        string accessToken = await input.ShowDialogAsync(this);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return false;
        }

        SpeedrunCom.Authenticator = new StaticSpeedrunComAuthenticator(accessToken);
        return SpeedrunCom.MakeSureUserIsAuthenticated();
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private void SetStatus(string text)
    {
        if (_statusBlock != null)
        {
            _statusBlock.Text = text;
        }
    }

    private sealed class StaticSpeedrunComAuthenticator : ISpeedrunComAuthenticator
    {
        private readonly string _accessToken;

        public StaticSpeedrunComAuthenticator(string accessToken)
        {
            _accessToken = accessToken;
        }

        public string GetAccessToken()
        {
            return _accessToken;
        }
    }

    private sealed class SharePlatformChoice
    {
        public static readonly SharePlatformChoice Twitter = new(
            "X (Twitter)",
            "X (Twitter) opens a browser compose window for sharing the run.",
            "Share",
            supportsNotes: true);

        public static readonly SharePlatformChoice SpeedrunCom = new(
            "Speedrun.com",
            "Speedrun.com provides centralized leaderboards for speedrunning.",
            "Submit",
            supportsNotes: true);

        public static readonly SharePlatformChoice Twitch = new(
            "Twitch",
            "Sharing to Twitch updates your stream title and game from the run.",
            "Share",
            supportsNotes: true);

        public static readonly SharePlatformChoice Screenshot = new(
            "Screenshot",
            "Save a screenshot of the current LiveSplit layout.",
            "Save",
            supportsNotes: false);

        public static readonly SharePlatformChoice Imgur = new(
            "Imgur",
            "Upload a screenshot of the current LiveSplit layout and copy the public URL.",
            "Upload",
            supportsNotes: true);

        public static readonly SharePlatformChoice Excel = new(
            "Excel",
            "Export the current splits to an Excel workbook.",
            "Export",
            supportsNotes: false);

        private SharePlatformChoice(string name, string description, string submitText, bool supportsNotes)
        {
            Name = name;
            Description = description;
            SubmitText = submitText;
            SupportsNotes = supportsNotes;
        }

        public string Name { get; }
        public string Description { get; }
        public string SubmitText { get; }
        public bool SupportsNotes { get; }

        public override string ToString()
        {
            return Name;
        }
    }
}
