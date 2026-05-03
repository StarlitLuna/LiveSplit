using System;
using System.IO;
using System.Collections.Generic;

using global::Avalonia;

using LiveSplit.Avalonia;
using LiveSplit.Model;
using LiveSplit.Options;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class TimerWindowShellMust
{
    [Fact]
    public void MatchMasterTimerWindowShellDefaults()
    {
        string xaml = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/TimerWindow.axaml"));

        Assert.Contains("SystemDecorations=\"None\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"Black\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CompensateAvaloniaBorderlessBoundsToMatchMasterFormSize()
    {
        (int windowWidth, int windowHeight) = TimerWindow.GetWindowSizeForLayout(252, 50);

        Assert.Equal(255, windowWidth);
        Assert.Equal(51, windowHeight);

        (int layoutWidth, int layoutHeight) = TimerWindow.GetLayoutSizeForWindow(255, 51);

        Assert.Equal(252, layoutWidth);
        Assert.Equal(50, layoutHeight);
    }

    [Fact]
    public void EnableMousePassThroughOnlyWhileRunningAndNotForeground()
    {
        var settings = new LayoutSettings { MousePassThroughWhileRunning = true };

        Assert.False(TimerWindow.ShouldUseMousePassThrough(settings, TimerPhase.Running, isForeground: true));
        Assert.True(TimerWindow.ShouldUseMousePassThrough(settings, TimerPhase.Running, isForeground: false));
        Assert.False(TimerWindow.ShouldUseMousePassThrough(settings, TimerPhase.NotRunning, isForeground: false));

        settings.MousePassThroughWhileRunning = false;
        Assert.False(TimerWindow.ShouldUseMousePassThrough(settings, TimerPhase.Running, isForeground: false));
    }

    [Fact]
    public void ClampSavedLayoutPositionInsideAvailableScreenBounds()
    {
        var screen = new PixelRect(0, 0, 500, 400);

        Assert.Equal(
            new PixelPoint(0, 0),
            TimerWindow.ClampLayoutPosition(new PixelPoint(-20, -10), new Size(100, 100), [screen]));
        Assert.Equal(
            new PixelPoint(400, 300),
            TimerWindow.ClampLayoutPosition(new PixelPoint(900, 800), new Size(100, 100), [screen]));
    }

    [Fact]
    public void PreservePreviousServerStateWhenRestartingPreviousStateServerSettings()
    {
        Assert.Equal(
            ServerStateType.TCP,
            TimerWindow.ResolveServerStateForSettingsRestart(
                ServerStartupType.PreviousState,
                runningServerState: ServerStateType.TCP,
                savedServerState: ServerStateType.Off));
    }

    [Fact]
    public void TrackWindowResizeWithoutMarkingLayoutDirty()
    {
        var layout = new LiveSplit.UI.Layout
        {
            Mode = LiveSplit.UI.LayoutMode.Vertical,
            VerticalWidth = 252,
            VerticalHeight = 50,
        };
        (int expectedWidth, int expectedHeight) = TimerWindow.GetLayoutSizeForWindow(300, 80);

        Assert.True(TimerWindow.UpdateLayoutSizeFromWindowSize(layout, new Size(300, 80)));

        Assert.Equal(expectedWidth, layout.VerticalWidth);
        Assert.Equal(expectedHeight, layout.VerticalHeight);
        Assert.False(layout.HasChanged);
    }

    [Fact]
    public void PromptForLayoutOnCloseSplitsOnlyWhenMasterWouldPrompt()
    {
        var normalLayout = new LiveSplit.UI.Layout
        {
            HasChanged = true,
        };
        normalLayout.LayoutComponents.Add(new LiveSplit.UI.Components.LayoutComponent("Splits", new FakeComponent("Splits")));

        var timerOnlyLayout = new LiveSplit.UI.Layout
        {
            HasChanged = true,
        };
        timerOnlyLayout.LayoutComponents.Add(new LiveSplit.UI.Components.LayoutComponent("Timer", new FakeComponent("Timer")));

        Assert.True(TimerWindow.ShouldPromptForLayoutOnCloseSplits(normalLayout));
        Assert.False(TimerWindow.ShouldPromptForLayoutOnCloseSplits(timerOnlyLayout));

        normalLayout.HasChanged = false;
        Assert.False(TimerWindow.ShouldPromptForLayoutOnCloseSplits(normalLayout));
    }

    [Fact]
    public void PromptForSplitsSaveOnlyOutsideTimerOnlyModeWhenRunIsDirty()
    {
        var run = new LiveSplit.Model.Run(new LiveSplit.Model.Comparisons.StandardComparisonGeneratorsFactory())
        {
            HasChanged = true,
        };

        Assert.True(TimerWindow.ShouldPromptForSplitsSave(inTimerOnlyMode: false, run));
        Assert.False(TimerWindow.ShouldPromptForSplitsSave(inTimerOnlyMode: true, run));

        run.HasChanged = false;
        Assert.False(TimerWindow.ShouldPromptForSplitsSave(inTimerOnlyMode: false, run));
    }

    [Fact]
    public void PromptForBestTimeUpdatesOnlyOutsideTimerOnlyMode()
    {
        LiveSplit.Model.IRun run = new LiveSplit.Model.RunFactories.StandardRunFactory()
            .Create(new LiveSplit.Model.Comparisons.StandardComparisonGeneratorsFactory());
        run[0].PersonalBestSplitTime = new LiveSplit.Model.Time(realTime: TimeSpan.FromMinutes(2));
        run[0].SplitTime = new LiveSplit.Model.Time(realTime: TimeSpan.FromMinutes(1));
        var settings = new LiveSplit.Options.SettingsFactories.StandardSettingsFactory().Create();
        var state = new LiveSplit.Model.LiveSplitState(
            run,
            null,
            new LiveSplit.UI.Layout(),
            new LiveSplit.Options.SettingsFactories.StandardLayoutSettingsFactory().Create(),
            settings)
        {
            CurrentTimingMethod = LiveSplit.Model.TimingMethod.RealTime,
        };

        Assert.True(TimerWindow.ShouldPromptToUpdateTimesOnReset(state, inTimerOnlyMode: false));
        Assert.False(TimerWindow.ShouldPromptToUpdateTimesOnReset(state, inTimerOnlyMode: true));

        settings.WarnOnReset = false;
        Assert.False(TimerWindow.ShouldPromptToUpdateTimesOnReset(state, inTimerOnlyMode: false));
    }

    private static string FindRepoFile(string relativePath)
    {
        DirectoryInfo directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }

    private sealed class FakeComponent : LiveSplit.UI.Components.IComponent
    {
        public FakeComponent(string componentName)
        {
            ComponentName = componentName;
        }

        public string ComponentName { get; }
        public float HorizontalWidth => 10;
        public float MinimumHeight => 1;
        public float MinimumWidth => 1;
        public float PaddingBottom => 0;
        public float PaddingLeft => 0;
        public float PaddingRight => 0;
        public float PaddingTop => 0;
        public float VerticalHeight => 10;
        public IDictionary<string, Action> ContextMenuControls { get; } = new Dictionary<string, Action>();

        public void Dispose()
        {
        }

        public void DrawHorizontal(LiveSplit.UI.Drawing.IDrawingContext context, LiveSplit.Model.LiveSplitState state, float height)
        {
        }

        public void DrawVertical(LiveSplit.UI.Drawing.IDrawingContext context, LiveSplit.Model.LiveSplitState state, float width)
        {
        }

        public global::Avalonia.Controls.Control GetSettingsControl(LiveSplit.UI.LayoutMode mode)
            => new global::Avalonia.Controls.Panel();

        public System.Xml.XmlNode GetSettings(System.Xml.XmlDocument document)
            => document.CreateElement("Settings");

        public void SetSettings(System.Xml.XmlNode settings)
        {
        }

        public void Update(LiveSplit.UI.IInvalidator invalidator, LiveSplit.Model.LiveSplitState state, float width, float height, LiveSplit.UI.LayoutMode mode)
        {
        }
    }
}
