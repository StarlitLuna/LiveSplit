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
        Width = 600;
        Height = 540;

        _list = new ListBox { ItemsSource = ComponentNames(), Margin = new Thickness(8) };

        var addBtn = new Button { Content = "Add…", Margin = new Thickness(4) };
        addBtn.Click += (_, _) => ShowAddComponentMenu(addBtn);
        var removeBtn = new Button { Content = "Remove", Margin = new Thickness(4) };
        removeBtn.Click += (_, _) => RemoveSelected();
        var upBtn = new Button { Content = "Move Up", Margin = new Thickness(4) };
        upBtn.Click += (_, _) => MoveSelected(-1);
        var downBtn = new Button { Content = "Move Down", Margin = new Thickness(4) };
        downBtn.Click += (_, _) => MoveSelected(1);
        var settingsBtn = new Button { Content = "Settings…", Margin = new Thickness(4) };
        settingsBtn.Click += async (_, _) => await EditSelectedSettings();
        var layoutSettingsBtn = new Button { Content = "Layout Settings…", Margin = new Thickness(4) };
        layoutSettingsBtn.Click += async (_, _) =>
        {
            var dlg = new LayoutSettingsDialog(layout.Settings);
            await dlg.ShowDialogAsync(this);
        };
        var verticalBtn = new Button { Content = "Vertical", Margin = new Thickness(4) };
        verticalBtn.Click += (_, _) => SetOrientation(LayoutMode.Vertical);
        var horizontalBtn = new Button { Content = "Horizontal", Margin = new Thickness(4) };
        horizontalBtn.Click += (_, _) => SetOrientation(LayoutMode.Horizontal);
        var setSizeBtn = new Button { Content = "Set Sizeâ€¦", Margin = new Thickness(4) };
        setSizeBtn.Click += async (_, _) => await SetSize();

        var sideBar = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 8, 8, 8),
            Children =
            {
                addBtn,
                removeBtn,
                upBtn,
                downBtn,
                settingsBtn,
                layoutSettingsBtn,
                verticalBtn,
                horizontalBtn,
                setSizeBtn,
            },
        };

        var ok = new Button { Content = "OK", Width = 80, IsDefault = true };
        ok.Click += (_, _) =>
        {
            AcceptLayout(Layout);
            _result.TrySetResult(true);
            Close();
        };
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        cancel.Click += (_, _) =>
        {
            RestoreOriginalComponents();
            _result.TrySetResult(false);
            Close();
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 0, 12, 12),
            Children = { cancel, ok },
        };

        var center = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };
        Grid.SetColumn(_list, 0);
        Grid.SetColumn(sideBar, 1);
        center.Children.Add(_list);
        center.Children.Add(sideBar);

        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(center);
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

    private void RestoreOriginalComponents()
    {
        _snapshot.Apply(Layout);
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

    private void RemoveSelected()
    {
        int idx = _list.SelectedIndex;
        if (idx < 0 || idx >= Layout.LayoutComponents.Count)
        {
            return;
        }

        Layout.LayoutComponents.RemoveAt(idx);
        Refresh();
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
        Refresh();
        _list.SelectedIndex = newIdx;
    }

    private void SetOrientation(LayoutMode mode)
    {
        if (Layout.Mode == mode)
        {
            return;
        }

        Layout.Mode = mode;
        Layout.HasChanged = true;
    }

    private async Task SetSize()
    {
        bool vertical = Layout.Mode == LayoutMode.Vertical;
        var target = new Window
        {
            Width = vertical ? Layout.VerticalWidth : Layout.HorizontalWidth,
            Height = vertical ? Layout.VerticalHeight : Layout.HorizontalHeight,
        };

        if (target.Width <= 0)
        {
            target.Width = Width;
        }

        if (target.Height <= 0)
        {
            target.Height = Height;
        }

        var dlg = new SetSizeForm(target);
        if (await dlg.ShowDialogAsync(this))
        {
            ApplyCurrentModeSize(Layout, (int)Math.Round(target.Width), (int)Math.Round(target.Height));
        }
    }

    internal static void AcceptLayout(ILayout layout)
    {
        if (layout != null)
        {
            layout.HasChanged = true;
        }
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
        var downloadMore = new MenuItem { Header = "Download More…" };
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
        var dlg = new ComponentSettingsDialog(comp);
        await dlg.ShowDialogAsync(this);
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
