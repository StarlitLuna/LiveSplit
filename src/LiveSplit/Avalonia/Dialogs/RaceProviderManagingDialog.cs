using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>
/// Placeholder Avalonia replacement for <c>RaceProviderManagingDialog</c>. The WinForms
/// version lets users enable / configure race providers (SRL, Racetime, …). Racetime was
/// dropped from the Linux build (Phase 1 plan — WebView2 dependency), and SRL's web-socket
/// authentication flow hasn't been ported. Users who need racing configuration should open
/// LiveSplit on Windows.
/// </summary>
public sealed class RaceProviderManagingDialog : Window
{
    private readonly TaskCompletionSource<bool> _result = new();

    public RaceProviderManagingDialog()
    {
        Title = "Race Providers";
        Width = 420;
        Height = 220;
        CanResize = false;

        var msg = new TextBlock
        {
            Text = "Race provider configuration isn't available in the Avalonia build yet. " +
                   "Racetime (WebView2-based) is excluded on Linux by design; SpeedRunsLive " +
                   "configuration is still Windows-only. Use the Windows build to manage these.",
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(20),
        };

        var close = new Button { Content = "Close", Width = 80, IsDefault = true, IsCancel = true };
        close.Click += (_, _) => { _result.TrySetResult(false); Close(); };

        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(close, Dock.Bottom);
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Margin = new Thickness(0, 0, 12, 12);
        root.Children.Add(close);
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
