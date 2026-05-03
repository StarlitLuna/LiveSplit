using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;

using global::Avalonia.Controls;
using global::Avalonia.Controls.Notifications;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Markup.Xaml;
using global::Avalonia.Platform.Storage;
using global::Avalonia.Threading;

using LiveSplit.Avalonia.Dialogs;
using LiveSplit.Localization;
using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.Server;
using LiveSplit.UI;
using LiveSplit.UI.Components;

namespace LiveSplit.Avalonia;

/// <summary>
/// Hosts a <see cref="SkiaRenderControl"/> backed by an <see cref="AvaloniaTimerHost"/>, plus
/// window-focused split/reset/skip/undo/pause keys, file/URL open commands, recents submenus,
/// comparison + timing-method switching, drag-and-drop run/layout loading, and the control-server
/// (TCP/WS/Pipe) wire-up.
/// </summary>
public sealed partial class TimerWindow : Window
{
    private const int BorderlessWidthCompensation = 3;
    private const int BorderlessHeightCompensation = 1;

    public AvaloniaTimerHost Host { get; }

    public ICommand SplitCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand SkipCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand PauseCommand { get; }

    public ICommand OpenSplitsCommand { get; }
    public ICommand OpenLayoutCommand { get; }
    public ICommand OpenSplitsFromUrlCommand { get; }
    public ICommand OpenLayoutFromUrlCommand { get; }
    public ICommand CloseSplitsCommand { get; }
    public ICommand SaveSplitsCommand { get; }
    public ICommand SaveSplitsAsCommand { get; }
    public ICommand SaveLayoutCommand { get; }
    public ICommand SaveLayoutAsCommand { get; }
    public ICommand EditSplitsCommand { get; }
    public ICommand EditLayoutCommand { get; }
    public ICommand LayoutSettingsCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand SetSizeCommand { get; }
    public ICommand ShareCommand { get; }
    public ICommand AboutCommand { get; }
    public ICommand CloseCommand { get; }

    private CommandServer _commandServer;
    private WindowNotificationManager _notificationManager;
    private EventHandler<NotificationEventArgs> _notificationHandler;

    public TimerWindow()
        : this(splitsPath: null, layoutPath: null)
    {
    }

    public TimerWindow(string splitsPath, string layoutPath)
    {
        AvaloniaXamlLoader.Load(this);

        // The host posts an invalidate every refresh tick. Calling Window.InvalidateVisual
        // doesn't re-run the SkiaRenderControl's CustomDrawOperation; we have to invalidate
        // the canvas itself. This keeps the timer ticking visibly (otherwise only the very
        // first frame ever paints, and components that only set their VerticalHeight inside
        // DrawGeneral are stuck at their pre-render seed values forever).
        Action invalidate = () =>
        {
            SkiaRenderControl c = this.FindControl<SkiaRenderControl>("Canvas");
            c?.InvalidateVisual();
        };
        Host = new AvaloniaTimerHost(invalidate, splitsPath, layoutPath);
        Host.ResetRequested += async (_, _) => await Reset();

        SplitCommand = new RelayCommand(() => Host.Model.Split());
        ResetCommand = new RelayCommand(async () => await Reset());
        SkipCommand = new RelayCommand(() => Host.Model.SkipSplit());
        UndoCommand = new RelayCommand(() => Host.Model.UndoSplit());
        PauseCommand = new RelayCommand(() => Host.Model.Pause());

        OpenSplitsCommand = new RelayCommand(async () => await OpenSplits());
        OpenLayoutCommand = new RelayCommand(async () => await OpenLayout());
        OpenSplitsFromUrlCommand = new RelayCommand(async () => await OpenSplitsFromUrl());
        OpenLayoutFromUrlCommand = new RelayCommand(async () => await OpenLayoutFromUrl());
        CloseSplitsCommand = new RelayCommand(async () => await CloseSplits());
        SaveSplitsCommand = new RelayCommand(async () => await SaveSplits());
        SaveSplitsAsCommand = new RelayCommand(async () => await SaveSplitsAs());
        SaveLayoutCommand = new RelayCommand(async () => await SaveLayout());
        SaveLayoutAsCommand = new RelayCommand(async () => await SaveLayoutAs());
        EditSplitsCommand = new RelayCommand(async () => await OpenEditSplits());
        EditLayoutCommand = new RelayCommand(async () => await OpenEditLayout());
        LayoutSettingsCommand = new RelayCommand(async () => await OpenLayoutSettings());
        SettingsCommand = new RelayCommand(async () => await OpenSettings());
        SetSizeCommand = new RelayCommand(async () => await OpenSetSize());
        ShareCommand = new RelayCommand(async () => await OpenShare());
        AboutCommand = new RelayCommand(async () => await OpenAbout());
        CloseCommand = new RelayCommand(Close);

        DataContext = this;

        if (this.FindControl<SkiaRenderControl>("Canvas") is SkiaRenderControl canvas)
        {
            canvas.Host = Host;
        }

        Host.LayoutApplied += () =>
        {
            ApplyLayoutSize();
            ApplyLayoutWindowSettings();
        };
        ApplyLayoutSize();
        ApplyLayoutWindowSettings();
        Host.State.OnStart += (_, _) => Dispatcher.UIThread.Post(ApplyLayoutWindowSettings);
        Host.State.OnReset += (_, _) => Dispatcher.UIThread.Post(ApplyLayoutWindowSettings);
        Host.State.OnPause += (_, _) => Dispatcher.UIThread.Post(ApplyLayoutWindowSettings);
        Host.State.OnResume += (_, _) => Dispatcher.UIThread.Post(ApplyLayoutWindowSettings);

        // Re-apply on Opened too — some window managers (Wayland XWayland in particular)
        // don't honor Width/Height set before the window is realized. Setting them again on
        // Opened forces the geometry the user expects from the layout's saved dimensions.
        Opened += (_, _) =>
        {
            ApplyLayoutSize();
            ApplyLayoutWindowSettings();
        };

        SizeChanged += OnSizeChanged;

        WireContextMenu();

        // Tunnel-phase so the handler runs even when child controls swallow the bubble; the
        // right-click ContextMenu wired via XAML still surfaces normally.
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        // Drag-and-drop a .lss / .lsl onto the window to load it.
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        Closing += OnClosing;
        Closed += OnClosed;

        // Toast/notification surface for component status messages (therun.gg uploads, etc.).
        // Components publish through LiveSplit.UI.Notifications without taking an Avalonia
        // dependency; we route into Avalonia's WindowNotificationManager here.
        Opened += (_, _) =>
        {
            _notificationManager = new WindowNotificationManager(this) { Position = NotificationPosition.BottomRight, MaxItems = 3 };
            _notificationHandler = (_, args) => Dispatcher.UIThread.Post(() =>
            {
                NotificationType type = args.Severity switch
                {
                    NotificationSeverity.Success => NotificationType.Success,
                    NotificationSeverity.Error => NotificationType.Error,
                    _ => NotificationType.Information,
                };
                _notificationManager.Show(new Notification("LiveSplit", args.Message, type));
            });
            Notifications.Raised += _notificationHandler;
        };

        StartConfiguredServer();
    }

    private bool _closeConfirmed;

    private async void OnClosing(object sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed)
        {
            return;
        }

        e.Cancel = true;

        CaptureLayoutPosition();
        bool ok = await ConfirmUnsavedChanges();
        if (ok)
        {
            _closeConfirmed = true;
            Close();
        }
    }

    private async Task<bool> ConfirmUnsavedChanges()
    {
        if (Host?.State?.Run is { HasChanged: true })
        {
            var dlg = new MessageDialog(
                "Save Splits?",
                "Your splits have been updated but not yet saved.\nDo you want to save your splits now?",
                MessageDialog.Buttons.YesNoCancel);
            MessageResult r = await RunWithNormalHotkeysSuppressed(() => dlg.ShowDialogResultAsync(this));
            if (r == MessageResult.Cancel)
            {
                return false;
            }

            if (r == MessageResult.Yes && !await SaveSplits(promptPBMessage: false))
            {
                return false;
            }
        }

        if (Host?.State?.Layout is { HasChanged: true })
        {
            var dlg = new MessageDialog(
                "Save Layout?",
                "Your layout has been updated but not yet saved.\nDo you want to save your layout now?",
                MessageDialog.Buttons.YesNoCancel);
            MessageResult r = await RunWithNormalHotkeysSuppressed(() => dlg.ShowDialogResultAsync(this));
            if (r == MessageResult.Cancel)
            {
                return false;
            }

            if (r == MessageResult.Yes && !await SaveLayout())
            {
                return false;
            }
        }

        return true;
    }

    private bool _resetMessageShown;

    private async Task Reset()
    {
        if (_resetMessageShown)
        {
            return;
        }

        try
        {
            _resetMessageShown = true;
            if (Host.State.Settings.WarnOnReset && ResetWarning.ShouldAskToUpdateBestTimes(Host.State))
            {
                var dlg = new MessageDialog(
                    "Update Times?",
                    "You have beaten some of your best times.\nDo you want to update them?",
                    MessageDialog.Buttons.YesNoCancel);
                MessageResult result = await RunWithNormalHotkeysSuppressed(() => dlg.ShowDialogResultAsync(this));
                if (result == MessageResult.Cancel)
                {
                    return;
                }

                Host.Model.Reset(updateSplits: result == MessageResult.Yes);
                return;
            }

            Host.Model.Reset();
        }
        finally
        {
            _resetMessageShown = false;
        }
    }

    private void CaptureLayoutPosition()
        => Host.UpdateLayoutPosition(Position.X, Position.Y);

    private void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        PointerPointProperties props = e.GetCurrentPoint(this).Properties;
        if (props.IsLeftButtonPressed && Host?.State?.LayoutSettings?.AllowMoving != false)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (Host?.DispatchFocusedHotkey(e.Key, e.KeyModifiers) == true)
        {
            e.Handled = true;
        }
    }

    // --- Layout ↔ window dimension propagation ----------------------------------------------

    /// <summary>
    /// Sync window Topmost from the active layout's <c>AlwaysOnTop</c> setting. Master parity:
    /// the setting is per-layout (lives in the .lsl) rather than global, so swapping layouts
    /// must re-apply this.
    /// </summary>
    private void ApplyLayoutWindowSettings()
    {
        LayoutSettings settings = Host?.State?.Layout?.Settings;
        if (settings is null)
        {
            return;
        }

        Topmost = settings.AlwaysOnTop;
        Opacity = Math.Clamp(settings.Opacity, 0.0, 1.0);
        CanResize = settings.AllowResizing;

        if (this.FindControl<SkiaRenderControl>("Canvas") is SkiaRenderControl canvas)
        {
            bool passThrough = ShouldUseMousePassThrough(settings, Host.State.CurrentPhase, IsActive);
            IsHitTestVisible = !passThrough;
            canvas.IsHitTestVisible = !passThrough;
        }
    }

    /// <summary>
    /// Resize and reposition the window to match the active layout's stored dimensions. Mirrors
    /// master's TimerForm.SetLayout — the .lsl persists VerticalWidth/Height (or Horizontal
    /// variants) and X/Y; this method applies them after a load/swap.
    /// </summary>
    private void ApplyLayoutSize()
    {
        UI.ILayout layout = Host?.State?.Layout;
        if (layout is null)
        {
            return;
        }

        bool vertical = layout.Mode == UI.LayoutMode.Vertical;
        int w = vertical ? layout.VerticalWidth : layout.HorizontalWidth;
        int h = vertical ? layout.VerticalHeight : layout.HorizontalHeight;

        if (w != UI.Layout.InvalidSize && h != UI.Layout.InvalidSize && w > 0 && h > 0)
        {
            (int windowWidth, int windowHeight) = GetWindowSizeForLayout(w, h);
            Width = windowWidth;
            Height = windowHeight;
        }

        if (layout.X != 0 || layout.Y != 0)
        {
            var desired = new global::Avalonia.PixelPoint(layout.X, layout.Y);
            var windowSize = new global::Avalonia.Size(Width, Height);
            IEnumerable<global::Avalonia.PixelRect> screens = Screens?.All?.Select(x => x.WorkingArea) ?? [];
            Position = ClampLayoutPosition(desired, windowSize, screens);
        }
    }

    /// <summary>
    /// User-resize → layout. Skips no-op updates (so the initial ApplyLayoutSize-driven
    /// SizeChanged echo doesn't dirty the layout) and flips HasChanged so Save Layout will
    /// persist the new dimensions.
    /// </summary>
    private void OnSizeChanged(object sender, global::Avalonia.Controls.SizeChangedEventArgs e)
    {
        UI.ILayout layout = Host?.State?.Layout;
        if (layout is null)
        {
            return;
        }

        (int w, int h) = GetLayoutSizeForWindow(e.NewSize.Width, e.NewSize.Height);
        bool vertical = layout.Mode == UI.LayoutMode.Vertical;
        int currentW = vertical ? layout.VerticalWidth : layout.HorizontalWidth;
        int currentH = vertical ? layout.VerticalHeight : layout.HorizontalHeight;
        if (currentW == w && currentH == h)
        {
            return;
        }

        if (vertical)
        {
            layout.VerticalWidth = w;
            layout.VerticalHeight = h;
        }
        else
        {
            layout.HorizontalWidth = w;
            layout.HorizontalHeight = h;
        }

        layout.HasChanged = true;
    }

    public static (int Width, int Height) GetWindowSizeForLayout(int layoutWidth, int layoutHeight)
        => (
            Math.Max(1, layoutWidth + BorderlessWidthCompensation),
            Math.Max(1, layoutHeight + BorderlessHeightCompensation));

    public static (int Width, int Height) GetLayoutSizeForWindow(double windowWidth, double windowHeight)
        => (
            Math.Max(1, (int)Math.Round(windowWidth) - BorderlessWidthCompensation),
            Math.Max(1, (int)Math.Round(windowHeight) - BorderlessHeightCompensation));

    internal static bool ShouldUseMousePassThrough(LayoutSettings settings, TimerPhase phase, bool isForeground)
        => settings?.MousePassThroughWhileRunning == true
            && phase == TimerPhase.Running
            && !isForeground;

    internal static global::Avalonia.PixelPoint ClampLayoutPosition(
        global::Avalonia.PixelPoint position,
        global::Avalonia.Size windowSize,
        IEnumerable<global::Avalonia.PixelRect> workingAreas)
    {
        global::Avalonia.PixelRect[] areas = workingAreas?.ToArray() ?? [];
        if (areas.Length == 0)
        {
            return position;
        }

        int width = Math.Max(1, (int)Math.Ceiling(windowSize.Width));
        int height = Math.Max(1, (int)Math.Ceiling(windowSize.Height));
        global::Avalonia.PixelRect area = areas.FirstOrDefault(x =>
            position.X < x.X + x.Width
            && position.X + width > x.X
            && position.Y < x.Y + x.Height
            && position.Y + height > x.Y);
        if (area.Width <= 0 || area.Height <= 0)
        {
            area = areas[0];
        }

        int maxX = Math.Max(area.X, area.X + area.Width - width);
        int maxY = Math.Max(area.Y, area.Y + area.Height - height);
        return new global::Avalonia.PixelPoint(
            Math.Clamp(position.X, area.X, maxX),
            Math.Clamp(position.Y, area.Y, maxY));
    }

#pragma warning disable CS0618 // Avalonia 11 marks FileDialogFilter / OpenFileDialog obsolete in favor of StorageProvider; migration is tracked separately.
    private async Task OpenSplits()
    {
        if (!await ConfirmUnsavedChanges())
        {
            return;
        }

        string path = await PickFile("Open Splits", new[]
        {
            new FileDialogFilter { Name = "LiveSplit Splits", Extensions = { "lss" } },
            new FileDialogFilter { Name = "All Files", Extensions = { "*" } },
        });
        if (!string.IsNullOrEmpty(path))
        {
            Host.LoadRun(path);
        }
    }

    private async Task OpenLayout()
    {
        if (!await ConfirmUnsavedChanges())
        {
            return;
        }

        string path = await PickFile("Open Layout", new[]
        {
            new FileDialogFilter { Name = "LiveSplit Layout", Extensions = { "lsl" } },
            new FileDialogFilter { Name = "All Files", Extensions = { "*" } },
        });
        if (!string.IsNullOrEmpty(path))
        {
            Host.LoadLayout(path);
        }
    }

    private async Task OpenSplitsFromUrl()
    {
        if (!await ConfirmUnsavedChanges())
        {
            return;
        }

        var dlg = new TextInputDialog("Open Splits From URL", "Enter the URL of a .lss file:");
        string url = await RunWithNormalHotkeysSuppressed(() => dlg.ShowDialogAsync(this));
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        string downloaded = await DownloadToTempFile(url, "splits.lss");
        if (!string.IsNullOrEmpty(downloaded))
        {
            Host.LoadRun(downloaded);
        }
    }

    private async Task OpenLayoutFromUrl()
    {
        if (!await ConfirmUnsavedChanges())
        {
            return;
        }

        var dlg = new TextInputDialog("Open Layout From URL", "Enter the URL of a .lsl file:");
        string url = await RunWithNormalHotkeysSuppressed(() => dlg.ShowDialogAsync(this));
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        string downloaded = await DownloadToTempFile(url, "layout.lsl");
        if (!string.IsNullOrEmpty(downloaded))
        {
            Host.LoadLayout(downloaded);
        }
    }

    private async Task<string> PickFile(string title, IEnumerable<FileDialogFilter> filters)
    {
        var picker = new OpenFileDialog
        {
            Title = title,
            AllowMultiple = false,
            Filters = filters.ToList(),
        };

        string[] paths = await RunWithNormalHotkeysSuppressed(() => picker.ShowAsync(this));
        return paths is { Length: > 0 } ? paths[0] : null;
    }

    private async Task<string> PickSaveFile(string title, string defaultExtension, IEnumerable<FileDialogFilter> filters)
    {
        var picker = new SaveFileDialog
        {
            Title = title,
            DefaultExtension = defaultExtension,
            Filters = filters.ToList(),
        };

        return await RunWithNormalHotkeysSuppressed(() => picker.ShowAsync(this));
    }

    private async Task<bool> SaveSplits(bool promptPBMessage = true)
    {
        if (!string.IsNullOrEmpty(Host.State.Run?.FilePath))
        {
            if (!await ConfirmSaveAsPersonalBest(promptPBMessage))
            {
                return false;
            }

            return Host.SaveRun();
        }

        return await SaveSplitsAs(promptPBMessage);
    }

    private async Task<bool> SaveSplitsAs(bool promptPBMessage = true)
    {
        string path = await PickSaveFile("Save Splits", "lss", new[]
        {
            new FileDialogFilter { Name = "LiveSplit Splits", Extensions = { "lss" } },
            new FileDialogFilter { Name = "All Files", Extensions = { "*" } },
        });

        if (string.IsNullOrEmpty(path) || !await ConfirmSaveAsPersonalBest(promptPBMessage))
        {
            return false;
        }

        return Host.SaveRunAs(path);
    }

    private async Task<bool> ConfirmSaveAsPersonalBest(bool promptPBMessage)
    {
        if (!promptPBMessage || !ShouldPromptSaveAsPersonalBest())
        {
            return true;
        }

        var dlg = new MessageDialog(
            "Save as Personal Best?",
            "This run did not beat your current splits. Would you like to save this run as a Personal Best?",
            MessageDialog.Buttons.YesNoCancel);
        MessageResult result = await RunWithNormalHotkeysSuppressed(() => dlg.ShowDialogResultAsync(this));
        if (result == MessageResult.Cancel)
        {
            return false;
        }

        if (result == MessageResult.Yes)
        {
            Host.Model.ResetAndSetAttemptAsPB();
        }

        return true;
    }

    private bool ShouldPromptSaveAsPersonalBest()
    {
        IRun run = Host.State.Run;
        if (run is null || run.Count == 0)
        {
            return false;
        }

        TimingMethod method = Host.State.CurrentTimingMethod;
        return Host.State.CurrentPhase is TimerPhase.Running or TimerPhase.Paused
            || (Host.State.CurrentPhase == TimerPhase.Ended
                && run.Last().PersonalBestSplitTime[method] != null
                && run.Last().SplitTime[method] >= run.Last().PersonalBestSplitTime[method]);
    }

    private async Task<bool> SaveLayout()
    {
        CaptureLayoutPosition();
        if (!string.IsNullOrEmpty(Host.State.Layout?.FilePath))
        {
            return Host.SaveLayout();
        }

        return await SaveLayoutAs();
    }

    private async Task<bool> SaveLayoutAs()
    {
        CaptureLayoutPosition();
        string path = await PickSaveFile("Save Layout", "lsl", new[]
        {
            new FileDialogFilter { Name = "LiveSplit Layout", Extensions = { "lsl" } },
            new FileDialogFilter { Name = "All Files", Extensions = { "*" } },
        });

        return !string.IsNullOrEmpty(path) && Host.SaveLayoutAs(path, Position.X, Position.Y);
    }

    private async Task CloseSplits()
    {
        if (await ConfirmUnsavedChanges())
        {
            Host.CloseSplits();
        }
    }

#pragma warning restore CS0618

    private static readonly HttpClient HttpClient = new();

    private async Task<string> DownloadToTempFile(string url, string fileName)
    {
        try
        {
            string tempPath = Path.Combine(Path.GetTempPath(), fileName);
            byte[] data = await HttpClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(tempPath, data);
            return tempPath;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            await RunWithNormalHotkeysSuppressed(() => new MessageDialog("Download Failed", ex.Message).ShowDialogAsync(this));
            return null;
        }
    }

    private async Task OpenEditSplits()
    {
        var dlg = new RunEditorDialog(Host.State);
        if (await RunWithNormalHotkeysSuppressed(() => dlg.ShowDialogAsync(this)))
        {
            InvalidateVisual();
        }
    }

    private async Task OpenEditLayout()
    {
        var dlg = new LayoutEditorDialog(Host.State.Layout, Host.State);
        if (await RunWithNormalHotkeysSuppressed(() => dlg.ShowDialogAsync(this)))
        {
            InvalidateVisual();
        }
    }

    private async Task OpenLayoutSettings()
    {
        var dlg = new LayoutSettingsDialog(Host.State.LayoutSettings);
        if (await RunWithNormalHotkeysSuppressed(() => dlg.ShowDialogAsync(this)))
        {
            Host.State.Layout.HasChanged = true;
            ApplyLayoutWindowSettings();
            InvalidateVisual();
        }
    }

    private async Task OpenSettings()
    {
        ISettings previous = (ISettings)Host.State.Settings.Clone();
        ISettings edited = (ISettings)Host.State.Settings.Clone();
        var dlg = new SettingsDialog(edited, Host.State.CurrentHotkeyProfile);
        if (await RunWithNormalHotkeysSuppressed(() => dlg.ShowDialogAsync(this)))
        {
            Host.ApplySettings(edited, dlg.SelectedHotkeyProfile);
            ApplyServerSettings(previous, edited);
            InvalidateVisual();
        }
    }

    private async Task OpenSetSize()
    {
        var dlg = new SetSizeForm(this);
        bool ok = await RunWithNormalHotkeysSuppressed(() => dlg.ShowDialogAsync(this));
        if (!ok)
        {
            return;
        }

        UI.ILayout layout = Host?.State?.Layout;
        if (layout is null)
        {
            return;
        }

        (int w, int h) = GetLayoutSizeForWindow(Width, Height);
        if (layout.Mode == UI.LayoutMode.Vertical)
        {
            layout.VerticalWidth = w;
            layout.VerticalHeight = h;
        }
        else
        {
            layout.HorizontalWidth = w;
            layout.HorizontalHeight = h;
        }

        layout.HasChanged = true;
    }

    private async Task OpenShare()
    {
        var dlg = new ShareRunDialog((LiveSplitState)Host.State.Clone(), Host.State.Settings, RenderToPng);
        await RunWithNormalHotkeysSuppressed(() => dlg.ShowDialogAsync(this));
    }

    private async Task OpenAbout()
    {
        var dlg = new AboutBox();
        await RunWithNormalHotkeysSuppressed(() => dlg.ShowDialog(this));
    }

    private byte[] RenderToPng()
    {
        return this.FindControl<SkiaRenderControl>("Canvas")?.SnapshotPng();
    }

    // --- Context menu -----------------------------------------------------------------------

    private void WireContextMenu()
    {
        if (ContextMenu is not null)
        {
            ContextMenu.Opening += OnContextMenuOpening;
        }
    }

    private TimerContextMenuContext CreateContextMenuContext()
    {
        return new TimerContextMenuContext
        {
            State = Host.State,
            RecentSplits = Host.State.Settings.RecentSplits,
            RecentLayouts = Host.State.Settings.RecentLayouts,
            RaceProviderItems = BuildRaceProviderItems(),
            LayoutComponents = ContextMenuComponents(),
            EditSplits = OpenEditSplits,
            OpenSplits = OpenSplits,
            OpenSplitsFromUrl = OpenSplitsFromUrl,
            EditSplitsHistory = EditSplitsHistory,
            SaveSplits = () => SaveSplits(),
            SaveSplitsAs = () => SaveSplitsAs(),
            CloseSplits = CloseSplits,
            OpenRecentSplits = OpenRecentSplits,
            EditLayout = OpenEditLayout,
            OpenLayout = OpenLayout,
            OpenLayoutFromUrl = OpenLayoutFromUrl,
            LoadDefaultLayout = LoadDefaultLayout,
            EditLayoutHistory = EditLayoutHistory,
            SaveLayout = SaveLayout,
            SaveLayoutAs = SaveLayoutAs,
            OpenRecentLayout = OpenRecentLayout,
            Settings = OpenSettings,
            Share = OpenShare,
            About = OpenAbout,
            Exit = Close,
            StartOrSplit = StartOrSplit,
            Reset = Reset,
            UndoSplit = () => Host.Model.UndoSplit(),
            SkipSplit = () => Host.Model.SkipSplit(),
            Pause = () => Host.Model.Pause(),
            UndoAllPauses = () => Host.Model.UndoAllPauses(),
            ToggleGlobalHotkeys = ToggleGlobalHotkeys,
            GetServerState = CurrentServerState,
            ToggleTcpServer = ToggleTcpServer,
            ToggleWebSocketServer = ToggleWebSocketServer,
            SwitchComparison = SwitchComparison,
            SwitchTimingMethod = SwitchTimingMethod,
            ApplyLanguage = ApplyLanguage,
        };
    }

    private IEnumerable<LiveSplit.UI.Components.IComponent> ContextMenuComponents()
    {
        IEnumerable<LiveSplit.UI.Components.IComponent> components =
            Host.State.Layout.LayoutComponents.Select(x => x.Component);
        if (Host.State.Run.IsAutoSplitterActive())
        {
            components = components.Concat([Host.State.Run.AutoSplitter.Component]);
        }

        return components;
    }

    private IReadOnlyList<MenuItem> BuildRaceProviderItems()
    {
        var items = new List<MenuItem>();
        foreach (KeyValuePair<string, IRaceProviderFactory> entry in ComponentManager.RaceProviderFactories)
        {
            RaceProviderSettings settings = Host.State.Settings.RaceProvider.FirstOrDefault(x => x.Name == entry.Key);
            RaceProviderAPI provider = entry.Value.Create(Host.Model, settings ?? entry.Value.CreateSettings());
            if (Host.State.Settings.RaceProvider.Any(x => x.DisplayName == provider.ProviderName && !x.Enabled))
            {
                continue;
            }

            var providerItem = new MenuItem
            {
                Header = $"{provider.ProviderName} Races",
                Tag = provider
            };
            providerItem.Items.Add(new MenuItem { Header = "Refreshing...", IsEnabled = false });
            providerItem.SubmenuOpened += (_, _) =>
            {
                providerItem.Items.Clear();
                providerItem.Items.Add(new MenuItem { Header = "Refreshing...", IsEnabled = false });
                provider.RacesRefreshedCallback = refreshedProvider =>
                    Dispatcher.UIThread.Post(() => PopulateRaceProviderMenu(providerItem, refreshedProvider));
                provider.RefreshRacesListAsync();
            };
            items.Add(providerItem);
        }

        return items;
    }

    private async Task OpenRecentSplits(string path)
    {
        if (await ConfirmUnsavedChanges())
        {
            Host.LoadRun(path);
        }
    }

    private async Task OpenRecentLayout(string path)
    {
        if (await ConfirmUnsavedChanges())
        {
            Host.LoadLayout(path);
        }
    }

    private async Task EditSplitsHistory()
    {
        var dlg = new EditHistoryDialog(Host.State.Settings.RecentSplits.Select(x => x.Path));
        if (await RunWithNormalHotkeysSuppressed(() => dlg.ShowDialogAsync(this)))
        {
            Host.SetRecentSplitsHistory(dlg.History);
        }
    }

    private async Task EditLayoutHistory()
    {
        var dlg = new EditHistoryDialog(Host.State.Settings.RecentLayouts);
        if (await RunWithNormalHotkeysSuppressed(() => dlg.ShowDialogAsync(this)))
        {
            Host.SetRecentLayoutsHistory(dlg.History);
        }
    }

    private async Task LoadDefaultLayout()
    {
        if (await ConfirmUnsavedChanges())
        {
            Host.LoadDefaultLayout(Position.X, Position.Y);
        }
    }

    private void StartOrSplit()
    {
        switch (Host.State.CurrentPhase)
        {
            case TimerPhase.Running:
                Host.Model.Split();
                break;
            case TimerPhase.Paused:
                Host.Model.Pause();
                break;
            case TimerPhase.NotRunning:
                Host.Model.Start();
                break;
            case TimerPhase.Ended:
                Host.Model.Reset();
                break;
        }
    }

    private void ToggleGlobalHotkeys()
    {
        if (Host.State.Settings.HotkeyProfiles.TryGetValue(Host.State.CurrentHotkeyProfile, out HotkeyProfile profile))
        {
            profile.GlobalHotkeysEnabled = !profile.GlobalHotkeysEnabled;
        }
    }

    private ServerStateType CurrentServerState()
        => _commandServer?.ServerState ?? Host.State.Settings.ServerState;

    private void ToggleTcpServer()
    {
        if (CurrentServerState() == ServerStateType.TCP)
        {
            StopServer(updateStartupSetting: false);
        }
        else if (CurrentServerState() == ServerStateType.Off)
        {
            StartServer(ServerStartupType.TCP, updateStartupSetting: false);
        }
    }

    private void ToggleWebSocketServer()
    {
        if (CurrentServerState() == ServerStateType.Websocket)
        {
            StopServer(updateStartupSetting: false);
        }
        else if (CurrentServerState() == ServerStateType.Off)
        {
            StartServer(ServerStartupType.Websocket, updateStartupSetting: false);
        }
    }

    private void SwitchComparison(string name)
    {
        if (!Host.State.Run.Comparisons.Contains(name))
        {
            name = Run.PersonalBestComparisonName;
        }

        Host.State.CurrentComparison = name;
        InvalidateVisual();
    }

    private void SwitchTimingMethod(TimingMethod method)
    {
        Host.State.CurrentTimingMethod = method;
        InvalidateVisual();
    }

    private void OnContextMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (sender is not ContextMenu menu)
        {
            return;
        }

        menu.Items.Clear();
        foreach (object item in TimerContextMenuBuilder.BuildRootItems(CreateContextMenuContext()))
        {
            menu.Items.Add(item);
        }
    }

    private void PopulateRaceProviderMenu(MenuItem providerItem, RaceProviderAPI provider)
    {
        providerItem.Items.Clear();
        IReadOnlyList<IRaceInfo> races = provider.GetRaces()?.ToList() ?? [];
        int addedCount = 0;

        foreach (IRaceInfo race in races.Where(x => x.State == 1))
        {
            var item = new MenuItem
            {
                Header = $"{RaceMenuFormatter.FormatOpenRaceTitle(race)} - {RaceMenuFormatter.FormatOpenRaceAction(provider.JoinCapability)}",
                Tag = race.Id
            };
            item.Click += async (_, _) => await JoinRaceOrOpenViewer(provider, race);
            providerItem.Items.Add(item);
            addedCount++;
        }

        bool addedSeparator = false;
        foreach (IRaceInfo race in races.Where(x => x.State == 3))
        {
            if (addedCount > 0 && !addedSeparator)
            {
                providerItem.Items.Add(new Separator());
                addedSeparator = true;
            }

            var item = new MenuItem
            {
                Header = RaceMenuFormatter.FormatInProgressRaceTitle(race, DateTime.UtcNow),
                Tag = race.Id
            };
            item.Click += async (_, _) =>
            {
                if (!race.IsParticipant(provider.Username))
                {
                    await JoinRaceOrOpenViewer(provider, race);
                }
                else
                {
                    await JoinRaceOrOpenViewer(provider, race);
                }
            };
            providerItem.Items.Add(item);
            addedCount++;
        }

        if (addedCount > 0)
        {
            providerItem.Items.Add(new Separator());
        }

        var newRace = new MenuItem { Header = "New Race..." };
        newRace.Click += async (_, _) => await CreateRace(provider);
        providerItem.Items.Add(newRace);
    }

    private async Task JoinRaceOrOpenViewer(RaceProviderAPI provider, IRaceInfo race)
    {
        try
        {
            if (provider.JoinCapability == RaceJoinCapability.JoinRace && provider.JoinRace != null)
            {
                provider.JoinRace(Host.Model, race.Id);
            }
            else
            {
                OpenRaceViewer(race);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            await RunWithNormalHotkeysSuppressed(() => new MessageDialog("Enter Race", ex.Message).ShowDialogAsync(this));
        }
    }

    private void OpenRaceViewer(IRaceInfo race)
    {
        try
        {
            Host.State.Settings.RaceViewer?.ShowRace(race);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private async Task CreateRace(RaceProviderAPI provider)
    {
        try
        {
            if (provider.CreateRace != null)
            {
                provider.CreateRace(Host.Model);
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            await RunWithNormalHotkeysSuppressed(() => new MessageDialog("New Race", ex.Message).ShowDialogAsync(this));
            return;
        }

        await RunWithNormalHotkeysSuppressed(() => new MessageDialog(
            "New Race",
            $"{provider.ProviderName} does not expose race creation.").ShowDialogAsync(this));
    }

    private async Task ApplyLanguage(string code)
    {
        Host.State.Settings.UILanguage = LanguageResolver.NormalizeSettingValue(code);
        LanguageResolver.SetCurrentLanguageSetting(Host.State.Settings.UILanguage);
        Host.SaveSettings();
        AppLanguage language = LanguageResolver.ResolveCurrentCultureLanguage();
        var dlg = new MessageDialog(
            UiLocalizer.TranslateKey(LocalizationKeys.LanguageMenu, "Language", language),
            UiLocalizer.TranslateKey(
                LocalizationKeys.LanguageRestartRequired,
                "Language change will take effect after restarting LiveSplit.",
                language));
        await RunWithNormalHotkeysSuppressed(() => dlg.ShowDialogAsync(this));
    }

    // --- Server lifecycle -------------------------------------------------------------------

    private void StartConfiguredServer()
    {
        try
        {
            StartNamedPipeServer();
            switch (Host.State.Settings.ServerStartup)
            {
                case ServerStartupType.TCP:
                    StartServer(ServerStartupType.TCP, updateStartupSetting: false);
                    break;
                case ServerStartupType.Websocket:
                    StartServer(ServerStartupType.Websocket, updateStartupSetting: false);
                    break;
                case ServerStartupType.PreviousState:
                    if (Host.State.Settings.ServerState == ServerStateType.TCP)
                    {
                        StartServer(ServerStartupType.TCP, updateStartupSetting: false);
                    }
                    else if (Host.State.Settings.ServerState == ServerStateType.Websocket)
                    {
                        StartServer(ServerStartupType.Websocket, updateStartupSetting: false);
                    }
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    private void ApplyServerSettings(ISettings previous, ISettings current)
    {
        if (previous.ServerPort == current.ServerPort
            && previous.ServerStartup == current.ServerStartup
            && previous.ServerState == current.ServerState)
        {
            return;
        }

        try
        {
            _commandServer?.StopTcp();
            _commandServer?.StopWs();
            ServerStateType restoreState = ResolveServerStateForSettingsRestart(
                current.ServerStartup,
                _commandServer?.ServerState ?? ServerStateType.Off,
                current.ServerState);
            Host.State.Settings.ServerState = restoreState;
            StartConfiguredServer();
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    private void StartServer(ServerStartupType type, bool updateStartupSetting = true)
    {
        try
        {
            _commandServer ??= new CommandServer(Host.State);
            switch (type)
            {
                case ServerStartupType.TCP:
                    _commandServer.StartTcp();
                    break;
                case ServerStartupType.Websocket:
                    _commandServer.StartWs();
                    break;
            }

            if (updateStartupSetting)
            {
                Host.State.Settings.ServerStartup = type;
            }

            Host.State.Settings.ServerState = _commandServer.ServerState;
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    private void StartNamedPipeServer()
    {
        try
        {
            _commandServer ??= new CommandServer(Host.State);
            _commandServer.StartNamedPipe();
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    private void StopServer(bool updateStartupSetting = true)
    {
        try
        {
            if (_commandServer?.ServerState == ServerStateType.TCP)
            {
                _commandServer.StopTcp();
            }
            else if (_commandServer?.ServerState == ServerStateType.Websocket)
            {
                _commandServer.StopWs();
            }

            if (updateStartupSetting)
            {
                Host.State.Settings.ServerStartup = ServerStartupType.Off;
            }

            Host.State.Settings.ServerState = _commandServer?.ServerState ?? ServerStateType.Off;
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    internal static ServerStateType ResolveServerStateForSettingsRestart(
        ServerStartupType startup,
        ServerStateType runningServerState,
        ServerStateType savedServerState)
        => startup == ServerStartupType.PreviousState && runningServerState != ServerStateType.Off
            ? runningServerState
            : savedServerState;

    private int _normalHotkeySuppressionDepth;

    private async Task<T> RunWithNormalHotkeysSuppressed<T>(Func<Task<T>> action)
    {
        _normalHotkeySuppressionDepth++;
        Host.SetNormalHotkeysSuppressed(true);
        try
        {
            return await action();
        }
        finally
        {
            _normalHotkeySuppressionDepth--;
            if (_normalHotkeySuppressionDepth <= 0)
            {
                _normalHotkeySuppressionDepth = 0;
                Host.SetNormalHotkeysSuppressed(false);
            }
        }
    }

    private async Task RunWithNormalHotkeysSuppressed(Func<Task> action)
    {
        await RunWithNormalHotkeysSuppressed(async () =>
        {
            await action();
            return true;
        });
    }

    // --- Drag and drop ----------------------------------------------------------------------

    private static readonly string[] DraggableExtensions = [".lss", ".lsl"];

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.DragEffects = ContainsDraggableFile(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!await ConfirmUnsavedChanges())
        {
            e.Handled = true;
            return;
        }

        IEnumerable<IStorageItem> files = e.Data.GetFiles() ?? Array.Empty<IStorageItem>();
        foreach (IStorageItem item in files)
        {
            string path = item.TryGetLocalPath();
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".lss")
            {
                Host.LoadRun(path);
            }
            else if (ext == ".lsl")
            {
                Host.LoadLayout(path);
            }
        }

        e.Handled = true;
    }

    private static bool ContainsDraggableFile(DragEventArgs e)
    {
        IEnumerable<IStorageItem> files = e.Data.GetFiles();
        if (files == null)
        {
            return false;
        }

        foreach (IStorageItem item in files)
        {
            string path = item.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path)
                && DraggableExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
            {
                return true;
            }
        }

        return false;
    }

    private void OnClosed(object sender, EventArgs e)
    {
        if (_notificationHandler != null)
        {
            Notifications.Raised -= _notificationHandler;
            _notificationHandler = null;
        }

        try
        {
            _commandServer?.StopAll();
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        Host?.Dispose();
    }

    private sealed class RelayCommand : ICommand
    {
        private readonly Action _action;
        private readonly Func<Task> _asyncAction;

        public RelayCommand(Action action) => _action = action;
        public RelayCommand(Func<Task> action) => _asyncAction = action;

        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter)
        {
            if (_asyncAction is not null)
            {
                _ = Dispatcher.UIThread.InvokeAsync(async () => await _asyncAction());
            }
            else
            {
                _action?.Invoke();
            }
        }

        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
}
