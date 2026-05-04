using System;
using System.Numerics;

using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.UI.Drawing;
using LiveSplit.UI.Drawing.Skia;

using Xunit;

namespace LiveSplit.Tests.UI.Components;

[Collection("DrawingApi")]
public class DetailedTimerMust
{
    [Fact]
    public void ContinueInvalidatingAnimatedIconLoadedFromRawIconBytes()
    {
        DrawingApi.Register(new SkiaDrawingFactory());
        LiveSplitState state = CreateState();
        var component = new InspectableDetailedTimer(state)
        {
            Settings =
            {
                DisplayIcon = true,
            },
        };
        var invalidator = new RecordingInvalidator();

        component.Update(invalidator, state, 300, 120, LayoutMode.Vertical);
        Assert.True(component.CurrentFrameCount > 1);
        invalidator.Count = 0;

        component.Update(invalidator, state, 300, 120, LayoutMode.Vertical);

        Assert.True(invalidator.Count > 0);
    }

    private static LiveSplitState CreateState()
    {
        IRun run = new StandardRunFactory().Create(new StandardComparisonGeneratorsFactory());
        run.Clear();
        run.Add(new Segment("Intro")
        {
            IconPng = AnimatedGifBytes(),
        });
        var state = new LiveSplitState(
            run,
            null,
            new Layout(),
            new StandardLayoutSettingsFactory().Create(),
            new StandardSettingsFactory().Create())
        {
            CurrentComparison = Run.PersonalBestComparisonName,
            CurrentTimingMethod = TimingMethod.RealTime,
            CurrentSplitIndex = 0,
        };
        return state;
    }

    private static byte[] AnimatedGifBytes()
        => Convert.FromBase64String(
            "R0lGODlhAQABAIEAAAAAAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQACgAAACwAAAAAAQABAAAIBAABBAQAIfkEAAoAAAAsAAAAAAEAAQAAAggAAQQEADs=");

    private sealed class RecordingInvalidator : IInvalidator
    {
        public Matrix3x2 Transform { get; set; } = Matrix3x2.Identity;
        public int Count { get; set; }

        public void Invalidate(float x, float y, float width, float height)
        {
            Count++;
        }
    }

    private sealed class InspectableDetailedTimer(LiveSplitState state) : DetailedTimer(state)
    {
        public int CurrentFrameCount => FrameCount;
    }
}
