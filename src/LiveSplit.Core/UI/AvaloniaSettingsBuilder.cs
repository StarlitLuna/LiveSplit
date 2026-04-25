using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Layout;
using global::Avalonia.Media;

namespace LiveSplit.UI;

/// <summary>
/// Auto-generates an Avalonia settings panel from an arbitrary object's public read/write
/// properties. Each property maps to a widget appropriate for its type: bool →
/// <see cref="CheckBox"/>, enum → <see cref="ComboBox"/>, numeric/string → <see cref="TextBox"/>,
/// <see cref="System.Drawing.Color"/> → swatch + hex textbox. Properties of unsupported types
/// (Font, list-of-complex-objects, …) are skipped silently.
///
/// XML round-trip of the underlying settings object is the caller's responsibility — this
/// builder only reads / writes the same public properties any other consumer would touch.
/// </summary>
public static class AvaloniaSettingsBuilder
{
    /// <summary>
    /// Names of properties that should be excluded from the auto-generated panel — typically
    /// internal helpers like <c>GradientString</c> that wrap a real enum, or read-only metadata
    /// like <c>Mode</c> that the component sets, not the user.
    /// </summary>
    private static readonly HashSet<string> ExcludedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "GradientString", "Mode", "ComponentName", "Width", "Height", "Padding",
        "Anchor", "Dock", "TabIndex", "Tag", "Name", "Site", "Visible",
        "Enabled", "BackColor", "ForeColor", "Font", "Cursor", "Bounds",
        "Size", "Location", "Margin", "MinimumSize", "MaximumSize",
        "AutoSize", "Region", "DataContext",
    };

    public static Control Build(object settings, string title = null)
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 8,
            Orientation = Orientation.Vertical,
        };

        if (!string.IsNullOrEmpty(title))
        {
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(0, 0, 0, 4),
            });
        }

        if (settings == null)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "(no settings)",
                Foreground = global::Avalonia.Media.Brushes.Gray as IBrush,
            });
            return new ScrollViewer { Content = stack };
        }

        Type t = settings.GetType();
        PropertyInfo[] props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (PropertyInfo prop in props)
        {
            if (!prop.CanRead || !prop.CanWrite)
            {
                continue;
            }

            if (ExcludedNames.Contains(prop.Name))
            {
                continue;
            }

            // Skip indexers and properties that need special handling.
            if (prop.GetIndexParameters().Length > 0)
            {
                continue;
            }

            Control row = BuildRowForProperty(settings, prop);
            if (row != null)
            {
                stack.Children.Add(row);
            }
        }

        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stack,
        };
    }

    private static Control BuildRowForProperty(object target, PropertyInfo prop)
    {
        Type type = prop.PropertyType;

        if (type == typeof(bool))
        {
            return BuildCheckBox(target, prop);
        }

        if (type.IsEnum)
        {
            return BuildEnumCombo(target, prop);
        }

        if (type == typeof(System.Drawing.Color))
        {
            return BuildColorRow(target, prop);
        }

        if (type == typeof(string))
        {
            return BuildTextRow(target, prop, isNumeric: false);
        }

        if (type == typeof(int) || type == typeof(float) || type == typeof(double)
            || type == typeof(short) || type == typeof(byte) || type == typeof(long)
            || type == typeof(decimal))
        {
            return BuildTextRow(target, prop, isNumeric: true);
        }

        // Unsupported property type (System.Drawing.Font, lists of complex objects, etc.) —
        // skip; the value still round-trips through XML, just isn't editable in this panel.
        return null;
    }

    private static Control BuildCheckBox(object target, PropertyInfo prop)
    {
        bool initial = (bool)prop.GetValue(target)!;
        var cb = new CheckBox
        {
            Content = Humanize(prop.Name),
            IsChecked = initial,
        };
        cb.IsCheckedChanged += (_, _) => prop.SetValue(target, cb.IsChecked == true);
        return cb;
    }

    private static Control BuildEnumCombo(object target, PropertyInfo prop)
    {
        Array values = Enum.GetValues(prop.PropertyType);
        var combo = new ComboBox
        {
            ItemsSource = values,
            SelectedItem = prop.GetValue(target),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is not null)
            {
                prop.SetValue(target, combo.SelectedItem);
            }
        };
        return WithLabel(prop.Name, combo);
    }

    private static Control BuildColorRow(object target, PropertyInfo prop)
    {
        System.Drawing.Color initial = (System.Drawing.Color)prop.GetValue(target)!;
        var swatch = new Border
        {
            Width = 60,
            Height = 22,
            BorderThickness = new Thickness(1),
            BorderBrush = global::Avalonia.Media.Brushes.Gray,
            Background = new SolidColorBrush(global::Avalonia.Media.Color.FromArgb(initial.A, initial.R, initial.G, initial.B)),
        };
        var hexBox = new TextBox
        {
            Text = $"#{initial.A:X2}{initial.R:X2}{initial.G:X2}{initial.B:X2}",
            Width = 110,
        };
        hexBox.LostFocus += (_, _) =>
        {
            if (TryParseArgbHex(hexBox.Text, out System.Drawing.Color parsed))
            {
                prop.SetValue(target, parsed);
                swatch.Background = new SolidColorBrush(global::Avalonia.Media.Color.FromArgb(parsed.A, parsed.R, parsed.G, parsed.B));
            }
            else
            {
                System.Drawing.Color current = (System.Drawing.Color)prop.GetValue(target)!;
                hexBox.Text = $"#{current.A:X2}{current.R:X2}{current.G:X2}{current.B:X2}";
            }
        };

        var inner = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { swatch, hexBox },
        };
        return WithLabel(prop.Name, inner);
    }

    private static Control BuildTextRow(object target, PropertyInfo prop, bool isNumeric)
    {
        object current = prop.GetValue(target);
        var box = new TextBox
        {
            Text = current?.ToString() ?? string.Empty,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        box.LostFocus += (_, _) =>
        {
            try
            {
                object parsed = Convert.ChangeType(box.Text, prop.PropertyType, System.Globalization.CultureInfo.InvariantCulture);
                prop.SetValue(target, parsed);
            }
            catch
            {
                // On parse failure, snap the textbox back to the live value.
                object live = prop.GetValue(target);
                box.Text = live?.ToString() ?? string.Empty;
            }
        };
        return WithLabel(prop.Name, box);
    }

    private static Control WithLabel(string name, Control inner)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("160,*"),
        };
        var label = new TextBlock
        {
            Text = Humanize(name) + ":",
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 0);
        Grid.SetColumn(inner, 1);
        grid.Children.Add(label);
        grid.Children.Add(inner);
        return grid;
    }

    private static string Humanize(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return identifier;
        }

        var sb = new System.Text.StringBuilder(identifier.Length + 8);
        for (int i = 0; i < identifier.Length; i++)
        {
            char c = identifier[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(identifier[i - 1]))
            {
                sb.Append(' ');
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static bool TryParseArgbHex(string text, out System.Drawing.Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string s = text.Trim();
        if (s.StartsWith('#'))
        {
            s = s[1..];
        }

        if (s.Length == 6)
        {
            // RGB → opaque
            if (uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out uint rgb))
            {
                color = System.Drawing.Color.FromArgb(255, (int)((rgb >> 16) & 0xFF), (int)((rgb >> 8) & 0xFF), (int)(rgb & 0xFF));
                return true;
            }
        }
        else if (s.Length == 8)
        {
            if (uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out uint argb))
            {
                color = System.Drawing.Color.FromArgb((int)((argb >> 24) & 0xFF), (int)((argb >> 16) & 0xFF), (int)((argb >> 8) & 0xFF), (int)(argb & 0xFF));
                return true;
            }
        }

        return false;
    }
}
