using System;
using System.Threading.Tasks;
using System.Windows.Input;

using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Markup.Xaml;
using global::Avalonia.Threading;

using LiveSplit.Avalonia.Dialogs;

namespace LiveSplit.Avalonia;

/// <summary>
/// Hosts a <see cref="SkiaRenderControl"/> backed by an <see cref="AvaloniaTimerHost"/>, plus
/// window-focused split/reset/skip/undo/pause keys. Window dragging is wired through
/// <see cref="Window.BeginMoveDrag"/>; right-click opens a context menu for editing splits /
/// layout / settings / size / about / close.
/// </summary>
public sealed partial class TimerWindow : Window
{
    public AvaloniaTimerHost Host { get; }

    public ICommand SplitCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand SkipCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand PauseCommand { get; }

    public ICommand EditSplitsCommand { get; }
    public ICommand EditLayoutCommand { get; }
    public ICommand LayoutSettingsCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand SetSizeCommand { get; }
    public ICommand AboutCommand { get; }
    public ICommand CloseCommand { get; }

    public TimerWindow()
        : this(splitsPath: null, layoutPath: null)
    {
    }

    public TimerWindow(string splitsPath, string layoutPath)
    {
        AvaloniaXamlLoader.Load(this);

        Host = new AvaloniaTimerHost(InvalidateVisual, splitsPath, layoutPath);

        SplitCommand = new RelayCommand(() => Host.Model.Split());
        ResetCommand = new RelayCommand(() => Host.Model.Reset());
        SkipCommand = new RelayCommand(() => Host.Model.SkipSplit());
        UndoCommand = new RelayCommand(() => Host.Model.UndoSplit());
        PauseCommand = new RelayCommand(() => Host.Model.Pause());

        EditSplitsCommand = new RelayCommand(async () => await OpenEditSplits());
        EditLayoutCommand = new RelayCommand(async () => await OpenEditLayout());
        LayoutSettingsCommand = new RelayCommand(async () => await OpenLayoutSettings());
        SettingsCommand = new RelayCommand(async () => await OpenSettings());
        SetSizeCommand = new RelayCommand(async () => await OpenSetSize());
        AboutCommand = new RelayCommand(async () => await OpenAbout());
        CloseCommand = new RelayCommand(Close);

        DataContext = this;

        if (this.FindControl<SkiaRenderControl>("Canvas") is SkiaRenderControl canvas)
        {
            canvas.Host = Host;
        }

        // Tunnel-phase so the handler runs even when child controls swallow the bubble; the
        // right-click ContextMenu wired via XAML still surfaces normally.
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        Closed += OnClosed;
    }

    private void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        PointerPointProperties props = e.GetCurrentPoint(this).Properties;
        if (props.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
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
        await dlg.ShowDialogAsync(this);
    }

    private async Task OpenAbout()
    {
        var dlg = new AboutBox();
        await dlg.ShowDialog(this);
    }

    private void OnClosed(object sender, EventArgs e)
    {
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

        public event EventHandler CanExecuteChanged;
    }
}
