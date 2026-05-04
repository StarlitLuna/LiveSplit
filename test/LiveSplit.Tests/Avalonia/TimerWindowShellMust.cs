using System;
using System.IO;
using System.Collections.Generic;

using global::Avalonia;

using LiveSplit.Avalonia;
using LiveSplit.Model;
using LiveSplit.Options;

using Xunit;

#pragma warning disable CS0618 // TimerWindow still uses Avalonia's obsolete FileDialog API for master-compatible dialog parity.

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
    public void SelectUniqueWindowTitleLikeMaster()
    {
        Assert.Equal(
            "LiveSplit",
            TimerWindow.SelectUniqueWindowTitle([]));

        Assert.Equal(
            "LiveSplit (2)",
            TimerWindow.SelectUniqueWindowTitle(["LiveSplit", "LiveSplit (1)"]));

        Assert.Equal(
            "LiveSplit",
            TimerWindow.SelectUniqueWindowTitle(["LiveSplit (1)"]));
    }

    [Fact]
    public void UseLayoutDimensionsAsBorderlessWindowDimensions()
    {
        (int windowWidth, int windowHeight) = TimerWindow.GetWindowSizeForLayout(252, 50);

        Assert.Equal(252, windowWidth);
        Assert.Equal(50, windowHeight);

        (int layoutWidth, int layoutHeight) = TimerWindow.GetLayoutSizeForWindow(252, 50);

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
    public void LeftClickOnTimerWindowClosesOpenContextMenuBeforeDragHandling()
    {
        Assert.True(TimerWindow.ShouldCloseOpenContextMenuOnPointerPress(contextMenuOpen: true, leftButtonPressed: true));
        Assert.False(TimerWindow.ShouldCloseOpenContextMenuOnPointerPress(contextMenuOpen: false, leftButtonPressed: true));
        Assert.False(TimerWindow.ShouldCloseOpenContextMenuOnPointerPress(contextMenuOpen: true, leftButtonPressed: false));

        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/TimerWindow.axaml.cs"));
        int methodStart = source.IndexOf("private void OnPointerPressed", StringComparison.Ordinal);
        Assert.True(methodStart >= 0);
        int methodEnd = source.IndexOf("private void OnPointerMoved", methodStart, StringComparison.Ordinal);
        Assert.True(methodEnd > methodStart);
        string method = source[methodStart..methodEnd];

        Assert.True(
            method.IndexOf("CloseOpenContextMenuForPointerPress(props.IsLeftButtonPressed)", StringComparison.Ordinal)
            < method.IndexOf("GetResizeEdge(", StringComparison.Ordinal));
        Assert.True(
            method.IndexOf("CloseOpenContextMenuForPointerPress(props.IsLeftButtonPressed)", StringComparison.Ordinal)
            < method.IndexOf("BeginMoveDrag(e)", StringComparison.Ordinal));
    }

    [Fact]
    public void NativeMousePassThroughTargetsWindowsAndX11Only()
    {
        Assert.True(NativeMousePassThrough.SupportsPlatformHandleDescriptor("HWND"));
        Assert.True(NativeMousePassThrough.SupportsPlatformHandleDescriptor("XID"));
        Assert.False(NativeMousePassThrough.SupportsPlatformHandleDescriptor("Wayland"));
        Assert.False(NativeMousePassThrough.SupportsPlatformHandleDescriptor(null));
    }

    [Fact]
    public void ApplyNativeMousePassThroughWithManagedHitTesting()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/TimerWindow.axaml.cs"));

        Assert.Contains("ApplyNativeMousePassThrough(passThrough);", source, StringComparison.Ordinal);
        Assert.Contains("ApplyNativeMousePassThrough(false);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RecalculateMousePassThroughWhenForegroundStateChanges()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/TimerWindow.axaml.cs"));

        Assert.Contains("Activated += (_, _) => ApplyLayoutWindowSettings();", source, StringComparison.Ordinal);
        Assert.Contains("Deactivated += (_, _) => ApplyLayoutWindowSettings();", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, 1, global::Avalonia.Controls.WindowEdge.NorthWest)]
    [InlineData(100, 1, global::Avalonia.Controls.WindowEdge.North)]
    [InlineData(199, 1, global::Avalonia.Controls.WindowEdge.NorthEast)]
    [InlineData(1, 50, global::Avalonia.Controls.WindowEdge.West)]
    [InlineData(199, 50, global::Avalonia.Controls.WindowEdge.East)]
    [InlineData(1, 99, global::Avalonia.Controls.WindowEdge.SouthWest)]
    [InlineData(100, 99, global::Avalonia.Controls.WindowEdge.South)]
    [InlineData(199, 99, global::Avalonia.Controls.WindowEdge.SouthEast)]
    public void DetectBorderlessResizeHitZonesLikeMaster(double x, double y, global::Avalonia.Controls.WindowEdge expected)
    {
        Assert.Equal(expected, TimerWindow.GetResizeEdge(x, y, width: 200, height: 100, allowResizing: true));
    }

    [Fact]
    public void IgnoreResizeHitZonesWhenResizingIsDisabledOrPointerIsInterior()
    {
        Assert.Null(TimerWindow.GetResizeEdge(50, 50, width: 200, height: 100, allowResizing: true));
        Assert.Null(TimerWindow.GetResizeEdge(1, 1, width: 200, height: 100, allowResizing: false));
    }

    [Fact]
    public void AspectLockedResizePreservesInitialRatioLikeMaster()
    {
        Size wideRequest = TimerWindow.ApplyAspectLockedResize(
            requestedSize: new Size(500, 100),
            initialAspectRatio: 2.0,
            shiftPressed: true);

        Assert.Equal(new Size(200, 100), wideRequest);

        Size tallRequest = TimerWindow.ApplyAspectLockedResize(
            requestedSize: new Size(100, 100),
            initialAspectRatio: 2.0,
            shiftPressed: true);

        Assert.Equal(new Size(100, 50), tallRequest);
        Assert.Equal(
            new Size(500, 100),
            TimerWindow.ApplyAspectLockedResize(new Size(500, 100), initialAspectRatio: 2.0, shiftPressed: false));
    }

    [Fact]
    public void ManagedCornerResizeAspectLockAnchorsOppositeCornerLikeMaster()
    {
        TimerWindow.ManagedResizeResult resized = TimerWindow.CalculateManagedResize(
            global::Avalonia.Controls.WindowEdge.NorthWest,
            startPosition: new PixelPoint(100, 100),
            startSize: new Size(200, 100),
            pointerDelta: new Vector(-100, -20),
            shiftPressed: true);

        Assert.Equal(new Size(240, 120), resized.Size);
        Assert.Equal(new PixelPoint(60, 80), resized.Position);
    }

    [Fact]
    public void ManagedSideResizeDoesNotAspectLockLikeMaster()
    {
        TimerWindow.ManagedResizeResult resized = TimerWindow.CalculateManagedResize(
            global::Avalonia.Controls.WindowEdge.East,
            startPosition: new PixelPoint(100, 100),
            startSize: new Size(200, 100),
            pointerDelta: new Vector(80, 40),
            shiftPressed: true);

        Assert.Equal(new Size(280, 100), resized.Size);
        Assert.Equal(new PixelPoint(100, 100), resized.Position);
    }

    [Fact]
    public void BorderlessResizeUsesManagedCrossPlatformDragPath()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/TimerWindow.axaml.cs"));

        Assert.Contains("BeginManagedResizeDrag(resizeEdge.Value", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BeginResizeDrag(resizeEdge.Value", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MaintainMinimumLayoutSizeLikeMaster()
    {
        Assert.Equal(
            new Size(50, 50),
            TimerWindow.MaintainMinimumLayoutSize(
                currentSize: new Size(50, 100),
                overallSize: 100,
                minimumCrossSize: 100,
                verticalLayout: true));

        Assert.Equal(
            new Size(50, 50),
            TimerWindow.MaintainMinimumLayoutSize(
                currentSize: new Size(100, 50),
                overallSize: 100,
                minimumCrossSize: 100,
                verticalLayout: false));
    }

    [Fact]
    public void DispatchMouseWheelScrollsSplitsLikeMaster()
    {
        var model = new TimerModel();
        int scrollUpCount = 0;
        int scrollDownCount = 0;
        model.OnScrollUp += (_, _) => scrollUpCount++;
        model.OnScrollDown += (_, _) => scrollDownCount++;

        Assert.True(TimerWindow.DispatchMouseWheel(model, 1));
        Assert.True(TimerWindow.DispatchMouseWheel(model, -1));
        Assert.False(TimerWindow.DispatchMouseWheel(model, 0));

        Assert.Equal(1, scrollUpCount);
        Assert.Equal(1, scrollDownCount);
    }

    [Theory]
    [InlineData("run.lss", ".lss", true)]
    [InlineData("run.LSS", ".lss", true)]
    [InlineData("run.txt", ".lss", false)]
    [InlineData("", ".lss", false)]
    public void ValidateSaveFileExtensionLikeMaster(string path, string extension, bool expected)
    {
        Assert.Equal(expected, TimerWindow.HasSaveExtension(path, extension));
    }

    [Fact]
    public void ExposeMasterLoadAndSaveFailureMessages()
    {
        Assert.Equal(
            "The selected file was not recognized as a splits file.",
            TimerWindow.RunLoadFailureMessage());
        Assert.Equal(
            "The selected file was not recognized as a layout file. (bad xml)",
            TimerWindow.LayoutLoadFailureMessage(new InvalidDataException("bad xml")));
        Assert.Equal("Splits could not be saved!", TimerWindow.SplitsSaveFailureMessage());
        Assert.Equal("Layout could not be saved!", TimerWindow.LayoutSaveFailureMessage());
    }

    [Fact]
    public void BuildOpenFileDialogWithRecentDirectory()
    {
        var dialog = TimerWindow.BuildOpenFileDialog(
            "Open Splits",
            [new global::Avalonia.Controls.FileDialogFilter { Name = "LiveSplit Splits", Extensions = { "lss" } }],
            @"C:\runs\Example.lss");

        Assert.Equal("Open Splits", dialog.Title);
        Assert.Equal(@"C:\runs", dialog.Directory);
    }

    [Fact]
    public void BuildSaveFileDialogWithDefaultExtensionAndSuggestedFileName()
    {
        var dialog = TimerWindow.BuildSaveFileDialog(
            "Save Splits",
            "lss",
            [new global::Avalonia.Controls.FileDialogFilter { Name = "LiveSplit Splits", Extensions = { "lss" } }],
            "Example Game - Any%.lss");

        Assert.Equal("Save Splits", dialog.Title);
        Assert.Equal("lss", dialog.DefaultExtension);
        Assert.Equal("Example Game - Any%.lss", dialog.InitialFileName);
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
    public void CaptureRunningServerStateBeforeStoppingServerForSettingsRestart()
    {
        Assert.Equal(
            ServerStateType.Websocket,
            TimerWindow.CaptureServerStateForSettingsRestart(
                commandServerState: ServerStateType.Websocket,
                settingsServerState: ServerStateType.Off));

        Assert.Equal(
            ServerStateType.TCP,
            TimerWindow.CaptureServerStateForSettingsRestart(
                commandServerState: ServerStateType.Off,
                settingsServerState: ServerStateType.TCP));
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

    [Fact]
    public void DialogModalStateTemporarilyClearsAlwaysOnTop()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/TimerWindow.axaml.cs"));

        Assert.Contains("bool restoreTopmost = Topmost;", source, StringComparison.Ordinal);
        Assert.Contains("Topmost = false;", source, StringComparison.Ordinal);
        Assert.Contains("Topmost = restoreTopmost;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedLayoutEditorChangesReapplyWindowSizeAndSettings()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/TimerWindow.axaml.cs"));

        int methodStart = source.IndexOf("private async Task OpenEditLayout()", StringComparison.Ordinal);
        Assert.True(methodStart >= 0);
        int methodEnd = source.IndexOf("private async Task OpenLayoutSettings()", methodStart, StringComparison.Ordinal);
        Assert.True(methodEnd > methodStart);
        string method = source[methodStart..methodEnd];

        Assert.Contains("ApplyLayoutSize();", method, StringComparison.Ordinal);
        Assert.Contains("ApplyLayoutWindowSettings();", method, StringComparison.Ordinal);
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
