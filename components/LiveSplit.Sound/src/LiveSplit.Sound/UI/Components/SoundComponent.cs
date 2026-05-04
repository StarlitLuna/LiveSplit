using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Platform.Storage;

using LibVLCSharp.Shared;

using LiveSplit.Model;
using LiveSplit.Options;

namespace LiveSplit.UI.Components;

public interface ISoundPlayer : IDisposable
{
    IReadOnlyList<SoundOutputDevice> GetOutputDevices();
    void Play(string path, int volume, int outputDevice);
    void Stop();
}

public sealed record SoundOutputDevice(string Description, string DeviceIdentifier);

public class SoundComponent : LogicComponent, IDeactivatableComponent
{
    public override string ComponentName => "Sound Effects";

    public bool Activated { get; set; }

    private LiveSplitState State { get; }
    private SoundSettings Settings { get; set; }
    private ISoundPlayer Player { get; }

    public SoundComponent(LiveSplitState state)
        : this(state, new LibVlcSoundPlayer())
    {
    }

    public SoundComponent(LiveSplitState state, ISoundPlayer player)
    {
        Activated = true;
        State = state;
        Settings = new SoundSettings();
        Player = player ?? throw new ArgumentNullException(nameof(player));

        State.OnStart += State_OnStart;
        State.OnSplit += State_OnSplit;
        State.OnSkipSplit += State_OnSkipSplit;
        State.OnUndoSplit += State_OnUndoSplit;
        State.OnPause += State_OnPause;
        State.OnResume += State_OnResume;
        State.OnReset += State_OnReset;
    }

    public override void Dispose()
    {
        State.OnStart -= State_OnStart;
        State.OnSplit -= State_OnSplit;
        State.OnSkipSplit -= State_OnSkipSplit;
        State.OnUndoSplit -= State_OnUndoSplit;
        State.OnPause -= State_OnPause;
        State.OnResume -= State_OnResume;
        State.OnReset -= State_OnReset;

        Player.Stop();
        Player.Dispose();
    }

    public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
    }

    public override Avalonia.Controls.Control GetSettingsControl(LayoutMode mode)
        => SoundSettingsControl.Build(Settings, Player.GetOutputDevices());

    public override XmlNode GetSettings(XmlDocument document)
        => Settings.GetSettings(document);

    public override void SetSettings(XmlNode settings)
        => Settings.SetSettings(settings);

    private void State_OnStart(object sender, EventArgs e)
        => PlaySound(Settings.StartTimer, Settings.StartTimerVolume);

    private void State_OnSplit(object sender, EventArgs e)
    {
        if (State.CurrentPhase == TimerPhase.Ended)
        {
            if (State.Run.Last().PersonalBestSplitTime[State.CurrentTimingMethod] == null
                || State.Run.Last().SplitTime[State.CurrentTimingMethod]
                < State.Run.Last().PersonalBestSplitTime[State.CurrentTimingMethod])
            {
                PlaySound(Settings.PersonalBest, Settings.PersonalBestVolume);
            }
            else
            {
                PlaySound(Settings.NotAPersonalBest, Settings.NotAPersonalBestVolume);
            }

            return;
        }

        string path = string.Empty;
        int volume = Settings.SplitVolume;

        int splitIndex = State.CurrentSplitIndex - 1;
        TimeSpan? timeDifference = State.Run[splitIndex].SplitTime[State.CurrentTimingMethod]
            - State.Run[splitIndex].Comparisons[State.CurrentComparison][State.CurrentTimingMethod];

        if (timeDifference != null)
        {
            if (timeDifference < TimeSpan.Zero)
            {
                path = Settings.SplitAheadGaining;
                volume = Settings.SplitAheadGainingVolume;

                if (LiveSplitStateHelper.GetPreviousSegmentDelta(
                    State, splitIndex, State.CurrentComparison, State.CurrentTimingMethod) > TimeSpan.Zero)
                {
                    path = Settings.SplitAheadLosing;
                    volume = Settings.SplitAheadLosingVolume;
                }
            }
            else
            {
                path = Settings.SplitBehindLosing;
                volume = Settings.SplitBehindLosingVolume;

                if (LiveSplitStateHelper.GetPreviousSegmentDelta(
                    State, splitIndex, State.CurrentComparison, State.CurrentTimingMethod) < TimeSpan.Zero)
                {
                    path = Settings.SplitBehindGaining;
                    volume = Settings.SplitBehindGainingVolume;
                }
            }
        }

        TimeSpan? curSegment = LiveSplitStateHelper.GetPreviousSegmentTime(State, splitIndex, State.CurrentTimingMethod);
        if (curSegment != null
            && (State.Run[splitIndex].BestSegmentTime[State.CurrentTimingMethod] == null
                || curSegment < State.Run[splitIndex].BestSegmentTime[State.CurrentTimingMethod]))
        {
            path = Settings.BestSegment;
            volume = Settings.BestSegmentVolume;
        }

        if (string.IsNullOrEmpty(path))
        {
            path = Settings.Split;
        }

        PlaySound(path, volume);
    }

    private void State_OnSkipSplit(object sender, EventArgs e)
        => PlaySound(Settings.SkipSplit, Settings.SkipSplitVolume);

    private void State_OnUndoSplit(object sender, EventArgs e)
        => PlaySound(Settings.UndoSplit, Settings.UndoSplitVolume);

    private void State_OnPause(object sender, EventArgs e)
        => PlaySound(Settings.Pause, Settings.PauseVolume);

    private void State_OnResume(object sender, EventArgs e)
        => PlaySound(Settings.Resume, Settings.ResumeVolume);

    private void State_OnReset(object sender, TimerPhase e)
    {
        if (e != TimerPhase.Ended)
        {
            PlaySound(Settings.Reset, Settings.ResetVolume);
        }
    }

    private void PlaySound(string location, int volume)
    {
        Player.Stop();

        if (!Activated || string.IsNullOrWhiteSpace(location) || !File.Exists(location))
        {
            return;
        }

        int effectiveVolume = Math.Clamp(volume * Settings.GeneralVolume / 100, 0, 100);
        Player.Play(location, effectiveVolume, Settings.OutputDevice);
    }

    public int GetSettingsHashCode()
        => Settings.GetSettingsHashCode();
}

internal static class SoundSettingsControl
{
    private static readonly SoundPathSetting[] SoundPathSettings =
    [
        new("Split", "Split:", s => s.Split, (s, v) => s.Split = v),
        new("SplitAheadGaining", "Split Ahead (Gaining Time):", s => s.SplitAheadGaining, (s, v) => s.SplitAheadGaining = v),
        new("SplitAheadLosing", "Split Ahead (Losing Time):", s => s.SplitAheadLosing, (s, v) => s.SplitAheadLosing = v),
        new("SplitBehindGaining", "Split Behind (Gaining Time):", s => s.SplitBehindGaining, (s, v) => s.SplitBehindGaining = v),
        new("SplitBehindLosing", "Split Behind (Losing Time):", s => s.SplitBehindLosing, (s, v) => s.SplitBehindLosing = v),
        new("BestSegment", "Best Segment:", s => s.BestSegment, (s, v) => s.BestSegment = v),
        new("UndoSplit", "Undo Split:", s => s.UndoSplit, (s, v) => s.UndoSplit = v),
        new("SkipSplit", "Skip Split:", s => s.SkipSplit, (s, v) => s.SkipSplit = v),
        new("PersonalBest", "Personal Best:", s => s.PersonalBest, (s, v) => s.PersonalBest = v),
        new("NotAPersonalBest", "Not A Personal Best:", s => s.NotAPersonalBest, (s, v) => s.NotAPersonalBest = v),
        new("Reset", "Reset:", s => s.Reset, (s, v) => s.Reset = v),
        new("Pause", "Pause:", s => s.Pause, (s, v) => s.Pause = v),
        new("Resume", "Resume:", s => s.Resume, (s, v) => s.Resume = v),
        new("StartTimer", "Start Timer:", s => s.StartTimer, (s, v) => s.StartTimer = v),
    ];

    private static readonly SoundVolumeSetting[] VolumeSettings =
    [
        new("GeneralVolume", "General Volume:", s => s.GeneralVolume, (s, v) => s.GeneralVolume = v),
        new("SplitVolume", "Split:", s => s.SplitVolume, (s, v) => s.SplitVolume = v),
        new("SplitAheadGainingVolume", "Split Ahead (Gaining Time):", s => s.SplitAheadGainingVolume, (s, v) => s.SplitAheadGainingVolume = v),
        new("SplitAheadLosingVolume", "Split Ahead (Losing Time):", s => s.SplitAheadLosingVolume, (s, v) => s.SplitAheadLosingVolume = v),
        new("SplitBehindGainingVolume", "Split Behind (Gaining Time):", s => s.SplitBehindGainingVolume, (s, v) => s.SplitBehindGainingVolume = v),
        new("SplitBehindLosingVolume", "Split Behind (Losing Time):", s => s.SplitBehindLosingVolume, (s, v) => s.SplitBehindLosingVolume = v),
        new("BestSegmentVolume", "Best Segment:", s => s.BestSegmentVolume, (s, v) => s.BestSegmentVolume = v),
        new("UndoSplitVolume", "Undo Split:", s => s.UndoSplitVolume, (s, v) => s.UndoSplitVolume = v),
        new("SkipSplitVolume", "Skip Split:", s => s.SkipSplitVolume, (s, v) => s.SkipSplitVolume = v),
        new("PersonalBestVolume", "Personal Best:", s => s.PersonalBestVolume, (s, v) => s.PersonalBestVolume = v),
        new("NotAPersonalBestVolume", "Not A Personal Best:", s => s.NotAPersonalBestVolume, (s, v) => s.NotAPersonalBestVolume = v),
        new("ResetVolume", "Reset:", s => s.ResetVolume, (s, v) => s.ResetVolume = v),
        new("PauseVolume", "Pause:", s => s.PauseVolume, (s, v) => s.PauseVolume = v),
        new("ResumeVolume", "Resume:", s => s.ResumeVolume, (s, v) => s.ResumeVolume = v),
        new("StartTimerVolume", "Start Timer:", s => s.StartTimerVolume, (s, v) => s.StartTimerVolume = v),
    ];

    public static Control Build(SoundSettings settings, IReadOnlyList<SoundOutputDevice> outputDevices)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(outputDevices);
        if (outputDevices.Count == 0)
        {
            outputDevices = [LibVlcSoundPlayer.DefaultOutputDevice];
        }

        return new TabControl
        {
            ItemsSource = new[]
            {
                new TabItem { Header = "Sound Files", Content = BuildSoundFilesTab(settings) },
                new TabItem { Header = "Volumes", Content = BuildVolumesTab(settings, outputDevices) },
            },
        };
    }

    private static Control BuildSoundFilesTab(SoundSettings settings)
    {
        var grid = new Grid
        {
            Margin = new Thickness(7),
            ColumnDefinitions = new ColumnDefinitions("178,*,81"),
        };

        for (int row = 0; row < SoundPathSettings.Length; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            SoundPathSetting item = SoundPathSettings[row];
            AddLabel(grid, item.Label, row);

            var pathBox = new TextBox
            {
                Name = item.Key + "PathTextBox",
                Text = item.Getter(settings) ?? string.Empty,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 2),
            };
            pathBox.PropertyChanged += (_, args) =>
            {
                if (args.Property == TextBox.TextProperty)
                {
                    item.Setter(settings, pathBox.Text ?? string.Empty);
                }
            };

            var browseButton = new Button
            {
                Content = "Browse...",
                Width = 75,
                Margin = new Thickness(6, 2, 0, 2),
            };
            browseButton.Click += async (_, _) => await BrowseSound(item, settings, pathBox, browseButton);

            Grid.SetColumn(pathBox, 1);
            Grid.SetRow(pathBox, row);
            Grid.SetColumn(browseButton, 2);
            Grid.SetRow(browseButton, row);
            grid.Children.Add(pathBox);
            grid.Children.Add(browseButton);
        }

        return new ScrollViewer { Content = grid };
    }

    private static Control BuildVolumesTab(SoundSettings settings, IReadOnlyList<SoundOutputDevice> outputDevices)
    {
        var grid = new Grid
        {
            Margin = new Thickness(7),
            ColumnDefinitions = new ColumnDefinitions("178,*"),
        };

        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        AddLabel(grid, "Output Device:", row: 0);
        var outputDeviceBox = new ComboBox
        {
            Name = "OutputDeviceComboBox",
            ItemsSource = outputDevices.Select(x => x.Description).ToArray(),
            SelectedIndex = Math.Clamp(settings.OutputDevice, 0, outputDevices.Count - 1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 2),
        };
        outputDeviceBox.SelectionChanged += (_, _) => settings.OutputDevice = Math.Max(0, outputDeviceBox.SelectedIndex);
        Grid.SetColumn(outputDeviceBox, 1);
        Grid.SetRow(outputDeviceBox, 0);
        grid.Children.Add(outputDeviceBox);

        for (int index = 0; index < VolumeSettings.Length; index++)
        {
            int row = index + 1;
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            SoundVolumeSetting item = VolumeSettings[index];
            AddLabel(grid, item.Label, row);

            var slider = new Slider
            {
                Name = item.Key + "Slider",
                Minimum = 0,
                Maximum = 100,
                Value = item.Getter(settings),
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 2),
            };
            slider.PropertyChanged += (_, args) =>
            {
                if (args.Property == global::Avalonia.Controls.Primitives.RangeBase.ValueProperty)
                {
                    item.Setter(settings, (int)Math.Round(slider.Value));
                }
            };

            Grid.SetColumn(slider, 1);
            Grid.SetRow(slider, row);
            grid.Children.Add(slider);
        }

        return new ScrollViewer { Content = grid };
    }

    private static void AddLabel(Grid grid, string text, int row)
    {
        var label = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2),
        };
        Grid.SetColumn(label, 0);
        Grid.SetRow(label, row);
        grid.Children.Add(label);
    }

    private static async Task BrowseSound(SoundPathSetting item, SoundSettings settings, TextBox pathBox, Control ownerControl)
    {
        TopLevel top = TopLevel.GetTopLevel(ownerControl);
        if (top is null)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Sound",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Audio Files")
                {
                    Patterns = ["*.mp3", "*.wav", "*.aiff", "*.wma"],
                },
                FilePickerFileTypes.All,
            ],
        });

        IStorageFile picked = files?.FirstOrDefault();
        if (picked?.Path?.LocalPath is not { Length: > 0 } path)
        {
            return;
        }

        item.Setter(settings, path);
        pathBox.Text = path;
    }

    private sealed record SoundPathSetting(
        string Key,
        string Label,
        Func<SoundSettings, string> Getter,
        Action<SoundSettings, string> Setter);

    private sealed record SoundVolumeSetting(
        string Key,
        string Label,
        Func<SoundSettings, int> Getter,
        Action<SoundSettings, int> Setter);
}

internal sealed class LibVlcSoundPlayer : ISoundPlayer
{
    private readonly object _sync = new();
    private LibVLC _libVlc;
    private MediaPlayer _player;

    public IReadOnlyList<SoundOutputDevice> GetOutputDevices()
    {
        try
        {
            lock (_sync)
            {
                EnsurePlayer();
                return EnumerateOutputDevices();
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
            return [DefaultOutputDevice];
        }
    }

    public void Play(string path, int volume, int outputDevice)
    {
        try
        {
            lock (_sync)
            {
                EnsurePlayer();
                ApplyOutputDevice(outputDevice);
                using var media = new Media(_libVlc, new Uri(Path.GetFullPath(path)));
                _player.Volume = volume;
                _player.Play(media);
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    public void Stop()
    {
        try
        {
            lock (_sync)
            {
                _player?.Stop();
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _player?.Dispose();
            _player = null;
            _libVlc?.Dispose();
            _libVlc = null;
        }
    }

    internal static SoundOutputDevice DefaultOutputDevice { get; } = new("Default", string.Empty);

    internal static string SelectOutputDeviceIdentifier(IReadOnlyList<SoundOutputDevice> outputDevices, int selectedIndex)
    {
        if (selectedIndex <= 0 || selectedIndex >= outputDevices.Count)
        {
            return null;
        }

        string deviceIdentifier = outputDevices[selectedIndex].DeviceIdentifier;
        return string.IsNullOrWhiteSpace(deviceIdentifier) ? null : deviceIdentifier;
    }

    private void EnsurePlayer()
    {
        if (_player != null)
        {
            return;
        }

        string libVlcDirectory = LibVlcRuntime.FindWindowsLibVlcDirectory();
        if (libVlcDirectory is null)
        {
            Core.Initialize();
        }
        else
        {
            Core.Initialize(libVlcDirectory);
        }
        _libVlc = new LibVLC("--no-video");
        _player = new MediaPlayer(_libVlc);
    }

    private void ApplyOutputDevice(int outputDevice)
    {
        string deviceIdentifier = SelectOutputDeviceIdentifier(EnumerateOutputDevices(), outputDevice);
        if (!string.IsNullOrWhiteSpace(deviceIdentifier))
        {
            _player.SetOutputDevice(deviceIdentifier);
        }
    }

    private IReadOnlyList<SoundOutputDevice> EnumerateOutputDevices()
    {
        List<SoundOutputDevice> devices = [DefaultOutputDevice];

        var audioDevices = _player.AudioOutputDeviceEnum;
        if (audioDevices is null)
        {
            return devices;
        }

        foreach (var device in audioDevices)
        {
            string description = string.IsNullOrWhiteSpace(device.Description)
                ? device.DeviceIdentifier
                : device.Description;
            if (!string.IsNullOrWhiteSpace(description))
            {
                devices.Add(new SoundOutputDevice(description, device.DeviceIdentifier));
            }
        }

        return devices;
    }
}

internal static class LibVlcRuntime
{
    public static string FindWindowsLibVlcDirectory()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        string rid = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "win-x86",
            Architecture.X64 => "win-x64",
            Architecture.Arm64 => "win-arm64",
            _ => null
        };

        if (rid is null)
        {
            return null;
        }

        string baseDirectory = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDirectory, "Components", "runtimes", rid, "libvlc", rid),
            Path.Combine(baseDirectory, "Components", "libvlc", rid),
            Path.Combine(baseDirectory, "libvlc", rid)
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "libvlc.dll"))
                && File.Exists(Path.Combine(candidate, "libvlccore.dll")))
            {
                return candidate;
            }
        }

        return null;
    }
}
