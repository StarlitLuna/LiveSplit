using System;
using System.Linq;
using System.Windows.Input;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;

using LiveSplit.Model.Comparisons;

namespace LiveSplit.UI.Components;

internal static class SubsplitsSettingsControl
{
    public static Control Build(SplitsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var panel = new StackPanel
        {
            Margin = new Thickness(7),
            Spacing = 6,
        };

        panel.Children.Add(AvaloniaSettingsBuilder.Build(settings, "Subsplits"));
        panel.Children.Add(new TextBlock { Text = "Header", FontWeight = FontWeight.Bold });
        panel.Children.Add(BuildHeaderRow(settings));

        var columnsHost = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = "Columns", FontWeight = FontWeight.Bold });
        panel.Children.Add(columnsHost);

        var addColumnButton = new Button
        {
            Name = "AddColumnButton",
            Content = "Add Column",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        panel.Children.Add(addColumnButton);

        void RebuildColumns()
        {
            columnsHost.Children.Clear();
            for (int i = 0; i < settings.ColumnsList.Count; i++)
            {
                columnsHost.Children.Add(BuildColumnRow(settings, i, RebuildColumns));
            }
        }

        addColumnButton.Command = new ActionCommand(() =>
        {
            settings.ColumnsList.Add(new ColumnSettings(settings.CurrentState, "Column", settings.ColumnsList));
            RebuildColumns();
        });

        RebuildColumns();
        return new ScrollViewer { Content = panel };
    }

    private static Control BuildHeaderRow(SplitsSettings settings)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("160,146"),
            RowDefinitions = new RowDefinitions("Auto"),
        };

        string[] headerComparisons = ColumnSettings.GetComparisons(settings.CurrentState, ColumnType.Delta, settings.HeaderComparison);
        var comparisonBox = new ComboBox
        {
            Name = "HeaderComparisonComboBox",
            ItemsSource = headerComparisons,
            SelectedItem = headerComparisons.Contains(settings.HeaderComparison) ? settings.HeaderComparison : "Current Comparison",
            Margin = new Thickness(0, 2, 4, 2),
        };
        comparisonBox.SelectionChanged += (_, _) =>
        {
            if (comparisonBox.SelectedItem is string comparison)
            {
                settings.HeaderComparison = comparison;
            }
        };

        var timingBox = new ComboBox
        {
            Name = "HeaderTimingMethodComboBox",
            ItemsSource = ColumnSettings.TimingMethodNames,
            SelectedItem = ColumnSettings.TimingMethodNames.Contains(settings.HeaderTimingMethod) ? settings.HeaderTimingMethod : "Current Timing Method",
            Margin = new Thickness(0, 2),
        };
        timingBox.SelectionChanged += (_, _) =>
        {
            if (timingBox.SelectedItem is string timingMethod)
            {
                settings.HeaderTimingMethod = timingMethod;
            }
        };

        Add(grid, comparisonBox, 0);
        Add(grid, timingBox, 1);
        return grid;
    }

    private static Control BuildColumnRow(SplitsSettings settings, int index, Action rebuildColumns)
    {
        ColumnSettings column = settings.ColumnsList[index];
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("120,142,160,146,Auto,Auto,Auto"),
            RowDefinitions = new RowDefinitions("Auto"),
        };

        var nameBox = new TextBox
        {
            Name = $"Column{index}NameTextBox",
            Text = column.ColumnName,
            Margin = new Thickness(0, 2, 4, 2),
        };
        nameBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.TextProperty)
            {
                column.ColumnName = nameBox.Text ?? string.Empty;
            }
        };

        var typeBox = new ComboBox
        {
            Name = $"Column{index}TypeComboBox",
            ItemsSource = ColumnSettings.TypeNames,
            SelectedItem = column.Type,
            Margin = new Thickness(0, 2, 4, 2),
        };

        var comparisonBox = new ComboBox
        {
            Name = $"Column{index}ComparisonComboBox",
            Margin = new Thickness(0, 2, 4, 2),
        };

        void RefreshComparisons()
        {
            ColumnType type = ColumnSettings.ParseColumnType((string)typeBox.SelectedItem);
            string[] comparisons = ColumnSettings.GetComparisons(settings.CurrentState, type, column.Comparison);
            if (ColumnSettings.UsesSegmentComparison(type)
                && column.Comparison == BestSplitTimesComparisonGenerator.ComparisonName)
            {
                column.Comparison = "Current Comparison";
                comparisons = ColumnSettings.GetComparisons(settings.CurrentState, type, column.Comparison);
            }

            comparisonBox.ItemsSource = comparisons;
            comparisonBox.SelectedItem = comparisons.Contains(column.Comparison)
                ? column.Comparison
                : "Current Comparison";
        }

        typeBox.SelectionChanged += (_, _) =>
        {
            if (typeBox.SelectedItem is string type)
            {
                column.Type = type;
                RefreshComparisons();
            }
        };

        comparisonBox.SelectionChanged += (_, _) =>
        {
            if (comparisonBox.SelectedItem is string comparison)
            {
                column.Comparison = comparison;
            }
        };

        var timingBox = new ComboBox
        {
            Name = $"Column{index}TimingMethodComboBox",
            ItemsSource = ColumnSettings.TimingMethodNames,
            SelectedItem = ColumnSettings.TimingMethodNames.Contains(column.TimingMethod) ? column.TimingMethod : "Current Timing Method",
            Margin = new Thickness(0, 2, 4, 2),
        };
        timingBox.SelectionChanged += (_, _) =>
        {
            if (timingBox.SelectedItem is string timingMethod)
            {
                column.TimingMethod = timingMethod;
            }
        };

        var moveUpButton = new Button
        {
            Name = $"Column{index}MoveUpButton",
            Content = "Move Up",
            IsEnabled = index > 0,
            Margin = new Thickness(0, 2, 4, 2),
        };
        moveUpButton.Command = new ActionCommand(() =>
        {
            if (index <= 0)
            {
                return;
            }

            settings.ColumnsList.RemoveAt(index);
            settings.ColumnsList.Insert(index - 1, column);
            rebuildColumns();
        });

        var moveDownButton = new Button
        {
            Name = $"Column{index}MoveDownButton",
            Content = "Move Down",
            IsEnabled = index < settings.ColumnsList.Count - 1,
            Margin = new Thickness(0, 2, 4, 2),
        };
        moveDownButton.Command = new ActionCommand(() =>
        {
            if (index >= settings.ColumnsList.Count - 1)
            {
                return;
            }

            settings.ColumnsList.RemoveAt(index);
            settings.ColumnsList.Insert(index + 1, column);
            rebuildColumns();
        });

        var removeButton = new Button
        {
            Name = $"Column{index}RemoveButton",
            Content = "Remove",
            IsEnabled = settings.ColumnsList.Count > 1,
            Margin = new Thickness(0, 2),
        };
        removeButton.Command = new ActionCommand(() =>
        {
            if (settings.ColumnsList.Count <= 1)
            {
                return;
            }

            settings.ColumnsList.RemoveAt(index);
            rebuildColumns();
        });

        RefreshComparisons();
        Add(grid, nameBox, 0);
        Add(grid, typeBox, 1);
        Add(grid, comparisonBox, 2);
        Add(grid, timingBox, 3);
        Add(grid, moveUpButton, 4);
        Add(grid, moveDownButton, 5);
        Add(grid, removeButton, 6);
        return grid;
    }

    private static void Add(Grid grid, Control control, int column)
    {
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    private sealed class ActionCommand : ICommand
    {
        private readonly Action _execute;

        public ActionCommand(Action execute)
        {
            _execute = execute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object parameter)
            => true;

        public void Execute(object parameter)
            => _execute();
    }
}
