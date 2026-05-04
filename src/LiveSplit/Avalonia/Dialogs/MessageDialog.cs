using System.Threading.Tasks;
using System.Collections.Generic;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;

namespace LiveSplit.Avalonia.Dialogs;

public enum MessageResult { Ok, Yes, No, Cancel }

public sealed class MessageDialog : Window
{
    public enum Buttons { Ok, OkCancel, YesNo, YesNoCancel, RetryCancel }

    private readonly TaskCompletionSource<MessageResult> _result = new();

    internal readonly record struct ButtonSpec(
        string Text,
        MessageResult Result,
        bool IsDefault = false,
        bool IsCancel = false);

    public MessageDialog(string title, string message, Buttons buttons = Buttons.Ok)
    {
        Title = title;
        Width = 420;
        Height = 200;
        CanResize = false;
        DialogTheme.ApplyWindow(this);

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

        foreach (ButtonSpec spec in GetButtonSpecs(buttons))
        {
            AddButton(buttonBar, spec.Text, spec.Result, spec.IsDefault, spec.IsCancel);
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

    internal static IReadOnlyList<ButtonSpec> GetButtonSpecs(Buttons buttons)
        => buttons switch
        {
            Buttons.OkCancel =>
            [
                new ButtonSpec("OK", MessageResult.Ok, IsDefault: true),
                new ButtonSpec("Cancel", MessageResult.Cancel, IsCancel: true),
            ],
            Buttons.YesNo =>
            [
                new ButtonSpec("Yes", MessageResult.Yes, IsDefault: true),
                new ButtonSpec("No", MessageResult.No, IsCancel: true),
            ],
            Buttons.YesNoCancel =>
            [
                new ButtonSpec("Yes", MessageResult.Yes, IsDefault: true),
                new ButtonSpec("No", MessageResult.No),
                new ButtonSpec("Cancel", MessageResult.Cancel, IsCancel: true),
            ],
            Buttons.RetryCancel =>
            [
                new ButtonSpec("Retry", MessageResult.Ok, IsDefault: true),
                new ButtonSpec("Cancel", MessageResult.Cancel, IsCancel: true),
            ],
            _ =>
            [
                new ButtonSpec("OK", MessageResult.Ok, IsDefault: true, IsCancel: true),
            ],
        };

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
