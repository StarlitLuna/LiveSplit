using System;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

using LiveSplit.Model;
using LiveSplit.Options;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>
/// Avalonia replacement for <c>ShareRunDialog</c>. The WinForms version posts to
/// Twitter / Twitch / Imgur / Speedrun.com / Excel via <c>LiveSplit.Web.Share</c>. The
/// Avalonia v1 keeps only the on-disk "save a screenshot" flow because the social
/// platforms' OAuth bits aren't wired through the Avalonia credential manager yet
/// (Phase 7 ports CredentialManager to Linux).
/// </summary>
public sealed class ShareRunDialog : Window
{
    private readonly Func<System.Drawing.Image> _screenshot;
    private readonly TaskCompletionSource<bool> _result = new();

    public ShareRunDialog(LiveSplitState state, ISettings settings, Func<System.Drawing.Image> screenShotFunction)
    {
        _screenshot = screenShotFunction;

        Title = "Share Run";
        Width = 480;
        Height = 300;
        CanResize = false;

        var msg = new TextBlock
        {
            Text = "The Avalonia build supports saving a local screenshot of the current layout. " +
                   "Posting to Twitter / Twitch / Imgur / Speedrun.com still runs on the Windows build.",
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(20),
        };

        var saveBtn = new Button { Content = "Save Screenshot…", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(20, 0, 0, 0) };
        saveBtn.Click += async (_, _) => await SaveScreenshot();

        var close = new Button { Content = "Close", Width = 80, IsCancel = true };
        close.Click += (_, _) => { _result.TrySetResult(false); Close(); };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 0, 12, 12),
            Children = { close },
        };

        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(buttons, Dock.Bottom);
        DockPanel.SetDock(saveBtn, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(saveBtn);
        root.Children.Add(msg);
        Content = root;

        Closed += (_, _) =>
        {
            if (!_result.Task.IsCompleted)
            {
                _result.TrySetResult(false);
            }
        };
    }

    private async Task SaveScreenshot()
    {
        if (_screenshot is null)
        {
            return;
        }

        var picker = new SaveFileDialog
        {
            Title = "Save Screenshot",
            DefaultExtension = "png",
            InitialFileName = "livesplit.png",
        };

        string path = await picker.ShowAsync(this);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            System.Drawing.Image img = _screenshot();
            img?.Save(path);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
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
