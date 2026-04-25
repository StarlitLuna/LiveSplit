using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>
/// Minimal Avalonia replacement for <see cref="System.Windows.Forms.MessageBox"/>. Shows a
/// title + message + OK/Cancel buttons (configurable). Returns whether the user picked OK.
/// </summary>
public sealed class MessageDialog : Window
{
    public enum Buttons { Ok, OkCancel, YesNo }

    private readonly TaskCompletionSource<bool> _result = new();

    public MessageDialog(string title, string message, Buttons buttons = Buttons.Ok)
    {
        Title = title;
        Width = 420;
        Height = 200;
        CanResize = false;

        var msg = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(20),
            VerticalAlignment = VerticalAlignment.Top,
        };

        var buttonBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 0, 12, 12),
        };

        switch (buttons)
        {
            case Buttons.OkCancel:
                AddCancelButton(buttonBar);
                AddOkButton(buttonBar, "OK");
                break;
            case Buttons.YesNo:
                AddCustomButton(buttonBar, "No", false);
                AddCustomButton(buttonBar, "Yes", true, isDefault: true);
                break;
            case Buttons.Ok:
            default:
                AddOkButton(buttonBar, "OK");
                break;
        }

        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(buttonBar, Dock.Bottom);
        root.Children.Add(buttonBar);
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

    private void AddOkButton(StackPanel bar, string text)
    {
        var btn = new Button { Content = text, Width = 80, IsDefault = true };
        btn.Click += (_, _) => { _result.TrySetResult(true); Close(); };
        bar.Children.Add(btn);
    }

    private void AddCancelButton(StackPanel bar)
    {
        var btn = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        btn.Click += (_, _) => { _result.TrySetResult(false); Close(); };
        bar.Children.Add(btn);
    }

    private void AddCustomButton(StackPanel bar, string text, bool returnValue, bool isDefault = false)
    {
        var btn = new Button { Content = text, Width = 80, IsDefault = isDefault };
        btn.Click += (_, _) => { _result.TrySetResult(returnValue); Close(); };
        bar.Children.Add(btn);
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
