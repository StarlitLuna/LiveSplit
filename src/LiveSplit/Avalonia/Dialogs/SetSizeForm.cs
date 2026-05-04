using System;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>
/// Lets the user set explicit width/height on the timer window and optionally lock the
/// aspect ratio.
/// </summary>
public sealed class SetSizeForm : Window
{
    private readonly Window _target;
    private readonly TaskCompletionSource<bool> _result = new();

    private NumericUpDown _widthBox;
    private NumericUpDown _heightBox;
    private CheckBox _keepRatio;
    private readonly double _originalWidth;
    private readonly double _originalHeight;
    private bool _suppressFeedback;

    public SetSizeForm(Window target)
    {
        _target = target;
        _originalWidth = target.Width;
        _originalHeight = target.Height;

        Title = "Set Size";
        Width = 320;
        Height = 220;
        DialogTheme.ApplyWindow(this);
        CanResize = false;

        _widthBox = new NumericUpDown { Minimum = 1, Maximum = 9999, Value = (decimal)target.Width };
        _heightBox = new NumericUpDown { Minimum = 1, Maximum = 9999, Value = (decimal)target.Height };
        _keepRatio = new CheckBox { Content = "Keep Aspect Ratio" };

        _widthBox.ValueChanged += OnWidthChanged;
        _heightBox.ValueChanged += OnHeightChanged;

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            Margin = new Thickness(20),
        };
        Add(grid, new TextBlock { Text = "Width:", VerticalAlignment = VerticalAlignment.Center }, 0, 0);
        Add(grid, _widthBox, 0, 1);
        Add(grid, new TextBlock { Text = "Height:", VerticalAlignment = VerticalAlignment.Center }, 1, 0);
        Add(grid, _heightBox, 1, 1);
        Add(grid, _keepRatio, 2, 1);

        var ok = new Button { Content = "OK", Width = 80, IsDefault = true };
        ok.Click += (_, _) => { _result.TrySetResult(true); Close(); };
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        cancel.Click += (_, _) =>
        {
            RestoreOriginalSize();
            _result.TrySetResult(false);
            Close();
        };

        var buttonBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 0, 12, 12),
            Children = { ok, cancel },
        };

        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(buttonBar, Dock.Bottom);
        root.Children.Add(buttonBar);
        root.Children.Add(grid);
        Content = root;

        Closed += (_, _) =>
        {
            if (!_result.Task.IsCompleted)
            {
                RestoreOriginalSize();
                _result.TrySetResult(false);
            }
        };
    }

    private void OnWidthChanged(object sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_suppressFeedback || e.NewValue is null)
        {
            return;
        }

        double newWidth = (double)e.NewValue.Value;
        _target.Width = newWidth;
        if (_keepRatio.IsChecked == true && _originalWidth > 0)
        {
            _suppressFeedback = true;
            try
            {
                double newHeight = Math.Round(_originalHeight * newWidth / _originalWidth);
                _heightBox.Value = (decimal)newHeight;
                _target.Height = newHeight;
            }
            finally
            {
                _suppressFeedback = false;
            }
        }
    }

    private void OnHeightChanged(object sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_suppressFeedback || e.NewValue is null)
        {
            return;
        }

        double newHeight = (double)e.NewValue.Value;
        _target.Height = newHeight;
        if (_keepRatio.IsChecked == true && _originalHeight > 0)
        {
            _suppressFeedback = true;
            try
            {
                double newWidth = Math.Round(_originalWidth * newHeight / _originalHeight);
                _widthBox.Value = (decimal)newWidth;
                _target.Width = newWidth;
            }
            finally
            {
                _suppressFeedback = false;
            }
        }
    }

    private void RestoreOriginalSize()
    {
        _target.Width = _originalWidth;
        _target.Height = _originalHeight;
    }

    private static void Add(Grid grid, Control control, int row, int col)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, col);
        grid.Children.Add(control);
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
