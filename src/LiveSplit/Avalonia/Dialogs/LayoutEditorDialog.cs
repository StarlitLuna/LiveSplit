using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.UI.LayoutSavers;

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
    private readonly List<ILayoutComponent> _originalComponents;

    public LayoutEditorDialog(ILayout layout, LiveSplitState state)
    {
        Layout = layout;
        State = state;

        // Snapshot the layout's component list so Cancel can roll back the in-place mutations
        // performed by Add / Remove / Move Up / Move Down (those buttons edit
        // Layout.LayoutComponents directly to keep the running window's renderer in sync).
        _originalComponents = new List<ILayoutComponent>(layout.LayoutComponents);

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

        var sideBar = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 8, 8, 8),
            Children = { addBtn, removeBtn, upBtn, downBtn, settingsBtn, layoutSettingsBtn },
        };

        var ok = new Button { Content = "OK", Width = 80, IsDefault = true };
        ok.Click += (_, _) =>
        {
            Layout.HasChanged = true;
            PersistIfPossible();
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
        Layout.LayoutComponents.Clear();
        foreach (ILayoutComponent c in _originalComponents)
        {
            Layout.LayoutComponents.Add(c);
        }
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

    private void PersistIfPossible()
    {
        if (string.IsNullOrEmpty(Layout.FilePath))
        {
            return;
        }

        try
        {
            using FileStream stream = File.Open(Layout.FilePath, FileMode.Create, FileAccess.Write);
            new XMLLayoutSaver().Save(Layout, stream);
            Layout.HasChanged = false;
            State.Settings.AddToRecentLayouts(Layout.FilePath);
        }
        catch (Exception ex)
        {
            LiveSplit.Options.Log.Error(ex);
        }
    }
}
