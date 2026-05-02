using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

using LiveSplit.UI;
using LiveSplit.Options;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>
/// Edits the <c>LayoutSettings</c> object (background, fonts, shadow color, text color, etc.)
/// via the reflection-driven <see cref="AvaloniaSettingsBuilder"/>. Font fields are not
/// editable here — the auto-generated panel skips them.
/// </summary>
public sealed class LayoutSettingsDialog : Window
{
    private readonly TaskCompletionSource<bool> _result = new();
    private readonly LayoutSettings _targetSettings;
    private readonly LayoutSettings _snapshot;
    private bool _accepted;

    public LayoutSettingsDialog(object layoutSettings)
    {
        _targetSettings = layoutSettings as LayoutSettings;
        _snapshot = _targetSettings?.Clone() as LayoutSettings;

        Title = "Layout Settings";
        Width = 540;
        Height = 600;

        Control settingsControl = AvaloniaSettingsBuilder.Build(layoutSettings, "Layout Settings");

        var ok = new Button { Content = "OK", Width = 80, IsDefault = true };
        ok.Click += (_, _) =>
        {
            ok.Focus();
            _accepted = true;
            _result.TrySetResult(true);
            Close();
        };
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

            if (!_accepted && _targetSettings != null && _snapshot != null)
            {
                _targetSettings.Assign(_snapshot);
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
