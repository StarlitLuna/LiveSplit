using System.Threading.Tasks;
using System.Xml;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

using LiveSplit.UI;
using LiveSplit.UI.Components;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>
/// Hosts a single component's settings UI inside a dialog window with OK / Cancel
/// buttons. Cancel reapplies the snapshotted XML so changes roll back. Components that
/// don't supply their own Avalonia control fall back to the reflection-driven
/// <see cref="AvaloniaSettingsBuilder"/> panel.
/// </summary>
public sealed class ComponentSettingsDialog : Window
{
    private readonly IComponent _component;
    private readonly XmlNode _snapshot;
    private readonly TaskCompletionSource<bool> _result = new();

    public ComponentSettingsDialog(IComponent component)
    {
        _component = component;
        _snapshot = component.GetSettings(new XmlDocument());

        Title = component.ComponentName + " Settings";
        Width = 540;
        Height = 600;

        Control settingsControl = component.GetSettingsControl(LayoutMode.Vertical)
            ?? AvaloniaSettingsBuilder.Build(component, component.ComponentName);

        var ok = new Button { Content = "OK", Width = 80, IsDefault = true };
        ok.Click += (_, _) => { _result.TrySetResult(true); Close(); };
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        cancel.Click += (_, _) =>
        {
            _component.SetSettings(_snapshot);
            _result.TrySetResult(false);
            Close();
        };

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
            // If the user closed via the X button, treat as Cancel — restore the snapshot.
            if (!_result.Task.IsCompleted)
            {
                _component.SetSettings(_snapshot);
                _result.TrySetResult(false);
            }
        };
    }

    /// <summary>
    /// Show the dialog modally over <paramref name="owner"/> and return whether the user
    /// clicked OK (true) or canceled (false).
    /// </summary>
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
