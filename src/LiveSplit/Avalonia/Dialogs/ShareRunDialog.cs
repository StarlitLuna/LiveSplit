using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
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
    private readonly ShareTemplateSettingsStore _templateStore;
    private readonly ShareTemplateSettings _templates;
    private readonly Dictionary<string, Button> _insertButtons = [];
    private ComboBox _platformBox;
    private TextBlock _descriptionBlock;
    private TextBox _notesBox;
    private TextBlock _statusBlock;
    private Button _submitButton;
    private Button _previewButton;

    public ShareRunDialog(LiveSplitState state, ISettings settings, Func<byte[]> screenshotPng)
    {
        _state = CloneStateForSharing(state);
        _screenshotPng = screenshotPng;
        _run = SelectRunForSharing(_state);
        _platforms = BuildPlatformList();
        _templateStore = ShareTemplateSettingsStore.CreateDefault();
        _templates = _templateStore.Load();

        Title = "Run Sharer";
        Width = 560;
        Height = 560;
        MinWidth = 460;
        MinHeight = 440;
        DialogTheme.ApplyWindow(this);

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
            MaxLength = 280,
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
        _insertButtons[token] = button;
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

    internal static LiveSplitState CloneStateForSharing(LiveSplitState state)
    {
        return state?.Clone() as LiveSplitState;
    }

    internal static IRun SelectRunForSharing(LiveSplitState state)
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
        foreach ((string token, Button button) in _insertButtons)
        {
            button.IsEnabled = IsInsertTokenEnabled(token, notesEnabled, _run, _state);
        }

        _notesBox.Text = GetInitialNotesForPlatform(platform.Name, _templates, _state, _run);

        _statusBlock.Text = string.Empty;
    }

    internal static string GetInitialNotesForPlatform(
        string platformName,
        ShareTemplateSettings templates,
        LiveSplitState state,
        IRun run)
    {
        return platformName switch
        {
            "X (Twitter)" => (templates ?? ShareTemplateSettings.Default).GetTwitterFormat(
                state?.CurrentPhase ?? TimerPhase.NotRunning),
            "Twitch" => (templates ?? ShareTemplateSettings.Default).GetTwitchFormat(),
            _ => string.Empty,
        };
    }

    private async Task Submit()
    {
        SharePlatformChoice platform = CurrentPlatform;
        PersistCurrentTemplate(platform);
        if (platform == SharePlatformChoice.Twitter)
        {
            await CopyTwitterScreenshotAsync(
                _screenshotPng,
                png => CopyPngToClipboardAsync(Clipboard, png));

            bool shared = Twitter.Instance.SubmitRun(
                _run,
                null,
                _state.CurrentTimingMethod,
                await FormatNotesAsync());
            await ShowShareResultMessage(shared ? ShareResult.Success : ShareResult.Failure, platform.Name);
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

    internal static async Task<bool> CopyTwitterScreenshotAsync(
        Func<byte[]> screenshotPng,
        Func<byte[], Task> copyPngAsync)
    {
        byte[] png = screenshotPng?.Invoke();
        if (png is null || png.Length == 0 || copyPngAsync is null)
        {
            return false;
        }

        try
        {
            await copyPngAsync(png);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return false;
        }
    }

    internal static async Task CopyPngToClipboardAsync(IClipboard clipboard, byte[] png)
    {
        if (clipboard is null || png is null || png.Length == 0)
        {
            return;
        }

        var data = new DataObject();
        data.Set("PNG", png);
        data.Set("image/png", png);
        await clipboard.SetDataObjectAsync(data);
    }

    private void PersistCurrentTemplate(SharePlatformChoice platform)
    {
        if (platform == SharePlatformChoice.Twitter)
        {
            _templates.SetTwitterFormat(_state.CurrentPhase, _notesBox.Text ?? string.Empty);
        }
        else if (platform == SharePlatformChoice.Twitch)
        {
            _templates.TwitchFormat = _notesBox.Text ?? string.Empty;
        }
        else
        {
            return;
        }

        try
        {
            _templateStore.Save(_templates);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private async Task Preview()
    {
        await new MessageDialog("Preview", await FormatNotesAsync()).ShowDialogAsync(this);
    }

    private void Insert(string token)
    {
        int caret = Math.Clamp(_notesBox.CaretIndex, 0, _notesBox.Text?.Length ?? 0);
        _notesBox.Text = (_notesBox.Text ?? string.Empty).Insert(caret, token);
        _notesBox.CaretIndex = caret + token.Length;
        _notesBox.Focus();
    }

    internal static bool IsInsertTokenEnabled(string token, bool supportsNotes, IRun run, LiveSplitState state)
    {
        if (!supportsNotes)
        {
            return false;
        }

        return token switch
        {
            "$game" => !string.IsNullOrEmpty(run?.GameName),
            "$category" => !string.IsNullOrEmpty(run?.CategoryName),
            "$title" => !string.IsNullOrEmpty(run?.GameName) || !string.IsNullOrEmpty(run?.CategoryName),
            "$pb" => run?.LastOrDefault()?.PersonalBestSplitTime[state.CurrentTimingMethod] != null,
            "$delta" or "$splitname" or "$splittime" => state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused
                && state.CurrentSplitIndex > 0
                && state.CurrentSplitIndex <= (run?.Count ?? 0),
            _ => true,
        };
    }

    private async Task<string> FormatNotesAsync()
    {
        string template = _notesBox.Text ?? string.Empty;
        return ShareNotesFormatter.Format(
            _run,
            _state.CurrentPhase,
            _state.CurrentSplitIndex,
            _state.CurrentTimingMethod,
            template,
            await ResolveStreamLinkForNotes(template));
    }

    private Task<string> ResolveStreamLinkForNotes(string template)
    {
        return ResolveStreamLinkForNotes(
            template,
            () => Twitch.Instance.IsLoggedIn,
            () => Twitch.Instance.VerifyLogin(false),
            EnsureTwitchAuthentication,
            () => Twitch.Instance.ChannelName);
    }

    internal static async Task<string> ResolveStreamLinkForNotes(
        string template,
        Func<bool> isLoggedIn,
        Func<bool> verifyStoredLogin,
        Func<Task<bool>> promptLogin,
        Func<string> channelName)
    {
        if (template?.Contains("$stream", StringComparison.Ordinal) != true)
        {
            return string.Empty;
        }

        try
        {
            if (isLoggedIn() || verifyStoredLogin() || await promptLogin())
            {
                string channel = channelName();
                return string.IsNullOrEmpty(channel) ? string.Empty : $"http://twitch.tv/{channel}";
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
                await FormatNotesAsync());

            if (result.Success)
            {
                IClipboard clipboard = Clipboard;
                if (clipboard is not null)
                {
                    await clipboard.SetTextAsync(result.Url);
                }

                Process.Start(new ProcessStartInfo(result.Url) { UseShellExecute = true });
                SetStatus($"Uploaded: {result.Url} (URL copied to clipboard).");
                await ShowShareResultMessage(ShareResult.Success, SharePlatformChoice.Imgur.Name);
                return;
            }

            SetStatus(result.ErrorMessage);
            await ShowShareResultMessage(ShareResult.Failure, SharePlatformChoice.Imgur.Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            SetStatus($"Imgur upload failed: {ex.Message}");
            await ShowShareResultMessage(ShareResult.Failure, SharePlatformChoice.Imgur.Name);
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
            await ShowShareResultMessage(ShareResult.LoginFailure, SharePlatformChoice.Twitch.Name);
            return;
        }

        try
        {
            bool submitted = await SubmitTwitchShareAsync(
                _run,
                await FormatNotesAsync(),
                gameName => Twitch.Instance.FindGame(gameName),
                ResolveTwitchGameWithDialog,
                (title, game) => Twitch.Instance.SetStreamTitleAndGame(title, game));

            await ShowShareResultMessage(
                submitted ? ShareResult.Success : ShareResult.Failure,
                SharePlatformChoice.Twitch.Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            await ShowShareResultMessage(ShareResult.Failure, SharePlatformChoice.Twitch.Name);
        }
    }

    internal readonly record struct TwitchGameResolveResult(bool Accepted, Twitch.TwitchGame Game)
    {
        public static TwitchGameResolveResult Selected(Twitch.TwitchGame game) => new(true, game);
        public static TwitchGameResolveResult NoGame() => new(true, null);
        public static TwitchGameResolveResult Canceled() => new(false, null);
    }

    internal static async Task<bool> SubmitTwitchShareAsync(
        IRun run,
        string title,
        Func<string, IEnumerable<Twitch.TwitchGame>> findGame,
        Func<string, Task<TwitchGameResolveResult>> resolveGame,
        Action<string, Twitch.TwitchGame> setStreamTitleAndGame)
    {
        TwitchGameResolveResult resolvedGame = TryResolveExactTwitchGame(run?.GameName, findGame);
        if (!resolvedGame.Accepted)
        {
            resolvedGame = resolveGame is null
                ? TwitchGameResolveResult.Canceled()
                : await resolveGame(run?.GameName ?? string.Empty);
        }

        if (!resolvedGame.Accepted)
        {
            return false;
        }

        setStreamTitleAndGame(title, resolvedGame.Game);
        return true;
    }

    internal static TwitchGameResolveResult TryResolveExactTwitchGame(
        string gameName,
        Func<string, IEnumerable<Twitch.TwitchGame>> findGame)
    {
        try
        {
            Twitch.TwitchGame game = findGame(gameName)
                .First(twitchGame => twitchGame.Name == gameName);
            return TwitchGameResolveResult.Selected(game);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return TwitchGameResolveResult.Canceled();
        }
    }

    private async Task<TwitchGameResolveResult> ResolveTwitchGameWithDialog(string gameName)
    {
        var dialog = new TwitchGameResolveDialog(gameName, name => Twitch.Instance.FindGame(name));
        return await dialog.ShowDialogAsync(this);
    }

    private async Task SubmitToSpeedrunCom()
    {
        SpeedrunComReadiness readiness = await CheckSpeedrunComReadiness(_run, EnsureSpeedrunComAuthentication);
        if (!readiness.CanSubmit)
        {
            SetStatus(readiness.Message);
            if (readiness.Message == "Your login information seems to be incorrect.")
            {
                await ShowShareResultMessage(ShareResult.LoginFailure, SharePlatformChoice.SpeedrunCom.Name);
                return;
            }

            await new MessageDialog("Submitting Failed", readiness.Message).ShowDialogAsync(this);
            return;
        }

        var dialog = new SpeedrunComSubmitDialog(_run.Metadata, await FormatNotesAsync());
        bool submitted = await dialog.ShowDialogAsync(this);
        await ShowShareResultMessage(
            submitted ? ShareResult.Success : ShareResult.Failure,
            SharePlatformChoice.SpeedrunCom.Name);
    }

    internal enum ShareResult
    {
        Success,
        LoginFailure,
        Failure,
    }

    internal readonly record struct ShareResultMessage(string Title, string Message);

    internal static ShareResultMessage GetShareResultMessage(ShareResult result, string platformName)
    {
        return result switch
        {
            ShareResult.Success => new ShareResultMessage(
                "Run Shared",
                $"Your run was successfully shared to {platformName}."),
            ShareResult.LoginFailure => new ShareResultMessage(
                "Error",
                "Your login information seems to be incorrect."),
            _ => new ShareResultMessage(
                "Error",
                "The run could not be shared."),
        };
    }

    private async Task ShowShareResultMessage(ShareResult result, string platformName)
    {
        ShareResultMessage message = GetShareResultMessage(result, platformName);
        SetStatus(message.Message);
        await new MessageDialog(message.Title, message.Message).ShowDialogAsync(this);
    }

    internal readonly record struct SpeedrunComReadiness(bool CanSubmit, string Message);

    internal static async Task<SpeedrunComReadiness> CheckSpeedrunComReadiness(
        IRun run,
        Func<Task<bool>> ensureAuthentication)
    {
        if (!string.IsNullOrEmpty(run?.Metadata?.RunID))
        {
            return new SpeedrunComReadiness(false, "This run already exists on speedrun.com.");
        }

        if (!await ensureAuthentication())
        {
            return new SpeedrunComReadiness(false, "Your login information seems to be incorrect.");
        }

        if (!SpeedrunCom.ValidateRun(run, out string reason))
        {
            return new SpeedrunComReadiness(false, reason);
        }

        return new SpeedrunComReadiness(true, string.Empty);
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
            supportsNotes: false);

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
