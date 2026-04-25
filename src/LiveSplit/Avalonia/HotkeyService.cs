using System;
using System.Collections.Generic;

using global::Avalonia.Threading;

using LiveSplit.Model;
using LiveSplit.Model.Input;
using LiveSplit.Options;

using SharpHook;
using SharpHook.Native;

namespace LiveSplit.Avalonia;

/// <summary>
/// System-wide keyboard hotkey listener built on <c>SharpHook</c> (libuiohook). Maps
/// global key presses against the active <see cref="HotkeyProfile"/> on the supplied
/// <see cref="LiveSplitState"/> and invokes the corresponding <see cref="ITimerModel"/>
/// action when a binding matches.
///
/// Why SharpHook: livesplit-core's C-API <c>HotkeySystem</c> is bound to a Rust
/// <c>SharedTimer</c> and would split a separate Rust-side timer rather than LiveSplit's C#
/// <see cref="TimerModel"/>. Adding a new C-API surface to expose the lower-level
/// <c>livesplit_hotkey::Hook</c> would require a Rust + capi + bindgen change. SharpHook ships
/// a managed wrapper around <c>libuiohook</c> with native binaries for linux-x64, win-x64, and
/// osx-x64 — drop-in for the linux-port + matches what the original Windows build did
/// (RegisterHotKey via Win32) on the platforms we ship to.
///
/// On Wayland without a compositor portal, libuiohook can't grab global keys; the
/// per-window <c>KeyBinding</c>s in <see cref="TimerWindow.axaml"/> are kept as a fallback.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private readonly LiveSplitState _state;
    private readonly ITimerModel _model;
    private TaskPoolGlobalHook _hook;
    private bool _disposed;

    public HotkeyService(LiveSplitState state, ITimerModel model)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public void Start()
    {
        if (_hook is not null || _disposed)
        {
            return;
        }

        try
        {
            _hook = new TaskPoolGlobalHook();
            _hook.KeyPressed += OnKeyPressed;
            // Run async so the constructor doesn't block; libuiohook's main loop runs in a
            // worker thread inside SharpHook.
            _ = _hook.RunAsync();
        }
        catch (Exception ex)
        {
            // libuiohook startup failures (no X11 display, missing perms, headless CI) shouldn't
            // crash the app — the user still has the per-window KeyBindings.
            Log.Error(ex);
            _hook?.Dispose();
            _hook = null;
        }
    }

    private void OnKeyPressed(object sender, KeyboardHookEventArgs e)
    {
        if (_disposed || _state?.Settings?.HotkeyProfiles is null)
        {
            return;
        }

        if (!_state.Settings.HotkeyProfiles.TryGetValue(_state.CurrentHotkeyProfile, out HotkeyProfile profile)
            || profile is null)
        {
            return;
        }

        if (!profile.GlobalHotkeysEnabled)
        {
            return;
        }

        Key? mapped = ToLiveSplitKey(e.Data.KeyCode);
        if (mapped is null)
        {
            return;
        }

        // The profile binds a KeyOrButton, but we only care about the keyboard half here —
        // gamepad inputs would need a different code path.
        Action target = SelectAction(profile, mapped.Value);
        if (target is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                target();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        });
    }

    private Action SelectAction(HotkeyProfile profile, Key pressed)
    {
        if (Matches(profile.SplitKey, pressed)) { return _model.Split; }
        if (Matches(profile.ResetKey, pressed)) { return () => _model.Reset(); }
        if (Matches(profile.SkipKey, pressed)) { return _model.SkipSplit; }
        if (Matches(profile.UndoKey, pressed)) { return _model.UndoSplit; }
        if (Matches(profile.PauseKey, pressed)) { return _model.Pause; }
        if (Matches(profile.SwitchComparisonPrevious, pressed)) { return SwitchComparisonPrevious; }
        if (Matches(profile.SwitchComparisonNext, pressed)) { return SwitchComparisonNext; }
        return null;
    }

    private void SwitchComparisonPrevious()
    {
        var comparisons = new List<string>(_state.Run.Comparisons);
        int idx = IndexOfComparison(comparisons, _state.CurrentComparison);
        if (idx < 0)
        {
            return;
        }

        _state.CurrentComparison = comparisons[(idx - 1 + comparisons.Count) % comparisons.Count];
    }

    private void SwitchComparisonNext()
    {
        var comparisons = new List<string>(_state.Run.Comparisons);
        int idx = IndexOfComparison(comparisons, _state.CurrentComparison);
        if (idx < 0)
        {
            return;
        }

        _state.CurrentComparison = comparisons[(idx + 1) % comparisons.Count];
    }

    private static int IndexOfComparison(IList<string> comparisons, string current)
    {
        for (int i = 0; i < comparisons.Count; i++)
        {
            if (string.Equals(comparisons[i], current, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool Matches(KeyOrButton binding, Key pressed)
    {
        return binding is { IsKey: true } && binding.Key == pressed;
    }

    /// <summary>
    /// Map SharpHook's <see cref="KeyCode"/> to LiveSplit's <see cref="Key"/>. Most names line
    /// up via the <c>Vc</c> prefix (e.g. <c>VcF1</c> ↔ <see cref="Key.F1"/>); a small dictionary
    /// covers the cases where the names diverge (numpad arithmetic keys, modifiers, common
    /// punctuation).
    /// </summary>
    private static Key? ToLiveSplitKey(KeyCode code) => code switch
    {
        // Letters
        KeyCode.VcA => Key.A, KeyCode.VcB => Key.B, KeyCode.VcC => Key.C, KeyCode.VcD => Key.D,
        KeyCode.VcE => Key.E, KeyCode.VcF => Key.F, KeyCode.VcG => Key.G, KeyCode.VcH => Key.H,
        KeyCode.VcI => Key.I, KeyCode.VcJ => Key.J, KeyCode.VcK => Key.K, KeyCode.VcL => Key.L,
        KeyCode.VcM => Key.M, KeyCode.VcN => Key.N, KeyCode.VcO => Key.O, KeyCode.VcP => Key.P,
        KeyCode.VcQ => Key.Q, KeyCode.VcR => Key.R, KeyCode.VcS => Key.S, KeyCode.VcT => Key.T,
        KeyCode.VcU => Key.U, KeyCode.VcV => Key.V, KeyCode.VcW => Key.W, KeyCode.VcX => Key.X,
        KeyCode.VcY => Key.Y, KeyCode.VcZ => Key.Z,

        // Digits (top row)
        KeyCode.Vc0 => Key.D0, KeyCode.Vc1 => Key.D1, KeyCode.Vc2 => Key.D2, KeyCode.Vc3 => Key.D3,
        KeyCode.Vc4 => Key.D4, KeyCode.Vc5 => Key.D5, KeyCode.Vc6 => Key.D6, KeyCode.Vc7 => Key.D7,
        KeyCode.Vc8 => Key.D8, KeyCode.Vc9 => Key.D9,

        // Function row
        KeyCode.VcF1 => Key.F1, KeyCode.VcF2 => Key.F2, KeyCode.VcF3 => Key.F3, KeyCode.VcF4 => Key.F4,
        KeyCode.VcF5 => Key.F5, KeyCode.VcF6 => Key.F6, KeyCode.VcF7 => Key.F7, KeyCode.VcF8 => Key.F8,
        KeyCode.VcF9 => Key.F9, KeyCode.VcF10 => Key.F10, KeyCode.VcF11 => Key.F11, KeyCode.VcF12 => Key.F12,
        KeyCode.VcF13 => Key.F13, KeyCode.VcF14 => Key.F14, KeyCode.VcF15 => Key.F15, KeyCode.VcF16 => Key.F16,
        KeyCode.VcF17 => Key.F17, KeyCode.VcF18 => Key.F18, KeyCode.VcF19 => Key.F19, KeyCode.VcF20 => Key.F20,
        KeyCode.VcF21 => Key.F21, KeyCode.VcF22 => Key.F22, KeyCode.VcF23 => Key.F23, KeyCode.VcF24 => Key.F24,

        // Numpad
        KeyCode.VcNumPad0 => Key.NumPad0, KeyCode.VcNumPad1 => Key.NumPad1,
        KeyCode.VcNumPad2 => Key.NumPad2, KeyCode.VcNumPad3 => Key.NumPad3,
        KeyCode.VcNumPad4 => Key.NumPad4, KeyCode.VcNumPad5 => Key.NumPad5,
        KeyCode.VcNumPad6 => Key.NumPad6, KeyCode.VcNumPad7 => Key.NumPad7,
        KeyCode.VcNumPad8 => Key.NumPad8, KeyCode.VcNumPad9 => Key.NumPad9,
        KeyCode.VcNumPadAdd => Key.Add,
        KeyCode.VcNumPadSubtract => Key.Subtract,
        KeyCode.VcNumPadMultiply => Key.Multiply,
        KeyCode.VcNumPadDivide => Key.Divide,
        KeyCode.VcNumPadDecimal => Key.Decimal,
        KeyCode.VcNumPadEnter => Key.Enter,
        KeyCode.VcNumPadSeparator => Key.Separator,

        // Whitespace + control
        KeyCode.VcSpace => Key.Space,
        KeyCode.VcEnter => Key.Enter,
        KeyCode.VcTab => Key.Tab,
        KeyCode.VcBackspace => Key.Back,
        KeyCode.VcEscape => Key.Escape,
        KeyCode.VcCapsLock => Key.CapsLock,
        KeyCode.VcInsert => Key.Insert,
        KeyCode.VcDelete => Key.Delete,
        KeyCode.VcHome => Key.Home,
        KeyCode.VcEnd => Key.End,
        KeyCode.VcPageUp => Key.PageUp,
        KeyCode.VcPageDown => Key.PageDown,
        KeyCode.VcPrintScreen => Key.PrintScreen,
        KeyCode.VcScrollLock => Key.Scroll,
        KeyCode.VcPause => Key.Pause,
        KeyCode.VcNumLock => Key.NumLock,

        // Arrows
        KeyCode.VcUp => Key.Up,
        KeyCode.VcDown => Key.Down,
        KeyCode.VcLeft => Key.Left,
        KeyCode.VcRight => Key.Right,

        // Modifiers (left/right variants exist; pick a canonical mapping)
        KeyCode.VcLeftShift => Key.LShiftKey,
        KeyCode.VcRightShift => Key.RShiftKey,
        KeyCode.VcLeftControl => Key.LControlKey,
        KeyCode.VcRightControl => Key.RControlKey,
        KeyCode.VcLeftAlt => Key.LMenu,
        KeyCode.VcRightAlt => Key.RMenu,
        KeyCode.VcLeftMeta => Key.LWin,
        KeyCode.VcRightMeta => Key.RWin,

        // Common OEM punctuation
        KeyCode.VcSemicolon => Key.OemSemicolon,
        KeyCode.VcEquals => Key.Oemplus,
        KeyCode.VcComma => Key.Oemcomma,
        KeyCode.VcMinus => Key.OemMinus,
        KeyCode.VcPeriod => Key.OemPeriod,
        KeyCode.VcSlash => Key.OemQuestion,
        KeyCode.VcBackQuote => Key.Oemtilde,
        KeyCode.VcOpenBracket => Key.OemOpenBrackets,
        KeyCode.VcBackslash => Key.OemPipe,
        KeyCode.VcCloseBracket => Key.OemCloseBrackets,
        KeyCode.VcQuote => Key.OemQuotes,

        // Browser / media (rarely used as speedrun hotkeys but cheap to forward)
        KeyCode.VcBrowserBack => Key.BrowserBack,
        KeyCode.VcBrowserForward => Key.BrowserForward,
        KeyCode.VcBrowserRefresh => Key.BrowserRefresh,
        KeyCode.VcBrowserStop => Key.BrowserStop,
        KeyCode.VcBrowserSearch => Key.BrowserSearch,
        KeyCode.VcBrowserFavorites => Key.BrowserFavorites,
        KeyCode.VcBrowserHome => Key.BrowserHome,
        KeyCode.VcVolumeMute => Key.VolumeMute,
        KeyCode.VcVolumeDown => Key.VolumeDown,
        KeyCode.VcVolumeUp => Key.VolumeUp,
        KeyCode.VcMediaNext => Key.MediaNextTrack,
        KeyCode.VcMediaPrevious => Key.MediaPreviousTrack,
        KeyCode.VcMediaStop => Key.MediaStop,
        KeyCode.VcMediaPlay => Key.MediaPlayPause,

        _ => null,
    };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (_hook is not null)
            {
                _hook.KeyPressed -= OnKeyPressed;
                _hook.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
        finally
        {
            _hook = null;
        }
    }
}
