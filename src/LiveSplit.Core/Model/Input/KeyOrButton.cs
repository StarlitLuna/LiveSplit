using System;
using System.Windows.Forms;

namespace LiveSplit.Model.Input;

public delegate void EventHandlerT<T>(object sender, T value);

public struct GamepadButton
{
    public string GamepadName;
    public string Button;

    public GamepadButton(string gamepadName, string button)
    {
        GamepadName = gamepadName;
        Button = button;
    }

    public static bool operator ==(GamepadButton a, GamepadButton b)
    {
        return a.GamepadName == b.GamepadName && a.Button == b.Button;
    }

    public static bool operator !=(GamepadButton a, GamepadButton b)
    {
        return !(a == b);
    }

    public override readonly bool Equals(object obj)
    {
        if (obj is GamepadButton button)
        {
            return this == button;
        }

        return base.Equals(obj);
    }

    public override readonly int GetHashCode()
    {
        return GamepadName.GetHashCode() ^ Button.GetHashCode();
    }
}

public class KeyOrButton
{
    public bool IsButton { get; protected set; }
    public bool IsKey { get => !IsButton; set => IsButton = !value; }

    public Keys Key { get; protected set; }
    public GamepadButton Button { get; protected set; }

    public KeyOrButton(Keys key)
    {
        Key = key;
        IsKey = true;
    }

    public KeyOrButton(GamepadButton button)
    {
        Button = button;
        IsButton = true;
    }

    public KeyOrButton(string stringRepresentation)
    {
        if (stringRepresentation.Contains(" ") && !stringRepresentation.Contains(", "))
        {
            string[] split = stringRepresentation.Split(new char[] { ' ' }, 2);
            Button = new GamepadButton(split[1], split[0]);
            IsButton = true;
        }
        else
        {
            Key = (Keys)Enum.Parse(typeof(Keys), stringRepresentation, true);
            IsKey = true;
        }
    }

    public override string ToString()
    {
        if (IsKey)
        {
            return Key.ToString();
        }
        else
        {
            return Button.Button.ToString() + " " + Button.GamepadName;
        }
    }

    public static bool operator ==(KeyOrButton a, KeyOrButton b)
    {
        if (a is null && b is null)
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        if (a.IsKey && b.IsKey)
        {
            return a.Key == b.Key;
        }
        else if (a.IsButton && b.IsButton)
        {
            return a.Button == b.Button;
        }

        return false;
    }

    public static bool operator !=(KeyOrButton a, KeyOrButton b)
    {
        return !(a == b);
    }

    public override bool Equals(object obj)
    {
        return obj is KeyOrButton other
            && this == other;
    }

    public override int GetHashCode()
    {
        return IsKey ? Key.GetHashCode() : Button.GetHashCode();
    }
}
