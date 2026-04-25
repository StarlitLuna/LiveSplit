using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>
/// Placeholder Avalonia replacement for <c>SpeedrunComSubmitDialog</c>. Submitting runs to
/// speedrun.com requires OAuth-backed credentials; that path still runs through the Windows
/// CredentialManager (DPAPI). Phase 7 migrates the credential store to Linux and this dialog
/// becomes fully functional; today it points users at the Windows build.
/// </summary>
public sealed class SpeedrunComSubmitDialog : Window
{
    private readonly TaskCompletionSource<bool> _result = new();

    public SpeedrunComSubmitDialog()
    {
        Title = "Submit to Speedrun.com";
        Width = 440;
        Height = 220;
        CanResize = false;

        var msg = new TextBlock
        {
            Text = "Submitting runs to speedrun.com isn't wired up in the Avalonia build yet — " +
                   "the OAuth credential flow is tied to the Windows DPAPI CredentialManager " +
                   "(Phase 7 migrates this to Linux). Use the Windows build to submit runs.",
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
