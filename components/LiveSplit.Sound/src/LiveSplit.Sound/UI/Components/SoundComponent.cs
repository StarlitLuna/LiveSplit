using System;
using System.IO;
using System.Linq;
using System.Xml;

using LibVLCSharp.Shared;

using LiveSplit.Model;
using LiveSplit.Options;

namespace LiveSplit.UI.Components;

public interface ISoundPlayer : IDisposable
{
    void Play(string path, int volume, int outputDevice);
    void Stop();
}

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
        => AvaloniaSettingsBuilder.Build(Settings, ComponentName);

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

internal sealed class LibVlcSoundPlayer : ISoundPlayer
{
    private readonly object _sync = new();
    private LibVLC _libVlc;
    private MediaPlayer _player;

    public void Play(string path, int volume, int outputDevice)
    {
        try
        {
            lock (_sync)
            {
                EnsurePlayer();
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

    private void EnsurePlayer()
    {
        if (_player != null)
        {
            return;
        }

        Core.Initialize();
        _libVlc = new LibVLC("--no-video");
        _player = new MediaPlayer(_libVlc);
    }
}
