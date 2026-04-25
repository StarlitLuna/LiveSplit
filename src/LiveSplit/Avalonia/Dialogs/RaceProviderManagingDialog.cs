using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>
/// Placeholder dialog explaining that race-provider configuration is not currently
/// implemented in this UI.
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
            Text = "Race provider configuration is not implemented in this build.",
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
