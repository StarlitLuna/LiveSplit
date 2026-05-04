using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.UI;
using LiveSplit.UI.Components;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>
/// Lists the layout's components, reorders them up/down, removes, opens per-component
/// settings, and opens layout-level settings.
/// </summary>
public sealed class LayoutEditorDialog : Window
{
    public ILayout Layout { get; }
    public LiveSplitState State { get; }

    private readonly TaskCompletionSource<bool> _result = new();
    private readonly ListBox _list;
    private readonly LayoutSnapshot _snapshot;

    public LayoutEditorDialog(ILayout layout, LiveSplitState state)
    {
        Layout = layout;
        State = state;

        // The editor mutates the live layout so the running renderer stays in sync. Keep a
        // complete value snapshot so Cancel / X can put every mutable layout field back.
        _snapshot = LayoutSnapshot.Capture(layout);

        Title = "Layout Editor";
        Width = LayoutEditorDialogLayoutSpec.Master.InitialClientWidth;
        Height = LayoutEditorDialogLayoutSpec.Master.InitialClientHeight;
        MinWidth = LayoutEditorDialogLayoutSpec.Master.MinimumWindowWidth;
        MinHeight = LayoutEditorDialogLayoutSpec.Master.MinimumWindowHeight;
        DialogTheme.ApplyWindow(this);

        _list = new ListBox
        {
            ItemsSource = ComponentNames(),
            Margin = new Thickness(3, 10, 10, 10),
        };
        _list.DoubleTapped += async (_, _) => await EditSelectedSettings();

        var addBtn = CreateIconButton("+", "Add Component");
        addBtn.Margin = new Thickness(10, 10, 3, 3);
        addBtn.Click += (_, _) => ShowAddComponentMenu(addBtn);
        var removeBtn = CreateIconButton("-", "Remove Component");
        removeBtn.Margin = new Thickness(10, 3, 3, 3);
        removeBtn.Click += (_, _) => RemoveSelected();
        var upBtn = CreateIconButton("^", "Move Up");
        upBtn.Margin = new Thickness(10, 3, 3, 3);
        upBtn.Click += (_, _) => MoveSelected(-1);
        var downBtn = CreateIconButton("v", "Move Down");
        downBtn.Margin = new Thickness(10, 3, 3, 3);
        downBtn.Click += (_, _) => MoveSelected(1);
        var layoutSettingsBtn = CreateFooterButton("Layout Settings", width: 88);
        layoutSettingsBtn.Click += async (_, _) => await OpenLayoutSettings();
        var setSizeBtn = CreateFooterButton("Set Size");
        setSizeBtn.Click += async (_, _) => await SetSize();
        var horizontalRadio = new RadioButton
        {
            Content = "Horizontal",
            Margin = new Thickness(3, 3, 3, 10),
            VerticalAlignment = VerticalAlignment.Center,
            IsChecked = Layout.Mode == LayoutMode.Horizontal,
        };
        horizontalRadio.IsCheckedChanged += (_, _) =>
        {
            if (horizontalRadio.IsChecked == true)
            {
                SetOrientation(LayoutMode.Horizontal);
            }
        };
        var verticalRadio = new RadioButton
        {
            Content = "Vertical",
            Margin = new Thickness(3, 3, 3, 10),
            VerticalAlignment = VerticalAlignment.Center,
            IsChecked = Layout.Mode != LayoutMode.Horizontal,
        };
        verticalRadio.IsCheckedChanged += (_, _) =>
        {
            if (verticalRadio.IsChecked == true)
            {
                SetOrientation(LayoutMode.Vertical);
            }
        };

        var ok = CreateFooterButton("OK");
        ok.HorizontalAlignment = HorizontalAlignment.Right;
        ok.IsDefault = true;
        ok.Click += (_, _) =>
        {
            _snapshot.DisposeComponentsRemovedFrom(Layout);
            AcceptLayout(Layout);
            _result.TrySetResult(true);
            Close();
        };
        var cancel = CreateFooterButton("Cancel");
        cancel.IsCancel = true;
        cancel.Click += (_, _) =>
        {
            RestoreOriginalComponents();
            _result.TrySetResult(false);
            Close();
        };

        var root = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("48,94,81,78,74,*,88"),
            RowDefinitions = new RowDefinitions("48,41,41,41,41,*,36"),
        };
        Add(root, addBtn, 0, 0);
        Add(root, removeBtn, 0, 1);
        Add(root, upBtn, 0, 2);
        Add(root, downBtn, 0, 3);
        Add(root, _list, 1, 0, columnSpan: 6, rowSpan: 6);
        Add(root, layoutSettingsBtn, 1, 6);
        Add(root, setSizeBtn, 2, 6);
        Add(root, horizontalRadio, 3, 6);
        Add(root, verticalRadio, 4, 6);
        Add(root, ok, 5, 6);
        Add(root, cancel, 6, 6);
        Content = root;

        Closed += (_, _) =>
        {
            if (!_result.Task.IsCompleted)
            {
                // Closed via the X button without confirming — same rollback as Cancel.
                RestoreOriginalComponents();
                _result.TrySetResult(false);
            }
        };
    }

    private static void Add(Grid grid, Control control, int column, int row, int columnSpan = 1, int rowSpan = 1)
    {
        Grid.SetColumn(control, column);
        Grid.SetRow(control, row);
        if (columnSpan > 1)
        {
            Grid.SetColumnSpan(control, columnSpan);
        }

        if (rowSpan > 1)
        {
            Grid.SetRowSpan(control, rowSpan);
        }

        grid.Children.Add(control);
    }

    private void RestoreOriginalComponents()
    {
        _snapshot.RestoreAfterCancel(Layout);
        Refresh();
    }

    private List<string> ComponentNames()
    {
        var names = new List<string>();
        foreach (ILayoutComponent c in Layout.LayoutComponents)
        {
            names.Add(c.Component.ComponentName);
        }

        return names;
    }

    private void Refresh() => _list.ItemsSource = ComponentNames();

    private static Button CreateIconButton(string text, string tooltip)
    {
        var button = new Button
        {
            Content = text,
            Width = LayoutEditorDialogLayoutSpec.Master.IconButtonSize,
            Height = LayoutEditorDialogLayoutSpec.Master.IconButtonSize,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(button, tooltip);
        return button;
    }

    private static Button CreateFooterButton(string text, int width = 75)
        => new()
        {
            Content = text,
            Width = width,
            Height = LayoutEditorDialogLayoutSpec.Master.FooterButtonHeight,
            Margin = new Thickness(3, 3, 3, 10),
            VerticalAlignment = VerticalAlignment.Center,
        };

    private void RemoveSelected()
    {
        int idx = _list.SelectedIndex;
        if (!RemoveComponentAt(Layout, idx))
        {
            return;
        }

        Refresh();
    }

    internal static bool CanRemoveComponent(ILayout layout)
        => layout?.LayoutComponents?.Count > 1;

    internal static bool RemoveComponentAt(ILayout layout, int index)
    {
        if (layout == null
            || index < 0
            || index >= layout.LayoutComponents.Count
            || !CanRemoveComponent(layout))
        {
            return false;
        }

        IComponent component = layout.LayoutComponents[index].Component;
        if (component is IDeactivatableComponent deactivatable)
        {
            deactivatable.Activated = false;
        }

        layout.LayoutComponents.RemoveAt(index);
        layout.HasChanged = true;
        return true;
    }

    private void MoveSelected(int direction)
    {
        int idx = _list.SelectedIndex;
        int newIdx = idx + direction;
        if (idx < 0 || newIdx < 0 || newIdx >= Layout.LayoutComponents.Count)
        {
            return;
        }

        ILayoutComponent moved = Layout.LayoutComponents[idx];
        Layout.LayoutComponents.RemoveAt(idx);
        Layout.LayoutComponents.Insert(newIdx, moved);
        Layout.HasChanged = true;
        Refresh();
        _list.SelectedIndex = newIdx;
    }

    private void SetOrientation(LayoutMode mode)
    {
        if (Layout.Mode == mode)
        {
            return;
        }

        ApplyOrientationSwitch(Layout, mode, EstimateOverallSize(Layout, mode));
    }

    internal static void ApplyOrientationSwitch(ILayout layout, LayoutMode mode, float overallSize)
    {
        if (layout == null || layout.Mode == mode)
        {
            return;
        }

        int currentWidth = layout.Mode == LayoutMode.Vertical
            ? layout.VerticalWidth
            : layout.HorizontalWidth;
        int currentHeight = layout.Mode == LayoutMode.Vertical
            ? layout.VerticalHeight
            : layout.HorizontalHeight;

        if (mode == LayoutMode.Vertical)
        {
            layout.HorizontalWidth = currentWidth;
            layout.HorizontalHeight = currentHeight;
            if (layout.VerticalWidth == LiveSplit.UI.Layout.InvalidSize
                || layout.VerticalHeight == LiveSplit.UI.Layout.InvalidSize)
            {
                layout.VerticalWidth = 300;
                layout.VerticalHeight = RoundMaster(overallSize);
            }
        }
        else
        {
            layout.VerticalWidth = currentWidth;
            layout.VerticalHeight = currentHeight;
            if (layout.HorizontalWidth == LiveSplit.UI.Layout.InvalidSize
                || layout.HorizontalHeight == LiveSplit.UI.Layout.InvalidSize)
            {
                layout.HorizontalWidth = RoundMaster(overallSize);
                layout.HorizontalHeight = 45;
            }
        }

        layout.Mode = mode;
        layout.HasChanged = true;
    }

    private static int RoundMaster(float value)
        => (int)(value + 0.5f);

    private static float EstimateOverallSize(ILayout layout, LayoutMode mode)
    {
        if (layout?.LayoutComponents is null || layout.LayoutComponents.Count == 0)
        {
            return 1f;
        }

        float total = mode == LayoutMode.Vertical
            ? layout.LayoutComponents.Sum(x => x.Component.VerticalHeight)
            : layout.LayoutComponents.Sum(x => x.Component.HorizontalWidth);
        return Math.Max(total, 1f);
    }

    private async Task SetSize()
    {
        Window target = Owner as Window ?? CreateSetSizeFallbackWindow(Layout, Width, Height);
        var dlg = new SetSizeForm(target);
        if (await dlg.ShowDialogAsync(this))
        {
            ApplyCurrentModeSize(Layout, (int)Math.Round(target.Width), (int)Math.Round(target.Height));
        }
    }

    private static Window CreateSetSizeFallbackWindow(ILayout layout, double fallbackWidth, double fallbackHeight)
    {
        bool vertical = layout?.Mode != LayoutMode.Horizontal;
        var target = new Window
        {
            Width = vertical ? layout?.VerticalWidth ?? 0 : layout?.HorizontalWidth ?? 0,
            Height = vertical ? layout?.VerticalHeight ?? 0 : layout?.HorizontalHeight ?? 0,
        };

        if (target.Width <= 0)
        {
            target.Width = fallbackWidth;
        }

        if (target.Height <= 0)
        {
            target.Height = fallbackHeight;
        }

        return target;
    }

    internal static void AcceptLayout(ILayout layout)
    {
        // Master leaves dirty tracking to the individual edit operations. Pressing OK after
        // inspecting the layout does not make an otherwise clean layout prompt for saving.
    }

    internal static void ApplyCurrentModeSize(ILayout layout, int width, int height)
    {
        if (layout == null)
        {
            return;
        }

        if (layout.Mode == LayoutMode.Vertical)
        {
            layout.VerticalWidth = width;
            layout.VerticalHeight = height;
        }
        else
        {
            layout.HorizontalWidth = width;
            layout.HorizontalHeight = height;
        }

        layout.HasChanged = true;
    }

    private async Task OpenLayoutSettings(IComponent selectedComponent = null)
    {
        var dlg = new LayoutSettingsDialog(Layout.Settings, Layout, selectedComponent);
        if (await dlg.ShowDialogAsync(this))
        {
            Layout.HasChanged = true;
        }
    }

    /// <summary>
    /// Builds a category-grouped flyout from <see cref="ComponentManager.ComponentFactories"/>
    /// (mirroring the original WinForms LayoutEditor's tree). Autosplitter DLLs that register
    /// themselves through the same factory pipeline are filtered out so they don't appear under
    /// the generic component groups.
    /// </summary>
    private void ShowAddComponentMenu(Control anchor)
    {
        if (ComponentManager.ComponentFactories is null)
        {
            ShowEmptyAddMessage();
            return;
        }

        IDictionary<string, AutoSplitter> autoSplitters = AutoSplitterFactory.Instance?.AutoSplitters;
        var skipKeys = new HashSet<string>(
            autoSplitters?.Where(x => !x.Value.ShowInLayoutEditor).Select(x => x.Value.FileName)
            ?? [],
            StringComparer.Ordinal);

        var grouped = ComponentManager.ComponentFactories
            .Where(kv => kv.Value != null && !skipKeys.Contains(kv.Key))
            .GroupBy(kv => kv.Value.Category)
            .OrderBy(g => g.Key)
            .ToList();

        if (grouped.Count == 0)
        {
            ShowEmptyAddMessage();
            return;
        }

        var flyout = new MenuFlyout();
        foreach (IGrouping<ComponentCategory, KeyValuePair<string, IComponentFactory>> group in grouped)
        {
            var groupItem = new MenuItem { Header = group.Key.ToString() };

            IEnumerable<KeyValuePair<string, IComponentFactory>> entries = group;
            if (group.Key == ComponentCategory.Other)
            {
                // Mirror the WinForms editor: synthesize a Separator entry alongside the
                // genuine factories so users can still insert layout dividers.
                entries = entries
                    .Concat([new KeyValuePair<string, IComponentFactory>(string.Empty, new SeparatorFactory())])
                    .OrderBy(x => x.Value.ComponentName, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                entries = entries.OrderBy(x => x.Value.ComponentName, StringComparer.OrdinalIgnoreCase);
            }

            foreach (KeyValuePair<string, IComponentFactory> entry in entries)
            {
                string factoryKey = entry.Key;
                IComponentFactory factory = entry.Value;
                var leaf = new MenuItem
                {
                    Header = factory.ComponentName,
                };
                if (!string.IsNullOrEmpty(factory.Description))
                {
                    ToolTip.SetTip(leaf, factory.Description);
                }

                leaf.Click += (_, _) => AddFactory(factoryKey, factory);
                groupItem.Items.Add(leaf);
            }

            flyout.Items.Add(groupItem);
        }

        flyout.Items.Add(new Separator());
        var downloadMore = new MenuItem { Header = "Download More..." };
        downloadMore.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://livesplit.org/components/",
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                LiveSplit.Options.Log.Error(ex);
            }
        };
        flyout.Items.Add(downloadMore);

        flyout.ShowAt(anchor);
    }

    private async void ShowEmptyAddMessage()
    {
        var dlg = new MessageDialog("Add Component", "No components are available to add.");
        await dlg.ShowDialogAsync(this);
    }

    private void AddFactory(string factoryKey, IComponentFactory factory)
    {
        try
        {
            IComponent component = factory is SeparatorFactory
                ? new SeparatorComponent()
                : factory.Create(State);
            var layoutComponent = new LayoutComponent(factoryKey ?? string.Empty, component);
            Layout.LayoutComponents.Add(layoutComponent);
            Layout.HasChanged = true;
            Refresh();
            _list.SelectedIndex = Layout.LayoutComponents.Count - 1;
        }
        catch (Exception ex)
        {
            LiveSplit.Options.Log.Error(ex);
        }
    }

    private async Task EditSelectedSettings()
    {
        int idx = _list.SelectedIndex;
        if (idx < 0 || idx >= Layout.LayoutComponents.Count)
        {
            return;
        }

        IComponent comp = Layout.LayoutComponents[idx].Component;
        await OpenLayoutSettings(comp);
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

    internal sealed class LayoutSnapshot
    {
        private readonly List<ComponentSnapshot> _components;
        private readonly LayoutSettings _settings;

        private LayoutSnapshot(ILayout layout)
        {
            Mode = layout.Mode;
            VerticalWidth = layout.VerticalWidth;
            VerticalHeight = layout.VerticalHeight;
            HorizontalWidth = layout.HorizontalWidth;
            HorizontalHeight = layout.HorizontalHeight;
            X = layout.X;
            Y = layout.Y;
            HasChanged = layout.HasChanged;
            FilePath = layout.FilePath;
            _settings = layout.Settings?.Clone() as LayoutSettings;
            _components = layout.LayoutComponents.Select(ComponentSnapshot.Capture).ToList();
        }

        private LayoutMode Mode { get; }
        private int VerticalWidth { get; }
        private int VerticalHeight { get; }
        private int HorizontalWidth { get; }
        private int HorizontalHeight { get; }
        private int X { get; }
        private int Y { get; }
        private bool HasChanged { get; }
        private string FilePath { get; }

        public static LayoutSnapshot Capture(ILayout layout) => new(layout);

        public void Apply(ILayout layout)
        {
            layout.Mode = Mode;
            layout.VerticalWidth = VerticalWidth;
            layout.VerticalHeight = VerticalHeight;
            layout.HorizontalWidth = HorizontalWidth;
            layout.HorizontalHeight = HorizontalHeight;
            layout.X = X;
            layout.Y = Y;
            layout.HasChanged = HasChanged;
            layout.FilePath = FilePath;

            if (_settings == null)
            {
                layout.Settings = null;
            }
            else if (layout.Settings == null)
            {
                layout.Settings = _settings.Clone() as LayoutSettings;
            }
            else
            {
                layout.Settings.Assign(_settings);
            }

            layout.LayoutComponents.Clear();
            foreach (ComponentSnapshot component in _components)
            {
                layout.LayoutComponents.Add(component.Restore());
            }
        }

        public void RestoreAfterCancel(ILayout layout)
        {
            if (layout == null)
            {
                return;
            }

            HashSet<IComponent> originalComponents = OriginalComponentSet();
            HashSet<IComponent> currentComponents = CurrentComponentSet(layout);
            foreach (IComponent transient in currentComponents.Where(x => !originalComponents.Contains(x)))
            {
                transient.Dispose();
            }

            Apply(layout);

            foreach (IComponent restored in originalComponents.Where(x => !currentComponents.Contains(x)))
            {
                if (restored is IDeactivatableComponent deactivatable)
                {
                    deactivatable.Activated = true;
                }
            }
        }

        public void DisposeComponentsRemovedFrom(ILayout layout)
        {
            HashSet<IComponent> currentComponents = CurrentComponentSet(layout);
            foreach (IComponent removed in OriginalComponentSet().Where(x => !currentComponents.Contains(x)))
            {
                removed.Dispose();
            }
        }

        private HashSet<IComponent> OriginalComponentSet()
            => _components
                .Select(x => x.Component)
                .Where(x => x is not null)
                .ToHashSet();

        private static HashSet<IComponent> CurrentComponentSet(ILayout layout)
            => layout?.LayoutComponents
                ?.Select(x => x.Component)
                .Where(x => x is not null)
                .ToHashSet() ?? [];
    }

    private sealed class ComponentSnapshot
    {
        private readonly string _path;
        private readonly IComponent _component;
        private readonly FontOverrides _fontOverrides;
        private readonly string _settingsXml;

        private ComponentSnapshot(ILayoutComponent layoutComponent)
        {
            _path = layoutComponent.Path;
            _component = layoutComponent.Component;
            _fontOverrides = (layoutComponent as LayoutComponent)?.FontOverrides?.Clone() as FontOverrides;
            _settingsXml = CaptureSettings(layoutComponent.Component);
        }

        public static ComponentSnapshot Capture(ILayoutComponent layoutComponent) => new(layoutComponent);

        public IComponent Component => _component;

        public ILayoutComponent Restore()
        {
            RestoreSettings(_component, _settingsXml);
            return new LayoutComponent(_path, _component)
            {
                FontOverrides = _fontOverrides?.Clone() as FontOverrides ?? new FontOverrides(),
            };
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

internal sealed class LayoutEditorDialogLayoutSpec
{
    public static LayoutEditorDialogLayoutSpec Master { get; } = new();

    public IReadOnlyList<int> ColumnWidths { get; } = [48, 94, 81, 78, 74, -1, 88];
    public IReadOnlyList<int> RowHeights { get; } = [48, 41, 41, 41, 41, -1, 36];
    public IReadOnlyList<string> StructuralOrder { get; } =
    [
        "AddIconButton",
        "RemoveIconButton",
        "MoveUpIconButton",
        "MoveDownIconButton",
        "ComponentList",
        "LayoutSettingsButton",
        "SetSizeButton",
        "HorizontalRadio",
        "VerticalRadio",
        "OK",
        "Cancel",
    ];

    public int InitialClientWidth => 544;
    public int InitialClientHeight => 320;
    public int MinimumWindowWidth => 560;
    public int MinimumWindowHeight => 306;
    public int IconButtonSize => 35;
    public int FooterButtonWidth => 75;
    public int FooterButtonHeight => 23;
}
