using System;
using System.Collections.Generic;

namespace LiveSplit.Model.Input;

public static class KeyExtensions
{
    private const Key SupportedModifiers = Key.Shift | Key.Control | Key.Alt;

    public static Key GetKeyCode(this Key key)
        => key & Key.KeyCode;

    public static Key GetModifiers(this Key key)
        => key & SupportedModifiers;

    public static Key WithModifiers(this Key keyCode, Key modifiers)
        => keyCode.GetKeyCode() | modifiers.GetModifiers();

    public static bool IsModifierKeyCode(this Key key)
    {
        Key keyCode = key.GetKeyCode();
        return keyCode is Key.ShiftKey or Key.ControlKey or Key.Menu
            or Key.LShiftKey or Key.RShiftKey
            or Key.LControlKey or Key.RControlKey
            or Key.LMenu or Key.RMenu;
    }

    public static string ToMasterString(this Key key)
    {
        Key modifiers = key.GetModifiers();
        Key keyCode = key.GetKeyCode();
        var parts = new List<string>(4);

        if ((modifiers & Key.Control) == Key.Control)
        {
            parts.Add(nameof(Key.Control));
        }

        if ((modifiers & Key.Shift) == Key.Shift)
        {
            parts.Add(nameof(Key.Shift));
        }

        if ((modifiers & Key.Alt) == Key.Alt)
        {
            parts.Add(nameof(Key.Alt));
        }

        if (keyCode != Key.None || parts.Count == 0)
        {
            parts.Add(keyCode.ToString());
        }

        return string.Join(", ", parts);
    }

    public static bool TryParseMasterString(string value, out Key key)
    {
        key = Key.None;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            parts = [value.Trim()];
        }

        foreach (string part in parts)
        {
            if (!TryParsePart(part, out Key parsed))
            {
                return false;
            }

            key |= parsed;
        }

        return true;
    }

    private static bool TryParsePart(string part, out Key key)
    {
        if (string.Equals(part, nameof(Key.Control), StringComparison.OrdinalIgnoreCase))
        {
            key = Key.Control;
            return true;
        }

        if (string.Equals(part, nameof(Key.Shift), StringComparison.OrdinalIgnoreCase))
        {
            key = Key.Shift;
            return true;
        }

        if (string.Equals(part, nameof(Key.Alt), StringComparison.OrdinalIgnoreCase))
        {
            key = Key.Alt;
            return true;
        }

        return Enum.TryParse(part, ignoreCase: true, out key);
    }

    public static bool MatchesKey(this Key binding, Key pressed)
        => binding == pressed;

    public static Key FromModifierBooleans(bool shift, bool control, bool alt)
    {
        Key result = Key.None;
        if (shift)
        {
            result |= Key.Shift;
        }

        if (control)
        {
            result |= Key.Control;
        }

        if (alt)
        {
            result |= Key.Alt;
        }

        return result;
    }
}
