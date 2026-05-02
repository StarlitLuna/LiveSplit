using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.UI.Components;

using Xunit;

namespace LiveSplit.Tests.UI.Components;

public class SoundComponentMust
{
    [Fact]
    public void RoundTripSettings()
    {
        var settings = new SoundSettings
        {
            Split = "split.wav",
            SplitAheadGaining = "ahead-gaining.wav",
            SplitAheadLosing = "ahead-losing.wav",
            SplitBehindGaining = "behind-gaining.wav",
            SplitBehindLosing = "behind-losing.wav",
            BestSegment = "best.wav",
            UndoSplit = "undo.wav",
            SkipSplit = "skip.wav",
            PersonalBest = "pb.wav",
            NotAPersonalBest = "not-pb.wav",
            Reset = "reset.wav",
            Pause = "pause.wav",
            Resume = "resume.wav",
            StartTimer = "start.wav",
            OutputDevice = 2,
            GeneralVolume = 50,
            SplitVolume = 51,
            SplitAheadGainingVolume = 52,
            SplitAheadLosingVolume = 53,
            SplitBehindGainingVolume = 54,
            SplitBehindLosingVolume = 55,
            BestSegmentVolume = 56,
            UndoSplitVolume = 57,
            SkipSplitVolume = 58,
            PersonalBestVolume = 59,
            NotAPersonalBestVolume = 60,
            ResetVolume = 61,
            PauseVolume = 62,
            ResumeVolume = 63,
            StartTimerVolume = 64
        };

        var document = new XmlDocument();
        var loaded = new SoundSettings();

        loaded.SetSettings(settings.GetSettings(document));

        Assert.Equal(settings.Split, loaded.Split);
        Assert.Equal(settings.SplitAheadGaining, loaded.SplitAheadGaining);
        Assert.Equal(settings.SplitAheadLosing, loaded.SplitAheadLosing);
        Assert.Equal(settings.SplitBehindGaining, loaded.SplitBehindGaining);
        Assert.Equal(settings.SplitBehindLosing, loaded.SplitBehindLosing);
        Assert.Equal(settings.BestSegment, loaded.BestSegment);
        Assert.Equal(settings.UndoSplit, loaded.UndoSplit);
        Assert.Equal(settings.SkipSplit, loaded.SkipSplit);
        Assert.Equal(settings.PersonalBest, loaded.PersonalBest);
        Assert.Equal(settings.NotAPersonalBest, loaded.NotAPersonalBest);
        Assert.Equal(settings.Reset, loaded.Reset);
        Assert.Equal(settings.Pause, loaded.Pause);
        Assert.Equal(settings.Resume, loaded.Resume);
        Assert.Equal(settings.StartTimer, loaded.StartTimer);
        Assert.Equal(settings.OutputDevice, loaded.OutputDevice);
        Assert.Equal(settings.GeneralVolume, loaded.GeneralVolume);
        Assert.Equal(settings.SplitVolume, loaded.SplitVolume);
        Assert.Equal(settings.SplitAheadGainingVolume, loaded.SplitAheadGainingVolume);
        Assert.Equal(settings.SplitAheadLosingVolume, loaded.SplitAheadLosingVolume);
        Assert.Equal(settings.SplitBehindGainingVolume, loaded.SplitBehindGainingVolume);
        Assert.Equal(settings.SplitBehindLosingVolume, loaded.SplitBehindLosingVolume);
        Assert.Equal(settings.BestSegmentVolume, loaded.BestSegmentVolume);
        Assert.Equal(settings.UndoSplitVolume, loaded.UndoSplitVolume);
        Assert.Equal(settings.SkipSplitVolume, loaded.SkipSplitVolume);
        Assert.Equal(settings.PersonalBestVolume, loaded.PersonalBestVolume);
        Assert.Equal(settings.NotAPersonalBestVolume, loaded.NotAPersonalBestVolume);
        Assert.Equal(settings.ResetVolume, loaded.ResetVolume);
        Assert.Equal(settings.PauseVolume, loaded.PauseVolume);
        Assert.Equal(settings.ResumeVolume, loaded.ResumeVolume);
        Assert.Equal(settings.StartTimerVolume, loaded.StartTimerVolume);
    }

    [Fact]
    public void PlayConfiguredStartSoundWhenTimerStarts()
    {
        LiveSplitState state = CreateState();
        var player = new FakeSoundPlayer();
        var component = new SoundComponent(state, player);
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(path, []);
        var document = new XmlDocument();
        XmlElement settings = document.CreateElement("Settings");
        SettingsHelper.CreateSetting(document, settings, "StartTimer", path);
        SettingsHelper.CreateSetting(document, settings, "StartTimerVolume", 80);
        SettingsHelper.CreateSetting(document, settings, "GeneralVolume", 50);
        component.SetSettings(settings);

        try
        {
            var model = new TimerModel { CurrentState = state };
            model.Start();

            Assert.Collection(player.Played,
                play =>
                {
                    Assert.Equal(path, play.Path);
                    Assert.Equal(40, play.Volume);
                    Assert.Equal(0, play.OutputDevice);
                });
        }
        finally
        {
            component.Dispose();
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void StopPlayerWhenDisposed()
    {
        LiveSplitState state = CreateState();
        var player = new FakeSoundPlayer();
        var component = new SoundComponent(state, player);

        component.Dispose();

        Assert.True(player.Stopped);
        Assert.True(player.Disposed);
    }

    private static LiveSplitState CreateState()
    {
        IRun run = new StandardRunFactory().Create(new StandardComparisonGeneratorsFactory());
        return new LiveSplitState(
            run,
            null,
            new Layout(),
            new StandardLayoutSettingsFactory().Create(),
            new StandardSettingsFactory().Create())
        {
            CurrentComparison = Run.PersonalBestComparisonName,
            CurrentTimingMethod = TimingMethod.RealTime
        };
    }

    private sealed class FakeSoundPlayer : ISoundPlayer
    {
        public List<(string Path, int Volume, int OutputDevice)> Played { get; } = [];
        public bool Stopped { get; private set; }
        public bool Disposed { get; private set; }

        public void Play(string path, int volume, int outputDevice)
        {
            Played.Add((path, volume, outputDevice));
        }

        public void Stop()
        {
            Stopped = true;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
