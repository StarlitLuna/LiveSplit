using System;
using System.Xml;

using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.UI.Components;

using Xunit;

namespace LiveSplit.Tests.UI.Components;

public class VideoComponentMust
{
    [Fact]
    public void RoundTripSettings()
    {
        var settings = new VideoSettings
        {
            VideoPath = @"C:\runs\pb.mp4",
            Offset = TimeSpan.FromSeconds(12.34),
            Height = 240,
            Width = 320
        };

        var document = new XmlDocument();
        var loaded = new VideoSettings();

        loaded.SetSettings(settings.GetSettings(document));

        Assert.Equal(settings.VideoPath, loaded.VideoPath);
        Assert.Equal(settings.Offset, loaded.Offset);
        Assert.Equal(settings.Height, loaded.Height);
        Assert.Equal(settings.Width, loaded.Width);
    }

    [Fact]
    public void LoadPlayAndSynchronizeWhenTimerStarts()
    {
        LiveSplitState state = CreateState();
        var player = new FakeVideoPlayer();
        var component = new VideoComponent(state, player);
        var document = new XmlDocument();
        XmlElement settings = document.CreateElement("Settings");
        SettingsHelper.CreateSetting(document, settings, "VideoPath", @"C:\runs\pb.mp4");
        SettingsHelper.CreateSetting(document, settings, "Offset", "0:00:03.000");
        SettingsHelper.CreateSetting(document, settings, "Height", 240f);
        SettingsHelper.CreateSetting(document, settings, "Width", 320f);
        component.SetSettings(settings);

        var model = new TimerModel { CurrentState = state };
        model.Start();

        Assert.Equal(@"C:\runs\pb.mp4", player.LoadedPath);
        Assert.True(player.Played);
        Assert.True(
            Math.Abs((player.LastTime.Value - TimeSpan.FromSeconds(3)).TotalMilliseconds) < 50,
            $"Expected synchronization near 3 seconds, got {player.LastTime}.");
    }

    [Fact]
    public void PauseResumeAndStopWithTimerState()
    {
        LiveSplitState state = CreateState();
        var player = new FakeVideoPlayer();
        var component = new VideoComponent(state, player);
        var model = new TimerModel { CurrentState = state };

        model.Start();
        model.Pause();
        model.Pause();
        model.Reset(false);

        Assert.True(player.Paused);
        Assert.True(player.Played);
        Assert.True(player.Stopped);
        component.Dispose();
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

    private sealed class FakeVideoPlayer : IVideoPlayer
    {
        public string LoadedPath { get; private set; }
        public TimeSpan? LastTime { get; private set; }
        public bool Played { get; private set; }
        public bool Paused { get; private set; }
        public bool Stopped { get; private set; }
        public bool Disposed { get; private set; }

        public void Load(string path)
        {
            LoadedPath = path;
        }

        public void Play()
        {
            Played = true;
        }

        public void Pause()
        {
            Paused = true;
        }

        public void Stop()
        {
            Stopped = true;
        }

        public void SetTime(TimeSpan time)
        {
            LastTime = time;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
