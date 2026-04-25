using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

using LiveSplit.Model.Comparisons;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>Avalonia counterpart of <c>LiveSplit.View.ChooseComparisonsDialog</c>.</summary>
public sealed class ChooseComparisonsDialog : Window
{
    public IDictionary<string, bool> ComparisonGeneratorStates { get; set; }
    public int HcpHistorySize { get; set; }
    public int HcpNBestRuns { get; set; }

    private readonly TaskCompletionSource<bool> _result = new();
    private static readonly string[] AllComparisons =
    {
        BestSegmentsComparisonGenerator.ComparisonName,
        BestSplitTimesComparisonGenerator.ComparisonName,
        AverageSegmentsComparisonGenerator.ComparisonName,
        MedianSegmentsComparisonGenerator.ComparisonName,
        WorstSegmentsComparisonGenerator.ComparisonName,
        PercentileComparisonGenerator.ComparisonName,
        LatestRunComparisonGenerator.ComparisonName,
        HCPComparisonGenerator.ComparisonName,
        NoneComparisonGenerator.ComparisonName,
    };

    private readonly List<CheckBox> _checkBoxes = new();
    private NumericUpDown _historyBox;
    private NumericUpDown _bestRunsBox;
    private StackPanel _hcpPanel;

    public ChooseComparisonsDialog()
    {
        Title = "Choose Comparisons";
        Width = 420;
        Height = 460;

        var stack = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 6,
        };

        foreach (string name in AllComparisons)
        {
            var cb = new CheckBox { Content = name };
            string captured = name;
            cb.IsCheckedChanged += (_, _) =>
            {
                if (ComparisonGeneratorStates is not null)
                {
                    ComparisonGeneratorStates[captured] = cb.IsChecked == true;
                }

                if (captured == HCPComparisonGenerator.ComparisonName && _hcpPanel is not null)
                {
                    _hcpPanel.IsVisible = cb.IsChecked == true;
                }
            };
            _checkBoxes.Add(cb);
            stack.Children.Add(cb);
        }

        _historyBox = new NumericUpDown { Minimum = 1, Maximum = 999, Width = 80 };
        _bestRunsBox = new NumericUpDown { Minimum = 1, Maximum = 999, Width = 80 };
        _historyBox.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue && _bestRunsBox.Value.HasValue && e.NewValue.Value < _bestRunsBox.Value.Value)
            {
                _bestRunsBox.Value = e.NewValue.Value;
            }

            HcpHistorySize = e.NewValue.HasValue ? (int)e.NewValue.Value : HcpHistorySize;
        };
        _bestRunsBox.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue && _historyBox.Value.HasValue && e.NewValue.Value > _historyBox.Value.Value)
            {
                _historyBox.Value = e.NewValue.Value;
            }

            HcpNBestRuns = e.NewValue.HasValue ? (int)e.NewValue.Value : HcpNBestRuns;
        };

        _hcpPanel = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(0, 12, 0, 0),
            IsVisible = false,
            Children =
            {
                new TextBlock { Text = "HCP Settings", FontWeight = global::Avalonia.Media.FontWeight.Bold },
                Row("History size:", _historyBox),
                Row("Best runs (n):", _bestRunsBox),
            },
        };
        stack.Children.Add(_hcpPanel);

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
        root.Children.Add(new ScrollViewer { Content = stack });
        Content = root;

        Opened += (_, _) =>
        {
            if (ComparisonGeneratorStates is not null)
            {
                foreach (CheckBox cb in _checkBoxes)
                {
                    string name = (string)cb.Content;
                    cb.IsChecked = ComparisonGeneratorStates.TryGetValue(name, out bool on) && on;
                }
            }

            _historyBox.Value = HcpHistorySize;
            _bestRunsBox.Value = HcpNBestRuns;
            _hcpPanel.IsVisible = _checkBoxes.Single(c => (string)c.Content == HCPComparisonGenerator.ComparisonName).IsChecked == true;
        };

        Closed += (_, _) =>
        {
            if (!_result.Task.IsCompleted)
            {
                _result.TrySetResult(false);
            }
        };
    }

    private static StackPanel Row(string label, Control control)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Width = 110 }, control },
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
