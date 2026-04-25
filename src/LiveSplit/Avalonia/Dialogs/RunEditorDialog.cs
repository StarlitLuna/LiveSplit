using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

using LiveSplit.Model;
using LiveSplit.UI.Components;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>
/// Edits the run's game/category/offset/attempt count and the per-segment list (add /
/// remove / rename / reorder). Per-segment Best Segment time editing across comparisons +
/// timing methods, run history, custom variables, game icon picker, and Speedrun.com
/// submit/import are not implemented here.
/// </summary>
public sealed class RunEditorDialog : Window
{
    public IRun Run { get; }
    public LiveSplitState State { get; }
    public List<System.Drawing.Image> ImagesToDispose { get; } = new();

    public event EventHandler RunEdited;
    public event EventHandler SegmentRemovedOrAdded;

    private readonly TaskCompletionSource<bool> _result = new();
    private readonly TextBox _gameBox;
    private readonly TextBox _categoryBox;
    private readonly TextBox _offsetBox;
    private readonly NumericUpDown _attemptBox;
    private readonly ListBox _segmentsList;

    public RunEditorDialog(LiveSplitState state)
    {
        State = state;
        Run = state.Run;

        Title = "Edit Splits";
        Width = 720;
        Height = 600;

        _gameBox = new TextBox { Text = Run.GameName ?? "" };
        _categoryBox = new TextBox { Text = Run.CategoryName ?? "" };
        _offsetBox = new TextBox { Text = Run.Offset.ToString() };
        _attemptBox = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 999999,
            Value = Run.AttemptCount,
            Width = 120,
        };

        var headerPanel = new StackPanel
        {
            Margin = new Thickness(20, 20, 20, 8),
            Spacing = 6,
        };
        headerPanel.Children.Add(LabeledRow("Game:", _gameBox));
        headerPanel.Children.Add(LabeledRow("Category:", _categoryBox));
        headerPanel.Children.Add(LabeledRow("Offset:", _offsetBox));
        headerPanel.Children.Add(LabeledRow("Attempt Count:", _attemptBox));

        _segmentsList = new ListBox
        {
            ItemsSource = SegmentNames(),
            Margin = new Thickness(0, 0, 8, 0),
        };

        var addBtn = new Button { Content = "Add Segment", Margin = new Thickness(4) };
        addBtn.Click += async (_, _) => await AddSegment();
        var removeBtn = new Button { Content = "Remove Segment", Margin = new Thickness(4) };
        removeBtn.Click += (_, _) => RemoveSegment();
        var renameBtn = new Button { Content = "Rename Segment", Margin = new Thickness(4) };
        renameBtn.Click += async (_, _) => await RenameSegment();
        var upBtn = new Button { Content = "Move Up", Margin = new Thickness(4) };
        upBtn.Click += (_, _) => MoveSegment(-1);
        var downBtn = new Button { Content = "Move Down", Margin = new Thickness(4) };
        downBtn.Click += (_, _) => MoveSegment(1);

        var sideBar = new StackPanel
        {
            Spacing = 4,
            Children = { addBtn, removeBtn, renameBtn, upBtn, downBtn },
        };

        var listGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(20, 8, 20, 8),
        };
        Grid.SetColumn(_segmentsList, 0);
        Grid.SetColumn(sideBar, 1);
        listGrid.Children.Add(_segmentsList);
        listGrid.Children.Add(sideBar);

        var center = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(headerPanel, Dock.Top);
        center.Children.Add(headerPanel);
        center.Children.Add(listGrid);

        var ok = new Button { Content = "OK", Width = 80, IsDefault = true };
        ok.Click += (_, _) => { Apply(); _result.TrySetResult(true); Close(); };
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
        root.Children.Add(center);
        Content = root;

        Closed += (_, _) =>
        {
            if (!_result.Task.IsCompleted)
            {
                _result.TrySetResult(false);
            }
        };
    }

    private void Apply()
    {
        Run.GameName = _gameBox.Text ?? string.Empty;
        Run.CategoryName = _categoryBox.Text ?? string.Empty;
        if (TimeSpan.TryParse(_offsetBox.Text, out TimeSpan offset))
        {
            Run.Offset = offset;
        }

        if (_attemptBox.Value.HasValue)
        {
            Run.AttemptCount = (int)_attemptBox.Value.Value;
        }

        Run.HasChanged = true;
        RunEdited?.Invoke(this, EventArgs.Empty);
    }

    private List<string> SegmentNames()
    {
        var names = new List<string>(Run.Count);
        for (int i = 0; i < Run.Count; i++)
        {
            names.Add(Run[i].Name);
        }

        return names;
    }

    private void Refresh() => _segmentsList.ItemsSource = SegmentNames();

    private async Task AddSegment()
    {
        var prompt = new TextInputDialog("New Segment", "Segment name:");
        if (await prompt.ShowDialogAsync(this) is { } name && !string.IsNullOrWhiteSpace(name))
        {
            ISegment seg = new Segment(name);
            int idx = _segmentsList.SelectedIndex < 0 ? Run.Count : _segmentsList.SelectedIndex + 1;
            Run.Insert(idx, seg);
            Refresh();
            _segmentsList.SelectedIndex = idx;
            SegmentRemovedOrAdded?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RemoveSegment()
    {
        int idx = _segmentsList.SelectedIndex;
        if (idx < 0 || idx >= Run.Count || Run.Count <= 1)
        {
            return;
        }

        Run.RemoveAt(idx);
        Refresh();
        SegmentRemovedOrAdded?.Invoke(this, EventArgs.Empty);
    }

    private async Task RenameSegment()
    {
        int idx = _segmentsList.SelectedIndex;
        if (idx < 0 || idx >= Run.Count)
        {
            return;
        }

        var prompt = new TextInputDialog("Rename Segment", "New name:", Run[idx].Name);
        if (await prompt.ShowDialogAsync(this) is { } name && !string.IsNullOrWhiteSpace(name))
        {
            Run[idx].Name = name;
            Refresh();
            _segmentsList.SelectedIndex = idx;
        }
    }

    private void MoveSegment(int dir)
    {
        int idx = _segmentsList.SelectedIndex;
        int newIdx = idx + dir;
        if (idx < 0 || newIdx < 0 || newIdx >= Run.Count)
        {
            return;
        }

        ISegment moved = Run[idx];
        Run.RemoveAt(idx);
        Run.Insert(newIdx, moved);
        Refresh();
        _segmentsList.SelectedIndex = newIdx;
    }

    private static StackPanel LabeledRow(string label, Control control)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Width = 110 },
                control,
            },
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
