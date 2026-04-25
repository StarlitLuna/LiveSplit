using System;
using System.Windows.Input;

using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Markup.Xaml;

namespace LiveSplit.Avalonia;

/// <summary>
/// Avalonia replacement for <c>TimerForm</c>. Hosts a <see cref="SkiaRenderControl"/> backed by
/// an <see cref="AvaloniaTimerHost"/>, plus window-focused split/reset/skip/undo/pause keys
/// (global hotkeys + gamepad were dropped as part of Phase 1 — see plan). Window dragging is
/// wired through Avalonia's <see cref="Window.BeginMoveDrag"/>; transparency is left at the
/// solid-background "good enough" default per the linux-port plan's fallback.
/// </summary>
public sealed partial class TimerWindow : Window
{
    public AvaloniaTimerHost Host { get; }

    public ICommand SplitCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand SkipCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand PauseCommand { get; }

    public TimerWindow()
    {
        AvaloniaXamlLoader.Load(this);

        Host = new AvaloniaTimerHost(InvalidateVisual);

        SplitCommand = new RelayCommand(() => Host.Model.Split());
        ResetCommand = new RelayCommand(() => Host.Model.Reset());
        SkipCommand = new RelayCommand(() => Host.Model.SkipSplit());
        UndoCommand = new RelayCommand(() => Host.Model.UndoSplit());
        PauseCommand = new RelayCommand(() => Host.Model.Pause());

        DataContext = this;

        // Hand the host to the render control so its paint loop can read state + renderer.
        if (this.FindControl<SkiaRenderControl>("Canvas") is SkiaRenderControl canvas)
        {
            canvas.Host = Host;
        }

        // Click-and-drag the window background. Mirrors TimerForm's WM_NCLBUTTONDOWN trick.
        PointerPressed += OnPointerPressed;
        Closed += OnClosed;
    }

    private void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnClosed(object sender, EventArgs e)
    {
        Host?.Dispose();
    }

    private sealed class RelayCommand : ICommand
    {
        private readonly Action _action;
        public RelayCommand(Action action) => _action = action;
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _action();
        public event EventHandler CanExecuteChanged;
    }
}
