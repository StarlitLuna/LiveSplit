using System;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Threading;

using LiveSplit.Model;
using LiveSplit.TimeFormatters;

namespace LiveSplit.UI.Components;

/// <summary>
/// Always-on-top borderless entry window: shows the running game time, accepts a typed time
/// (formats accepted by <see cref="TimeSpanParser"/>: <c>1:23.45</c>, <c>83.45</c>, <c>1:30:00</c>),
/// and on Enter or Apply pushes the parsed value into <see cref="LiveSplitState.SetGameTime"/>.
/// Replaces the WinForms <c>ShitSplitter</c> popup from the Windows build.
/// </summary>
public sealed class ManualGameTimeWindow : Window
{
    private readonly LiveSplitState _state;
    private readonly TextBox _input;
    private readonly TextBlock _hint;
    private readonly DispatcherTimer _refreshTimer;

    public ManualGameTimeWindow(LiveSplitState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));

        Title = "Manual Game Time";
        Width = 240;
        Height = 110;
        SystemDecorations = SystemDecorations.BorderOnly;
        Topmost = true;
        ShowInTaskbar = false;
        Background = new SolidColorBrush(Color.FromRgb(28, 28, 28));

        _input = new TextBox
        {
            Watermark = "00:00.00",
            FontSize = 22,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        _input.KeyDown += (_, e) =>
        {
            if (e.Key == global::Avalonia.Input.Key.Enter)
            {
                Apply();
                e.Handled = true;
            }
        };

        _hint = new TextBlock
        {
            Foreground = Brushes.Gray,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
            Text = "Type the segment's game time and press Enter.",
        };

        var apply = new Button { Content = "Apply", Width = 80 };
        apply.Click += (_, _) => Apply();

        var skip = new Button { Content = "Skip", Width = 80, Margin = new Thickness(8, 0, 0, 0) };
        skip.Click += (_, _) => state.SetGameTime(null);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { apply, skip },
        };

        var stack = new StackPanel
        {
            Margin = new Thickness(10),
            Spacing = 6,
            Children = { _input, buttons, _hint },
        };
        Content = stack;

        // Refresh the watermark with the current game time so the user can see what they're
        // overriding. Cheap (≤4 Hz). Stops on Closed.
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _refreshTimer.Tick += (_, _) =>
        {
            TimeSpan? gameTime = _state.CurrentTime.GameTime;
            if (gameTime.HasValue)
            {
                _input.Watermark = new RegularTimeFormatter(TimeAccuracy.Hundredths).Format(gameTime.Value);
            }
        };
        _refreshTimer.Start();
        Closed += (_, _) => _refreshTimer.Stop();
    }

    private void Apply()
    {
        if (string.IsNullOrWhiteSpace(_input.Text))
        {
            return;
        }

        try
        {
            TimeSpan parsed = TimeSpanParser.Parse(_input.Text);
            _state.SetGameTime(parsed);
            _input.Text = string.Empty;
            _hint.Text = "Applied.";
            _hint.Foreground = Brushes.Gray;
        }
        catch
        {
            _hint.Text = "Couldn't parse. Try mm:ss.ff or h:mm:ss.";
            _hint.Foreground = Brushes.IndianRed;
        }
    }
}
