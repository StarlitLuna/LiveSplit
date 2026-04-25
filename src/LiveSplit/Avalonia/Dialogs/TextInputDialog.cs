using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>
/// Minimal text-input dialog used by the Avalonia front-end where the WinForms version uses
/// inline grid editing or InputBox-style prompts.
/// </summary>
public sealed class TextInputDialog : Window
{
    private readonly TaskCompletionSource<string> _result = new();

    public TextInputDialog(string title, string prompt, string initialValue = "")
    {
        Title = title;
        Width = 380;
        Height = 180;
        CanResize = false;

        var box = new TextBox { Text = initialValue ?? string.Empty };

        var ok = new Button { Content = "OK", Width = 80, IsDefault = true };
        ok.Click += (_, _) => { _result.TrySetResult(box.Text ?? string.Empty); Close(); };
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        cancel.Click += (_, _) => { _result.TrySetResult(null); Close(); };

        Opened += (_, _) => box.Focus();

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = prompt },
                box,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Margin = new Thickness(0, 12, 0, 0),
                    Children = { cancel, ok },
                },
            },
        };

        Closed += (_, _) =>
        {
            if (!_result.Task.IsCompleted)
            {
                _result.TrySetResult(null);
            }
        };
    }

    public new async Task<string> ShowDialogAsync(Window owner)
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
