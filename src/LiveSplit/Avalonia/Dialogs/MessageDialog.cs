using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;

namespace LiveSplit.Avalonia.Dialogs;

public enum MessageResult { Ok, Yes, No, Cancel }

public sealed class MessageDialog : Window
{
    public enum Buttons { Ok, OkCancel, YesNo, YesNoCancel }

    private readonly TaskCompletionSource<MessageResult> _result = new();

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
                AddButton(buttonBar, "Cancel", MessageResult.Cancel, isCancel: true);
                AddButton(buttonBar, "OK", MessageResult.Ok, isDefault: true);
                break;
            case Buttons.YesNo:
                AddButton(buttonBar, "No", MessageResult.No, isCancel: true);
                AddButton(buttonBar, "Yes", MessageResult.Yes, isDefault: true);
                break;
            case Buttons.YesNoCancel:
                AddButton(buttonBar, "Cancel", MessageResult.Cancel, isCancel: true);
                AddButton(buttonBar, "No", MessageResult.No);
                AddButton(buttonBar, "Yes", MessageResult.Yes, isDefault: true);
                break;
            case Buttons.Ok:
            default:
                AddButton(buttonBar, "OK", MessageResult.Ok, isDefault: true, isCancel: true);
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
                _result.TrySetResult(MessageResult.Cancel);
            }
        };
    }

    private void AddButton(StackPanel bar, string text, MessageResult result, bool isDefault = false, bool isCancel = false)
    {
        var btn = new Button { Content = text, Width = 80, IsDefault = isDefault, IsCancel = isCancel };
        btn.Click += (_, _) => { _result.TrySetResult(result); Close(); };
        bar.Children.Add(btn);
    }

    public async Task<bool> ShowDialogAsync(Window owner)
    {
        MessageResult r = await ShowDialogResultAsync(owner);
        return r is MessageResult.Ok or MessageResult.Yes;
    }

    public async Task<MessageResult> ShowDialogResultAsync(Window owner)
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
