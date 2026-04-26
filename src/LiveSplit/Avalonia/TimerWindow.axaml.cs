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
using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.Server;
using LiveSplit.UI;

namespace LiveSplit.Avalonia;

/// <summary>
/// Hosts a <see cref="SkiaRenderControl"/> backed by an <see cref="AvaloniaTimerHost"/>, plus
/// window-focused split/reset/skip/undo/pause keys, file/URL open commands, recents submenus,
/// comparison + timing-method switching, drag-and-drop run/layout loading, and the control-server
/// (TCP/WS/Pipe) wire-up.
/// </summary>
public sealed partial class TimerWindow : Window
{
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

        SplitCommand = new RelayCommand(() => Host.Model.Split());
        ResetCommand = new RelayCommand(() => Host.Model.Reset());
        SkipCommand = new RelayCommand(() => Host.Model.SkipSplit());
        UndoCommand = new RelayCommand(() => Host.Model.UndoSplit());
        PauseCommand = new RelayCommand(() => Host.Model.Pause());

        OpenSplitsCommand = new RelayCommand(async () => await OpenSplits());
        OpenLayoutCommand = new RelayCommand(async () => await OpenLayout());
        OpenSplitsFromUrlCommand = new RelayCommand(async () => await OpenSplitsFromUrl());
        OpenLayoutFromUrlCommand = new RelayCommand(async () => await OpenLayoutFromUrl());
        CloseSplitsCommand = new RelayCommand(() => Host.CloseSplits());
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
            ApplyAlwaysOnTop();
        };
        ApplyLayoutSize();
        ApplyAlwaysOnTop();

        // Re-apply on Opened too — some window managers (Wayland XWayland in particular)
        // don't honor Width/Height set before the window is realized. Setting them again on
        // Opened forces the geometry the user expects from the layout's saved dimensions.
        Opened += (_, _) =>
        {
            ApplyLayoutSize();
            ApplyAlwaysOnTop();
        };

        SizeChanged += OnSizeChanged;

        WireDynamicSubmenus();
        WireComponentContextMenu();

        // Tunnel-phase so the handler runs even when child controls swallow the bubble; the
        // right-click ContextMenu wired via XAML still surfaces normally.
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);

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

        bool ok = await ConfirmUnsavedChanges();
        if (ok)
        {
            _closeConfirmed = true;
            Close();
        }
    }

    private async Task<bool> ConfirmUnsavedChanges()
    {
        if (Host?.State?.Run is { HasChanged: true } run && !string.IsNullOrEmpty(run.FilePath))
        {
            var dlg = new MessageDialog(
                "Save Splits?",
                "Your splits have been updated but not yet saved.\nDo you want to save your splits now?",
                MessageDialog.Buttons.YesNoCancel);
            MessageResult r = await dlg.ShowDialogResultAsync(this);
            if (r == MessageResult.Cancel)
            {
                return false;
            }

            if (r == MessageResult.Yes && !Host.SaveRun())
            {
                return false;
            }
        }

        if (Host?.State?.Layout is { HasChanged: true } layout && !string.IsNullOrEmpty(layout.FilePath))
        {
            var dlg = new MessageDialog(
                "Save Layout?",
                "Your layout has been updated but not yet saved.\nDo you want to save your layout now?",
                MessageDialog.Buttons.YesNoCancel);
            MessageResult r = await dlg.ShowDialogResultAsync(this);
            if (r == MessageResult.Cancel)
            {
                return false;
            }

            if (r == MessageResult.Yes && !Host.SaveLayout())
            {
                return false;
            }
        }

        return true;
    }

    private void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        PointerPointProperties props = e.GetCurrentPoint(this).Properties;
        if (props.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    // --- Layout ↔ window dimension propagation ----------------------------------------------

    /// <summary>
    /// Sync window Topmost from the active layout's <c>AlwaysOnTop</c> setting. Master parity:
    /// the setting is per-layout (lives in the .lsl) rather than global, so swapping layouts
    /// must re-apply this.
    /// </summary>
    private void ApplyAlwaysOnTop()
    {
        Topmost = Host?.State?.Layout?.Settings?.AlwaysOnTop == true;
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
            Width = w;
            Height = h;
        }

        if (layout.X != 0 || layout.Y != 0)
        {
            Position = new global::Avalonia.PixelPoint(layout.X, layout.Y);
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

        int w = (int)Math.Round(e.NewSize.Width);
        int h = (int)Math.Round(e.NewSize.Height);
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

#pragma warning disable CS0618 // Avalonia 11 marks FileDialogFilter / OpenFileDialog obsolete in favor of StorageProvider; migration is tracked separately.
    private async Task OpenSplits()
    {
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
        var dlg = new TextInputDialog("Open Splits From URL", "Enter the URL of a .lss file:");
        string url = await dlg.ShowDialogAsync(this);
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
        var dlg = new TextInputDialog("Open Layout From URL", "Enter the URL of a .lsl file:");
        string url = await dlg.ShowDialogAsync(this);
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

        string[] paths = await picker.ShowAsync(this);
        return paths is { Length: > 0 } ? paths[0] : null;
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
            await new MessageDialog("Download Failed", ex.Message).ShowDialogAsync(this);
            return null;
        }
    }

    private async Task OpenEditSplits()
    {
        var dlg = new RunEditorDialog(Host.State);
        if (await dlg.ShowDialogAsync(this))
        {
            InvalidateVisual();
        }
    }

    private async Task OpenEditLayout()
    {
        var dlg = new LayoutEditorDialog(Host.State.Layout, Host.State);
        if (await dlg.ShowDialogAsync(this))
        {
            InvalidateVisual();
        }
    }

    private async Task OpenLayoutSettings()
    {
        var dlg = new LayoutSettingsDialog(Host.State.LayoutSettings);
        await dlg.ShowDialogAsync(this);


        ApplyAlwaysOnTop();
        InvalidateVisual();
    }

    private async Task OpenSettings()
    {
        var dlg = new SettingsDialog(Host.State.Settings);
        await dlg.ShowDialogAsync(this);
        InvalidateVisual();
    }

    private async Task OpenSetSize()
    {
        var dlg = new SetSizeForm(this);
        bool ok = await dlg.ShowDialogAsync(this);
        if (!ok)
        {
            return;
        }

        UI.ILayout layout = Host?.State?.Layout;
        if (layout is null)
        {
            return;
        }

        int w = (int)Math.Round(Width);
        int h = (int)Math.Round(Height);
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
        var dlg = new ShareRunDialog(Host.State, Host.State.Settings, RenderToPng);
        await dlg.ShowDialogAsync(this);
    }

    private async Task OpenAbout()
    {
        var dlg = new AboutBox();
        await dlg.ShowDialog(this);
    }

    private byte[] RenderToPng()
    {
        return this.FindControl<SkiaRenderControl>("Canvas")?.SnapshotPng();
    }

    // --- Component-specific right-click items -----------------------------------------------

    // Set when the right-click happens, read when the ContextMenu opens. Captures the
    // component the cursor was over so we can build its ContextMenuControls into the menu
    // before the window-level entries.
    private LiveSplit.UI.Components.IComponent _contextMenuComponent;

    private void WireComponentContextMenu()
    {
        if (ContextMenu is null)
        {
            return;
        }

        // ContextRequestedEvent fires before ContextMenu.Opening, so we can capture the
        // pointer position then.
        AddHandler(ContextRequestedEvent, OnContextRequested, RoutingStrategies.Tunnel);
        ContextMenu.Opening += OnContextMenuOpening;
    }

    private void OnContextRequested(object sender, ContextRequestedEventArgs e)
    {
        _contextMenuComponent = null;
        if (Host?.State?.Layout is null)
        {
            return;
        }

        if (this.FindControl<SkiaRenderControl>("Canvas") is not SkiaRenderControl canvas)
        {
            return;
        }

        // ContextRequestedEventArgs.TryGetPosition gives screen-relative coords; ask in the
        // canvas's coord space to hit-test against the layout.
        if (!e.TryGetPosition(canvas, out global::Avalonia.Point pos))
        {
            return;
        }

        _contextMenuComponent = HitTestComponent(canvas, pos);
    }

    private LiveSplit.UI.Components.IComponent HitTestComponent(SkiaRenderControl canvas, global::Avalonia.Point pos)
    {
        Model.LiveSplitState state = Host.State;
        UI.LayoutMode mode = state.Layout.Mode;
        float overall = Math.Max(0.001f, Host.Renderer.OverallSize);

        float canvasW = (float)canvas.Bounds.Width;
        float canvasH = (float)canvas.Bounds.Height;
        float scale = mode == UI.LayoutMode.Vertical ? canvasH / overall : canvasW / overall;
        if (scale <= 0f || float.IsInfinity(scale) || float.IsNaN(scale))
        {
            return null;
        }

        float cursorAlongAxis = (float)(mode == UI.LayoutMode.Vertical ? pos.Y / scale : pos.X / scale);

        float cursor = 0f;
        foreach (LiveSplit.UI.Components.IComponent component in Host.Renderer.VisibleComponents)
        {
            float size = mode == UI.LayoutMode.Vertical ? component.VerticalHeight : component.HorizontalWidth;
            if (cursorAlongAxis >= cursor && cursorAlongAxis <= cursor + size)
            {
                return component;
            }

            cursor += size;
        }

        return null;
    }

    private void OnContextMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (sender is not ContextMenu menu)
        {
            return;
        }

        // Strip any items we added on the previous open so the menu doesn't grow unbounded.
        var staticItems = new List<object>();
        foreach (object item in menu.Items)
        {
            if (item is MenuItem mi && (mi.Tag as string) == "__component_action")
            {
                continue;
            }

            if (item is Separator s && (s.Tag as string) == "__component_separator")
            {
                continue;
            }

            staticItems.Add(item);
        }

        menu.Items.Clear();
        if (_contextMenuComponent?.ContextMenuControls is { Count: > 0 } actions)
        {
            foreach (KeyValuePair<string, Action> kv in actions)
            {
                Action handler = kv.Value;
                var item = new MenuItem { Header = kv.Key, Tag = "__component_action" };
                item.Click += (_, _) =>
                {
                    try
                    {
                        handler?.Invoke();
                        InvalidateVisual();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                    }
                };
                menu.Items.Add(item);
            }

            menu.Items.Add(new Separator { Tag = "__component_separator" });
        }

        foreach (object item in staticItems)
        {
            menu.Items.Add(item);
        }
    }

    // --- Dynamic submenus (recents, comparisons, timing methods, server) -------------------

    private void WireDynamicSubmenus()
    {
        if (this.FindControl<MenuItem>("RecentSplitsMenu") is MenuItem recentSplits)
        {
            recentSplits.SubmenuOpened += (_, _) => PopulateRecentSplits(recentSplits);
        }

        if (this.FindControl<MenuItem>("RecentLayoutsMenu") is MenuItem recentLayouts)
        {
            recentLayouts.SubmenuOpened += (_, _) => PopulateRecentLayouts(recentLayouts);
        }

        if (this.FindControl<MenuItem>("ComparisonMenu") is MenuItem comparisons)
        {
            comparisons.SubmenuOpened += (_, _) => PopulateComparisons(comparisons);
        }

        if (this.FindControl<MenuItem>("TimingMethodMenu") is MenuItem timingMethod)
        {
            timingMethod.SubmenuOpened += (_, _) => PopulateTimingMethods(timingMethod);
        }

        if (this.FindControl<MenuItem>("ServerMenu") is MenuItem serverMenu)
        {
            serverMenu.SubmenuOpened += (_, _) => PopulateServerMenu(serverMenu);
        }

        if (this.FindControl<MenuItem>("LanguageMenu") is MenuItem languageMenu)
        {
            languageMenu.SubmenuOpened += (_, _) => PopulateLanguageMenu(languageMenu);
        }
    }

    private void PopulateRecentSplits(MenuItem parent)
    {
        var children = new List<MenuItem>();
        // Settings.AddToRecentSplits appends, so the most-recent entry is at the tail; reverse
        // for menu display.
        foreach (RecentSplitsFile entry in Host.State.Settings.RecentSplits.Reverse())
        {
            if (string.IsNullOrEmpty(entry.Path))
            {
                continue;
            }

            string capturedPath = entry.Path;
            var item = new MenuItem { Header = Path.GetFileName(capturedPath) };
            item.Click += (_, _) => Host.LoadRun(capturedPath);
            children.Add(item);
        }

        parent.ItemsSource = children;
    }

    private void PopulateRecentLayouts(MenuItem parent)
    {
        var children = new List<MenuItem>();
        foreach (string path in Host.State.Settings.RecentLayouts.Reverse())
        {
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            string capturedPath = path;
            var item = new MenuItem { Header = Path.GetFileName(capturedPath) };
            item.Click += (_, _) => Host.LoadLayout(capturedPath);
            children.Add(item);
        }

        parent.ItemsSource = children;
    }

    private void PopulateComparisons(MenuItem parent)
    {
        var children = new List<MenuItem>();
        string current = Host.State.CurrentComparison;
        foreach (string name in Host.State.Run.Comparisons)
        {
            string captured = name;
            var item = new MenuItem
            {
                Header = name,
                Icon = string.Equals(name, current, StringComparison.Ordinal)
                    ? new TextBlock { Text = "•" }
                    : null,
            };
            item.Click += (_, _) =>
            {
                Host.State.CurrentComparison = captured;
                InvalidateVisual();
            };
            children.Add(item);
        }

        parent.ItemsSource = children;
    }

    private void PopulateTimingMethods(MenuItem parent)
    {
        TimingMethod current = Host.State.CurrentTimingMethod;
        var realTime = new MenuItem
        {
            Header = "Real Time",
            Icon = current == TimingMethod.RealTime ? new TextBlock { Text = "•" } : null,
        };
        realTime.Click += (_, _) => { Host.State.CurrentTimingMethod = TimingMethod.RealTime; InvalidateVisual(); };

        var gameTime = new MenuItem
        {
            Header = "Game Time",
            Icon = current == TimingMethod.GameTime ? new TextBlock { Text = "•" } : null,
        };
        gameTime.Click += (_, _) => { Host.State.CurrentTimingMethod = TimingMethod.GameTime; InvalidateVisual(); };

        parent.Items.Clear();
        parent.Items.Add(realTime);
        parent.Items.Add(gameTime);
    }

    private void PopulateLanguageMenu(MenuItem parent)
    {
        string current = LiveSplit.Localization.LanguageResolver.NormalizeSettingValue(Host.State.Settings.UILanguage);

        parent.Items.Clear();

        var auto = new MenuItem
        {
            Header = "Auto (System Default)",
            Icon = string.IsNullOrEmpty(current) ? new TextBlock { Text = "•" } : null,
        };
        auto.Click += async (_, _) => await ApplyLanguage(string.Empty);
        parent.Items.Add(auto);
        parent.Items.Add(new Separator());

        foreach (LiveSplit.Localization.AppLanguage language in LiveSplit.Localization.UiTextCatalog.Languages)
        {
            string captured = language.Code;
            var item = new MenuItem
            {
                Header = language.DisplayName,
                Icon = string.Equals(captured, current, StringComparison.OrdinalIgnoreCase)
                    ? new TextBlock { Text = "•" }
                    : null,
            };
            item.Click += async (_, _) => await ApplyLanguage(captured);
            parent.Items.Add(item);
        }
    }

    private async Task ApplyLanguage(string code)
    {
        Host.State.Settings.UILanguage = code;
        LiveSplit.Localization.LanguageResolver.SetCurrentLanguageSetting(code);
        var dlg = new MessageDialog(
            "Language",
            "The language change applies the next time LiveSplit starts.");
        await dlg.ShowDialogAsync(this);
    }

    private void PopulateServerMenu(MenuItem parent)
    {
        var children = new List<MenuItem>();

        var startTcp = new MenuItem { Header = "Start TCP Server" };
        startTcp.Click += (_, _) => StartServer(ServerStartupType.TCP);
        children.Add(startTcp);

        var startWs = new MenuItem { Header = "Start WebSocket Server" };
        startWs.Click += (_, _) => StartServer(ServerStartupType.Websocket);
        children.Add(startWs);

        var startPipe = new MenuItem { Header = "Start Named Pipe Server" };
        startPipe.Click += (_, _) => StartNamedPipeServer();
        children.Add(startPipe);

        if (_commandServer is { ServerState: not ServerStateType.Off })
        {
            children.Add(new MenuItem { Header = "-" });
            var stop = new MenuItem { Header = $"Stop Server ({_commandServer.ServerState})" };
            stop.Click += (_, _) => StopServer();
            children.Add(stop);
        }

        parent.ItemsSource = children;
    }

    // --- Server lifecycle -------------------------------------------------------------------

    private void StartConfiguredServer()
    {
        try
        {
            switch (Host.State.Settings.ServerStartup)
            {
                case ServerStartupType.TCP:
                    StartServer(ServerStartupType.TCP);
                    break;
                case ServerStartupType.Websocket:
                    StartServer(ServerStartupType.Websocket);
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    private void StartServer(ServerStartupType type)
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

            Host.State.Settings.ServerStartup = type;
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

    private void StopServer()
    {
        try
        {
            _commandServer?.StopAll();
            Host.State.Settings.ServerStartup = ServerStartupType.Off;
            Host.State.Settings.ServerState = ServerStateType.Off;
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    // --- Drag and drop ----------------------------------------------------------------------

    private static readonly string[] DraggableExtensions = [".lss", ".lsl"];

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.DragEffects = ContainsDraggableFile(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
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
