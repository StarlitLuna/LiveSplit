using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

namespace LiveSplit.Avalonia.Dialogs;

public sealed class EditHistoryDialog : Window
{
    public IList<string> History { get; private set; }

    private readonly TaskCompletionSource<bool> _result = new();
    private readonly ListBox _list;

    public EditHistoryDialog(IEnumerable<string> history)
    {
        History = history.Reverse().ToList();

        Title = "Edit History";
        Width = 390;
        Height = 270;
        MinWidth = 300;
        MinHeight = 258;
        DialogTheme.ApplyWindow(this);

        _list = new ListBox
        {
            SelectionMode = SelectionMode.Multiple,
            ItemsSource = History.Where(x => !string.IsNullOrEmpty(x)).ToList(),
            Margin = new Thickness(7),
        };

        var remove = new Button
        {
            Content = "Remove Selected",
            Width = 120,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 7, 4),
        };
        remove.Click += (_, _) =>
        {
            var selected = _list.SelectedItems.Cast<string>().ToList();
            foreach (string item in selected)
            {
                History.Remove(item);
            }

            _list.ItemsSource = History.Where(x => !string.IsNullOrEmpty(x)).ToList();
        };

        var ok = new Button { Content = "OK", Width = 80, IsDefault = true };
        ok.Click += (_, _) =>
        {
            History = History.Reverse().ToList();
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
            Margin = new Thickness(0, 0, 7, 7),
            Children = { ok, cancel },
        };

        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(buttons, Dock.Bottom);
        DockPanel.SetDock(remove, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(remove);
        root.Children.Add(_list);
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
