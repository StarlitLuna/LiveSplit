using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

using LiveSplit.Options;
using LiveSplit.UI;
using LiveSplit.UI.Components;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>
/// Edits the <c>LayoutSettings</c> object (background, fonts, shadow color, text color, etc.)
/// via the reflection-driven <see cref="AvaloniaSettingsBuilder"/>. Font fields are not
/// editable here — the auto-generated panel skips them.
/// </summary>
public sealed class LayoutSettingsDialog : Window
{
    private readonly TaskCompletionSource<bool> _result = new();
    private readonly LayoutSettings _targetSettings;
    private readonly LayoutSettings _snapshot;
    private readonly List<ComponentSnapshot> _componentSnapshots = [];
    private bool _accepted;

    public LayoutSettingsDialog(object layoutSettings, ILayout layout = null, IComponent selectedComponent = null)
    {
        _targetSettings = layoutSettings as LayoutSettings;
        _snapshot = _targetSettings?.Clone() as LayoutSettings;

        Title = "Layout Settings";
        Width = LayoutSettingsDialogLayoutSpec.Master.InitialClientWidth;
        Height = LayoutSettingsDialogLayoutSpec.Master.InitialClientHeight;
        MinWidth = LayoutSettingsDialogLayoutSpec.Master.MinimumWindowWidth;
        MinHeight = LayoutSettingsDialogLayoutSpec.Master.MinimumWindowHeight;
        MaxWidth = LayoutSettingsDialogLayoutSpec.Master.MaximumWindowWidth;
        MaxHeight = 10000;
        DialogTheme.ApplyWindow(this);

        Control settingsControl = layout is null
            ? AvaloniaSettingsBuilder.Build(layoutSettings, "Layout Settings")
            : CreateTabs(_targetSettings, layout, selectedComponent, _componentSnapshots);

        var ok = CreateFooterButton("OK");
        ok.HorizontalAlignment = HorizontalAlignment.Right;
        ok.IsDefault = true;
        ok.Click += (_, _) =>
        {
            ok.Focus();
            _accepted = true;
            _result.TrySetResult(true);
            Close();
        };
        var cancel = CreateFooterButton("Cancel");
        cancel.IsCancel = true;
        cancel.Click += (_, _) => { _result.TrySetResult(false); Close(); };

        var root = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,81"),
            RowDefinitions = new RowDefinitions("*,29"),
            Margin = new Thickness(LayoutSettingsDialogLayoutSpec.Master.Padding),
        };
        Grid.SetColumn(settingsControl, 0);
        Grid.SetRow(settingsControl, 0);
        Grid.SetColumnSpan(settingsControl, LayoutSettingsDialogLayoutSpec.Master.TabColumnSpan);
        Grid.SetColumn(ok, 0);
        Grid.SetRow(ok, 1);
        Grid.SetColumn(cancel, 1);
        Grid.SetRow(cancel, 1);
        root.Children.Add(settingsControl);
        root.Children.Add(ok);
        root.Children.Add(cancel);
        Content = root;

        Closed += (_, _) =>
        {
            if (!_result.Task.IsCompleted)
            {
                _result.TrySetResult(false);
            }

            if (!_accepted && _targetSettings != null && _snapshot != null)
            {
                RestoreSnapshotsForCancel();
            }
        };
    }

    internal void RestoreSnapshotsForCancel()
        => RestoreSnapshotsForCancel(_targetSettings, _snapshot, _componentSnapshots);

    private static Button CreateFooterButton(string text)
        => new()
        {
            Content = text,
            Width = LayoutSettingsDialogLayoutSpec.Master.ButtonWidth,
            Height = LayoutSettingsDialogLayoutSpec.Master.ButtonHeight,
            Margin = new Thickness(3),
            VerticalAlignment = VerticalAlignment.Center,
        };

    internal static void RestoreSnapshotsForCancel(
        LayoutSettings targetSettings,
        LayoutSettings snapshot,
        IEnumerable<ComponentSnapshot> componentSnapshots)
    {
        if (targetSettings != null && snapshot != null)
        {
            targetSettings.Assign(snapshot);
        }

        foreach (ComponentSnapshot componentSnapshot in componentSnapshots ?? [])
        {
            componentSnapshot.Restore();
        }
    }

    internal static TabControl CreateTabs(
        LayoutSettings settings,
        ILayout layout,
        IComponent selectedComponent = null,
        List<ComponentSnapshot> snapshots = null)
    {
        var tabs = new TabControl
        {
            Margin = new Thickness(8),
        };

        tabs.Items.Add(new TabItem
        {
            Header = "Layout",
            Content = AvaloniaSettingsBuilder.Build(settings, "Layout Settings"),
        });

        if (layout is null)
        {
            return tabs;
        }

        int selectedIndex = 0;
        foreach (ILayoutComponent layoutComponent in layout.LayoutComponents)
        {
            IComponent component = layoutComponent.Component;
            if (component is null)
            {
                continue;
            }

            Control settingsControl = component.GetSettingsControl(layout.Mode);
            GlobalFont usedFonts = GetUsedGlobalFonts(component);
            bool hasFontOverrides = layoutComponent is LayoutComponent && usedFonts != GlobalFont.None;
            if (settingsControl is null && !hasFontOverrides)
            {
                continue;
            }

            snapshots?.Add(ComponentSnapshot.Capture(layoutComponent));
            Control content = CreateComponentTabContent(
                settings,
                layoutComponent,
                settingsControl,
                usedFonts);
            tabs.Items.Add(new TabItem
            {
                Header = component.ComponentName,
                Content = content,
            });

            if (ReferenceEquals(component, selectedComponent))
            {
                selectedIndex = tabs.Items.Count - 1;
            }
        }

        tabs.SelectedIndex = selectedIndex;
        return tabs;
    }

    private static GlobalFont GetUsedGlobalFonts(IComponent component)
    {
        object[] attributes = component.GetType().GetCustomAttributes(typeof(GlobalFontConsumerAttribute), true);
        return attributes.Length > 0
            ? ((GlobalFontConsumerAttribute)attributes[0]).UsedGlobalFonts
            : GlobalFont.None;
    }

    private static Control CreateComponentTabContent(
        LayoutSettings settings,
        ILayoutComponent layoutComponent,
        Control settingsControl,
        GlobalFont usedFonts)
    {
        Control fontPanel = layoutComponent is LayoutComponent concrete && usedFonts != GlobalFont.None
            ? CreateFontOverridePanel(concrete.FontOverrides, settings, usedFonts)
            : null;
        if (fontPanel is null)
        {
            return settingsControl;
        }

        if (settingsControl is null)
        {
            return fontPanel;
        }

        return new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(8),
            Children =
            {
                fontPanel,
                settingsControl,
            },
        };
    }

    internal static Control CreateFontOverridePanel(
        FontOverrides overrides,
        LayoutSettings globalSettings,
        GlobalFont usedFonts)
    {
        var rows = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(8),
        };

        rows.Children.Add(new TextBlock
        {
            Text = "Font Overrides",
            FontWeight = global::Avalonia.Media.FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
        });

        if (usedFonts.HasFlag(GlobalFont.TimerFont))
        {
            rows.Children.Add(CreateFontOverrideRow(
                "OverrideTimerFont",
                "Override Timer Font",
                () => overrides.OverrideTimerFont,
                value => overrides.OverrideTimerFont = value,
                () => overrides.TimerFont,
                value => overrides.TimerFont = value,
                () => globalSettings.TimerFont));
        }

        if (usedFonts.HasFlag(GlobalFont.TimesFont))
        {
            rows.Children.Add(CreateFontOverrideRow(
                "OverrideTimesFont",
                "Override Times Font",
                () => overrides.OverrideTimesFont,
                value => overrides.OverrideTimesFont = value,
                () => overrides.TimesFont,
                value => overrides.TimesFont = value,
                () => globalSettings.TimesFont));
        }

        if (usedFonts.HasFlag(GlobalFont.TextFont))
        {
            rows.Children.Add(CreateFontOverrideRow(
                "OverrideTextFont",
                "Override Text Font",
                () => overrides.OverrideTextFont,
                value => overrides.OverrideTextFont = value,
                () => overrides.TextFont,
                value => overrides.TextFont = value,
                () => globalSettings.TextFont));
        }

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = DialogTheme.GroupBorderBrush,
            Padding = new Thickness(8),
            Child = rows,
        };
    }

    private static Control CreateFontOverrideRow(
        string key,
        string label,
        Func<bool> getEnabled,
        Action<bool> setEnabled,
        Func<FontDescriptor> getFont,
        Action<FontDescriptor> setFont,
        Func<FontDescriptor> getGlobalFont)
    {
        var fontLabel = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
        };
        var familyBox = new TextBox
        {
            Name = key + "Family",
            Width = 140,
        };
        var sizeBox = new TextBox
        {
            Name = key + "Size",
            Width = 54,
        };
        var styleBox = new ComboBox
        {
            Name = key + "Style",
            ItemsSource = Enum.GetValues(typeof(System.Drawing.FontStyle)),
            Width = 110,
        };
        var checkBox = new CheckBox
        {
            Content = label,
            IsChecked = getEnabled(),
            Width = 160,
            VerticalAlignment = VerticalAlignment.Center,
        };

        void PopulateFields(FontDescriptor font)
        {
            font ??= getGlobalFont();
            familyBox.Text = font?.FamilyName ?? string.Empty;
            sizeBox.Text = (font?.Size ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture);
            styleBox.SelectedItem = font?.Style ?? System.Drawing.FontStyle.Regular;
        }

        void CommitFields()
        {
            if (!getEnabled())
            {
                return;
            }

            FontDescriptor current = getFont() ?? getGlobalFont()?.Clone() ?? new FontDescriptor();
            string family = string.IsNullOrWhiteSpace(familyBox.Text)
                ? current.FamilyName
                : familyBox.Text;
            float size = current.Size;
            float.TryParse(
                sizeBox.Text,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out size);
            var style = (System.Drawing.FontStyle)(styleBox.SelectedItem ?? current.Style);
            setFont(new FontDescriptor(family, size, style, current.Unit));
            fontLabel.Text = SettingsHelper.FormatFont(getFont());
        }

        void Refresh()
        {
            bool enabled = getEnabled();
            familyBox.IsEnabled = enabled;
            sizeBox.IsEnabled = enabled;
            styleBox.IsEnabled = enabled;
            FontDescriptor font = enabled ? getFont() : null;
            FontDescriptor globalFont = getGlobalFont();
            fontLabel.Text = font != null
                ? SettingsHelper.FormatFont(font)
                : globalFont != null
                    ? $"Using global: {SettingsHelper.FormatFont(globalFont)}"
                    : "Using global";
            PopulateFields(font ?? globalFont);
        }

        checkBox.IsCheckedChanged += (_, _) =>
        {
            bool enabled = checkBox.IsChecked == true;
            setEnabled(enabled);
            if (enabled && getFont() == null)
            {
                setFont(getGlobalFont()?.Clone());
            }

            Refresh();
        };
        familyBox.TextChanged += (_, _) => CommitFields();
        sizeBox.TextChanged += (_, _) => CommitFields();
        styleBox.SelectionChanged += (_, _) => CommitFields();

        Refresh();

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("160,146,60,116,*"),
            Children =
            {
                checkBox,
                WithColumn(familyBox, 1),
                WithColumn(sizeBox, 2),
                WithColumn(styleBox, 3),
                WithColumn(fontLabel, 4),
            },
        };
    }

    private static T WithColumn<T>(T control, int column)
        where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }

    public async Task<bool> ShowDialogAsync(Window owner)
    {
        if (owner is not null)
        {
            await ShowDialog(owner);
        }
        else
        {
            Show();
        }

        return await _result.Task;
    }

    internal sealed class ComponentSnapshot
    {
        private readonly IComponent _component;
        private readonly string _settingsXml;
        private readonly LayoutComponent _layoutComponent;
        private readonly FontOverrides _fontOverrides;

        private ComponentSnapshot(ILayoutComponent layoutComponent)
        {
            _component = layoutComponent.Component;
            _settingsXml = CaptureSettings(_component);
            _layoutComponent = layoutComponent as LayoutComponent;
            _fontOverrides = _layoutComponent?.FontOverrides?.Clone() as FontOverrides;
        }

        public static ComponentSnapshot Capture(ILayoutComponent layoutComponent) => new(layoutComponent);

        public void Restore()
        {
            RestoreSettings(_component, _settingsXml);
            if (_layoutComponent != null && _fontOverrides != null)
            {
                _layoutComponent.FontOverrides = _fontOverrides.Clone() as FontOverrides;
            }
        }

        private static string CaptureSettings(IComponent component)
        {
            if (component == null)
            {
                return null;
            }

            var document = new XmlDocument();
            XmlNode settings = component.GetSettings(document);
            return settings?.OuterXml;
        }

        private static void RestoreSettings(IComponent component, string settingsXml)
        {
            if (component == null || string.IsNullOrEmpty(settingsXml))
            {
                return;
            }

            var document = new XmlDocument();
            document.LoadXml(settingsXml);
            component.SetSettings(document.DocumentElement);
        }
    }
}

internal sealed class LayoutSettingsDialogLayoutSpec
{
    public static LayoutSettingsDialogLayoutSpec Master { get; } = new();

    public IReadOnlyList<int> ColumnWidths { get; } = [-1, 81];
    public IReadOnlyList<int> RowHeights { get; } = [-1, 29];
    public IReadOnlyList<string> StructuralOrder { get; } = ["Tabs", "OK", "Cancel"];

    public int TabColumnSpan => 2;
    public int InitialClientWidth => 504;
    public int InitialClientHeight => 665;
    public int MinimumWindowWidth => 520;
    public int MinimumWindowHeight => 674;
    public int MaximumWindowWidth => 520;
    public int Padding => 7;
    public int ButtonWidth => 75;
    public int ButtonHeight => 23;
}
