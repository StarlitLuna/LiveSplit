using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;

using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.Web.Share;

namespace LiveSplit.Avalonia.Dialogs;

public sealed class SpeedrunComSubmitDialog : Window
{
    private readonly RunMetadata _metadata;
    private readonly bool _hasPersonalBestDateTime;
    private readonly TextBox _videoBox;
    private readonly TextBox _commentBox;
    private readonly TextBox _dateBox;
    private readonly TextBox _withoutLoadsBox;
    private readonly TextBox _gameTimeBox;
    private readonly TextBlock _statusBlock;
    private readonly TaskCompletionSource<bool> _result = new();

    public SpeedrunComSubmitDialog(RunMetadata metadata, string initialComment = "")
    {
        _metadata = metadata;
        _hasPersonalBestDateTime = SpeedrunCom.FindPersonalBestAttemptDate(metadata.LiveSplitRun).HasValue;

        Title = "Submitting to Speedrun.com";
        Width = 460;
        Height = 360;
        MinWidth = 360;
        MinHeight = 300;

        var fields = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 8
        };

        _videoBox = new TextBox { MaxLength = 255 };
        fields.Children.Add(new TextBlock { Text = "Video:" });
        fields.Children.Add(_videoBox);

        _commentBox = new TextBox
        {
            Text = initialComment ?? string.Empty,
            MaxLength = 2000,
            MinHeight = 90,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap
        };
        fields.Children.Add(new TextBlock { Text = "Comment:" });
        fields.Children.Add(_commentBox);

        if (!_hasPersonalBestDateTime)
        {
            _dateBox = new TextBox { Text = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
            fields.Children.Add(new TextBlock { Text = "Date:" });
            fields.Children.Add(_dateBox);
        }

        Time runTime = metadata.LiveSplitRun.Last().PersonalBestSplitTime;
        ReadOnlyCollection<SpeedrunComSharp.TimingMethod> timingMethods = metadata.Game.Ruleset.TimingMethods;
        bool usesGameTime = timingMethods.Contains(SpeedrunComSharp.TimingMethod.GameTime);
        bool usesWithoutLoads = timingMethods.Contains(SpeedrunComSharp.TimingMethod.RealTimeWithoutLoads);
        bool usesBoth = usesGameTime && usesWithoutLoads;

        if (!runTime.GameTime.HasValue || usesBoth)
        {
            if (usesWithoutLoads)
            {
                _withoutLoadsBox = new TextBox();
                fields.Children.Add(new TextBlock { Text = "Without Loads:" });
                fields.Children.Add(_withoutLoadsBox);
            }

            if (usesGameTime)
            {
                _gameTimeBox = new TextBox();
                fields.Children.Add(new TextBlock { Text = "Game Time:" });
                fields.Children.Add(_gameTimeBox);
            }
        }

        _statusBlock = new TextBlock
        {
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap
        };
        fields.Children.Add(_statusBlock);

        var submit = new Button { Content = "Submit", Width = 88, IsDefault = true };
        submit.Click += async (_, _) => await Submit();
        var cancel = new Button { Content = "Cancel", Width = 88, IsCancel = true };
        cancel.Click += (_, _) =>
        {
            _result.TrySetResult(false);
            Close();
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 0, 12, 12),
            Children = { cancel, submit }
        };

        var scroll = new ScrollViewer { Content = fields };
        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(scroll);
        Content = root;

        Opened += (_, _) => _videoBox.Focus();
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

    private async Task Submit()
    {
        if (!SpeedrunComSubmissionOptions.TryNormalizeVideoUri(_videoBox.Text, out Uri videoUri, out string videoError))
        {
            await new MessageDialog("Submitting Failed", videoError).ShowDialogAsync(this);
            return;
        }

        DateTime? date = null;
        if (!_hasPersonalBestDateTime)
        {
            if (!DateTime.TryParseExact(
                    _dateBox.Text,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTime parsedDate))
            {
                await new MessageDialog("Submitting Failed", "You didn't enter a valid date.").ShowDialogAsync(this);
                return;
            }

            date = parsedDate.Date;
        }

        var gameTime = await TryReadOptionalTime(_gameTimeBox, "You didn't enter a valid Game Time.");
        if (!gameTime.IsValid)
        {
            return;
        }

        SpeedrunComSubmissionOptions.PatchGameTime(_metadata.LiveSplitRun, gameTime.Time);

        var withoutLoads = await TryReadOptionalTime(_withoutLoadsBox, "You didn't enter a valid Real Time without Loads.");
        if (!withoutLoads.IsValid)
        {
            return;
        }

        _statusBlock.Text = "Submitting...";
        bool submitted = SpeedrunCom.SubmitRun(
            _metadata.LiveSplitRun,
            out string reason,
            comment: _commentBox.Text,
            videoUri: videoUri,
            date: date,
            withoutLoads: withoutLoads.Time);

        if (submitted)
        {
            try
            {
                if (_metadata.Run?.WebLink is { } link)
                {
                    Process.Start(new ProcessStartInfo(link.AbsoluteUri) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            _result.TrySetResult(true);
            Close();
            return;
        }

        _statusBlock.Text = reason;
        await new MessageDialog("Submitting Failed", reason ?? "The run could not be submitted.").ShowDialogAsync(this);
    }

    private async Task<(bool IsValid, TimeSpan? Time)> TryReadOptionalTime(TextBox box, string error)
    {
        if (box is null)
        {
            return (true, null);
        }

        if (SpeedrunComSubmissionOptions.TryParseOptionalTime(box.Text, out TimeSpan? time, out _))
        {
            return (true, time);
        }

        await new MessageDialog("Submitting Failed", error).ShowDialogAsync(this);
        return (false, null);
    }
}
