using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

using LiveSplit.UI;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>
/// General LiveSplit settings (display options, comparison generators, race providers,
/// language, etc.). Uses <see cref="AvaloniaSettingsBuilder"/> to surface the boolean / numeric
/// / enum / color settings on <c>ISettings</c> via reflection.
/// </summary>
public sealed class SettingsDialog : Window
{
    private readonly TaskCompletionSource<bool> _result = new();

    public SettingsDialog(object settings)
    {
        Title = "LiveSplit Settings";
        Width = 560;
        Height = 600;

        Control settingsControl = AvaloniaSettingsBuilder.Build(settings, "Settings");

        var ok = new Button { Content = "OK", Width = 80, IsDefault = true };
        ok.Click += (_, _) => { _result.TrySetResult(true); Close(); };
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        cancel.Click += (_, _) => { _result.TrySetResult(false); Close(); };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 0, 12, 12),
            Children = { cancel, ok },
        };

        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(settingsControl);
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
