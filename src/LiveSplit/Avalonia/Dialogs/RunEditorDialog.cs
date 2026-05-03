using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform.Storage;

using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.TimeFormatters;
using LiveSplit.UI;
using LiveSplit.UI.Components;

using SkiaSharp;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>
/// Tabbed Avalonia editor for an <see cref="IRun"/>. Mirrors the surface of the original
/// WinForms RunEditorDialog for local split, history, comparison, and metadata editing. Tabs:
///   • Edit       — segment list (add/remove/rename/reorder/double-click rename)
///   • Real Time  — split + segment + best-segment + custom-comparison times for RealTime
///   • Game Time  — same grid for GameTime
///   • History    — attempt log; remove single attempt or clear all
///   • Variables  — speedrun.com (read-only) + user custom variables
///
/// Edits run against a clone of the input run; <see cref="OkClicked"/> swaps it back into
/// <see cref="LiveSplitState.Run"/> on Apply, Cancel discards.
/// </summary>
public sealed class RunEditorDialog : Window
{
    public LiveSplitState State { get; }
    public IRun Run { get; private set; }

    private readonly IRun _original;
    private readonly TaskCompletionSource<bool> _result = new();
    private static readonly RegularTimeFormatter TimeFormatter = new(TimeAccuracy.Hundredths);
    private readonly List<string> _originalActiveAutoSplitters;
    private readonly System.Xml.XmlElement _originalAutoSplitterSettings;
    private bool _accepted;

    private readonly TextBox _gameBox;
    private readonly TextBox _categoryBox;
    private readonly TextBox _offsetBox;
    private readonly NumericUpDown _attemptBox;
    private readonly Image _iconImage;
    private readonly TextBlock _iconHint;
    private readonly CheckBox _linkedLayoutCheckBox;
    private readonly TextBox _layoutPathBox;
    private readonly TextBox _platformBox;
    private readonly TextBox _regionBox;
    private readonly CheckBox _emulatorCheckBox;

    private readonly ListBox _segmentsList;
    private readonly StackPanel _realTimeRows;
    private readonly StackPanel _gameTimeRows;
    private readonly DataGrid _historyGrid;
    private readonly StackPanel _variablesPanel;

    private TextBlock _autoSplitterDescription;
    private Button _autoSplitterActivateBtn;
    private Button _autoSplitterSettingsBtn;
    private Button _autoSplitterWebsiteBtn;

    private ObservableCollection<AttemptRow> _attemptRows;

    public RunEditorDialog(LiveSplitState state)
    {
        State = state;
        _original = state.Run;
        Run = (state.Run is Run r) ? r.Clone() : state.Run;
        _originalActiveAutoSplitters = State.Settings.ActiveAutoSplitters.ToList();
        _originalAutoSplitterSettings = _original.AutoSplitterSettings;

        Title = "Edit Splits";
        Width = 880;
        Height = 640;
        CanResize = true;

        // Header (game icon + name/category/offset/attempt fields)
        _iconImage = new Image
        {
            Width = 64,
            Height = 64,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _iconHint = new TextBlock
        {
            Text = "Click icon to set",
            FontSize = 10,
            Foreground = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var iconPanel = new StackPanel
        {
            Spacing = 4,
            Width = 80,
            Children = { _iconImage, _iconHint },
        };
        var iconBorder = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
            Margin = new Thickness(20, 20, 12, 8),
            Child = iconPanel,
            Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Hand),
        };
        iconBorder.PointerReleased += async (_, _) => await PickIcon();
        var iconContextMenu = new ContextMenu();
        var removeItem = new MenuItem { Header = "Remove Icon" };
        removeItem.Click += (_, _) => { Run.GameIconPng = null; Run.GameIcon = null; UpdateIconPreview(); Run.HasChanged = true; };
        iconContextMenu.Items.Add(removeItem);
        iconBorder.ContextMenu = iconContextMenu;

        _gameBox = new TextBox { Text = Run.GameName ?? "" };
        _categoryBox = new TextBox { Text = Run.CategoryName ?? "" };
        _offsetBox = new TextBox { Text = TimeFormatter.Format(Run.Offset) };
        _linkedLayoutCheckBox = new CheckBox { Content = "Use linked layout", IsChecked = !string.IsNullOrEmpty(Run.LayoutPath) };
        _layoutPathBox = new TextBox { Text = RunEditorDialogModel.DisplayLayoutPath(Run.LayoutPath), Width = 360 };
        _linkedLayoutCheckBox.IsCheckedChanged += (_, _) => UpdateLayoutControls();
        UpdateLayoutControls();
        _platformBox = new TextBox { Text = Run.Metadata.PlatformName ?? string.Empty, Width = 180 };
        _regionBox = new TextBox { Text = Run.Metadata.RegionName ?? string.Empty, Width = 180 };
        _emulatorCheckBox = new CheckBox { Content = "Uses Emulator", IsChecked = Run.Metadata.UsesEmulator };
        _attemptBox = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 999999,
            Value = Run.AttemptCount,
            Width = 120,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var headerFields = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(0, 20, 20, 8),
            Children =
            {
                LabeledRow("Game:", _gameBox),
                LabeledRow("Category:", _categoryBox),
                LabeledRow("Offset:", _offsetBox),
                LabeledRow("Attempt Count:", _attemptBox),
                BuildLayoutRow(),
            },
        };

        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
        };
        Grid.SetColumn(iconBorder, 0);
        Grid.SetColumn(headerFields, 1);
        headerGrid.Children.Add(iconBorder);
        headerGrid.Children.Add(headerFields);

        // Tabs
        _segmentsList = BuildSegmentsList();
        _realTimeRows = new StackPanel { Spacing = 2 };
        _gameTimeRows = new StackPanel { Spacing = 2 };
        _historyGrid = BuildHistoryGrid();
        _variablesPanel = new StackPanel { Spacing = 8, Margin = new Thickness(20) };

        var tabs = new TabControl
        {
            Margin = new Thickness(20, 0, 20, 8),
            Items =
            {
                new TabItem { Header = "Edit", Content = BuildEditTab() },
                new TabItem { Header = "Real Time", Content = BuildTimingTab(TimingMethod.RealTime, _realTimeRows) },
                new TabItem { Header = "Game Time", Content = BuildTimingTab(TimingMethod.GameTime, _gameTimeRows) },
                new TabItem { Header = "History", Content = BuildHistoryTab() },
                new TabItem { Header = "Variables", Content = BuildVariablesTab() },
                new TabItem { Header = "Auto Splitter", Content = BuildAutoSplitterTab() },
            },
        };

        // Game-name → autosplitter lookup mirrors the original WinForms cbxGameName_TextChanged:
        // create the matching AutoSplitter from AutoSplitterFactory and auto-activate when the
        // game appears in Settings.ActiveAutoSplitters.
        _gameBox.TextChanged += (_, _) => RefreshAutoSplitterFromGameName();
        RefreshAutoSplitterFromGameName();

        // Footer
        var ok = new Button { Content = "OK", Width = 80, IsDefault = true };
        ok.Click += (_, _) => OkClicked();
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        cancel.Click += (_, _) => CancelAndClose();

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 0, 20, 16),
            Children = { cancel, ok },
        };

        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(headerGrid, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(headerGrid);
        root.Children.Add(footer);
        root.Children.Add(tabs);
        Content = root;

        Closed += (_, _) =>
        {
            if (!_result.Task.IsCompleted)
            {
                RestoreAutoSplitterChanges();
                _result.TrySetResult(false);
            }
        };

        UpdateIconPreview();
        RebuildTimingRows();
        RebuildHistoryRows();
        RebuildVariablesPanel();
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

    // ------------------------------------------------------------------------
    // Footer apply
    // ------------------------------------------------------------------------

    private void OkClicked()
    {
        // Apply header edits to the clone.
        Run.GameName = _gameBox.Text ?? string.Empty;
        Run.CategoryName = _categoryBox.Text ?? string.Empty;

        try
        {
            Run.Offset = TimeSpanParser.Parse(string.IsNullOrWhiteSpace(_offsetBox.Text) ? "0" : _offsetBox.Text);
        }
        catch
        {
            // Keep previous offset if the text is unparseable rather than clobbering with 0.
        }

        if (_attemptBox.Value.HasValue)
        {
            Run.AttemptCount = (int)_attemptBox.Value.Value;
        }

        RunEditorDialogModel.SetLinkedLayout(
            Run,
            _linkedLayoutCheckBox.IsChecked == true,
            _layoutPathBox.Text);
        if (Run is Run editedRunForMetadata)
        {
            RunEditorDialogModel.SetAdditionalInfo(
                editedRunForMetadata,
                _platformBox.Text,
                _regionBox.Text,
                _emulatorCheckBox.IsChecked == true);
        }

        // Promote the working clone back onto State.Run; downstream state listeners pick it
        // up via WireStateEvents in the Avalonia host.
        if (_original is Run originalRun && Run is Run editedRun)
        {
            RunEditorDialogModel.ApplyAcceptedRun(originalRun, editedRun);
        }

        _accepted = true;
        _result.TrySetResult(true);
        Close();
    }

    private void CancelAndClose()
    {
        RestoreAutoSplitterChanges();
        _result.TrySetResult(false);
        Close();
    }

    private void RestoreAutoSplitterChanges()
    {
        if (_accepted)
        {
            return;
        }

        Run.AutoSplitter?.Deactivate();
        State.Settings.ActiveAutoSplitters.Clear();
        foreach (string game in _originalActiveAutoSplitters)
        {
            State.Settings.ActiveAutoSplitters.Add(game);
        }

        _original.AutoSplitterSettings = _originalAutoSplitterSettings;
    }

    private Control BuildLayoutRow()
    {
        var browseBtn = new Button { Content = "Browse...", Width = 86 };
        browseBtn.Click += async (_, _) => await PickLayoutPath();
        var defaultBtn = new Button { Content = "Default", Width = 76 };
        defaultBtn.Click += (_, _) =>
        {
            _linkedLayoutCheckBox.IsChecked = true;
            _layoutPathBox.Text = string.Empty;
            UpdateLayoutControls();
        };

        var pathPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { _layoutPathBox, browseBtn, defaultBtn },
        };

        return new StackPanel
        {
            Spacing = 4,
            Children = { _linkedLayoutCheckBox, pathPanel },
        };
    }

    private void UpdateLayoutControls()
    {
        if (_layoutPathBox is not null)
        {
            _layoutPathBox.IsEnabled = _linkedLayoutCheckBox?.IsChecked == true;
        }
    }

    private async Task PickLayoutPath()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Layout",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("LiveSplit Layout") { Patterns = new[] { "*.lsl" } },
                FilePickerFileTypes.All,
            },
        });

        if (files?.FirstOrDefault() is { } picked)
        {
            _linkedLayoutCheckBox.IsChecked = true;
            _layoutPathBox.Text = picked.TryGetLocalPath() ?? picked.Path.LocalPath;
            UpdateLayoutControls();
        }
    }

    // ------------------------------------------------------------------------
    // Auto Splitter tab — activate/deactivate + per-component settings
    // ------------------------------------------------------------------------

    private Control BuildAutoSplitterTab()
    {
        _autoSplitterDescription = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
        };

        _autoSplitterActivateBtn = new Button { Content = "Activate", Width = 120 };
        _autoSplitterActivateBtn.Click += (_, _) => ToggleAutoSplitterActivation();

        _autoSplitterSettingsBtn = new Button { Content = "Settings…", Width = 120 };
        _autoSplitterSettingsBtn.Click += async (_, _) => await OpenAutoSplitterSettings();

        _autoSplitterWebsiteBtn = new Button { Content = "Website", Width = 120 };
        _autoSplitterWebsiteBtn.Click += (_, _) => OpenAutoSplitterWebsite();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _autoSplitterActivateBtn, _autoSplitterSettingsBtn, _autoSplitterWebsiteBtn },
        };

        var panel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 8,
            Children = { _autoSplitterDescription, buttons },
        };

        RefreshAutoSplitterUI();
        return panel;
    }

    /// <summary>
    /// Re-resolve <see cref="IRun.AutoSplitter"/> from the current Game Name. Mirrors the
    /// original cbxGameName_TextChanged handler: the lookup runs on every text change, the
    /// previous splitter is deactivated, and the new one auto-activates iff the game name is
    /// already in <c>ISettings.ActiveAutoSplitters</c>.
    /// </summary>
    private void RefreshAutoSplitterFromGameName()
    {
        if (Run.AutoSplitter is { IsActivated: true } previous)
        {
            previous.Deactivate();
        }

        string gameName = _gameBox?.Text ?? string.Empty;
        AutoSplitter splitter = null;
        try
        {
            splitter = AutoSplitterFactory.Instance?.Create(gameName);
        }
        catch (Exception ex)
        {
            LiveSplit.Options.Log.Error(ex);
        }

        Run.AutoSplitter = splitter;
        if (splitter != null && State.Settings.ActiveAutoSplitters.Contains(gameName))
        {
            splitter.Activate(State);
            ApplyPersistedAutoSplitterSettings(gameName);
        }

        RefreshAutoSplitterUI();
    }

    private void RefreshAutoSplitterUI()
    {
        if (_autoSplitterDescription is null)
        {
            return;
        }

        AutoSplitter splitter = Run.AutoSplitter;
        if (splitter is null)
        {
            _autoSplitterDescription.Text = "There is no Auto Splitter available for this game.";
            _autoSplitterActivateBtn.Content = "Activate";
            _autoSplitterActivateBtn.IsEnabled = false;
            _autoSplitterSettingsBtn.IsEnabled = false;
            _autoSplitterWebsiteBtn.IsVisible = false;
            return;
        }

        _autoSplitterDescription.Text = string.IsNullOrEmpty(splitter.Description)
            ? "Auto Splitter available."
            : splitter.Description;
        _autoSplitterActivateBtn.IsEnabled = true;
        _autoSplitterActivateBtn.Content = splitter.IsActivated ? "Deactivate" : "Activate";
        _autoSplitterSettingsBtn.IsEnabled = splitter.IsActivated && SplitterHasSettings(splitter);
        _autoSplitterWebsiteBtn.IsVisible = !string.IsNullOrEmpty(splitter.Website);
    }

    private static bool SplitterHasSettings(AutoSplitter splitter)
    {
        try
        {
            return splitter.Component?.GetSettingsControl(LayoutMode.Vertical) != null;
        }
        catch
        {
            return false;
        }
    }

    private void ToggleAutoSplitterActivation()
    {
        AutoSplitter splitter = Run.AutoSplitter;
        if (splitter is null)
        {
            return;
        }

        string gameName = _gameBox?.Text ?? string.Empty;
        if (splitter.IsActivated)
        {
            State.Settings.ActiveAutoSplitters.Remove(gameName);
            splitter.Deactivate();
        }
        else
        {
            State.Settings.ActiveAutoSplitters.Add(gameName);
            splitter.Activate(State);
            ApplyPersistedAutoSplitterSettings(gameName);
        }

        RefreshAutoSplitterUI();
    }

    private void ApplyPersistedAutoSplitterSettings(string gameName)
    {
        AutoSplitter splitter = Run.AutoSplitter;
        if (splitter is not { IsActivated: true } || Run.AutoSplitterSettings is null)
        {
            return;
        }

        string persistedGame = Run.AutoSplitterSettings.GetAttribute("gameName");
        if (!string.Equals(persistedGame, gameName, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            splitter.Component?.SetSettings(Run.AutoSplitterSettings);
        }
        catch (Exception ex)
        {
            LiveSplit.Options.Log.Error(ex);
        }
    }

    private async Task OpenAutoSplitterSettings()
    {
        AutoSplitter splitter = Run.AutoSplitter;
        if (splitter?.Component is not IComponent component)
        {
            return;
        }

        var dlg = new ComponentSettingsDialog(component);
        if (await dlg.ShowDialogAsync(this))
        {
            try
            {
                var doc = new System.Xml.XmlDocument();
                System.Xml.XmlElement element = doc.CreateElement("AutoSplitterSettings");
                element.InnerXml = component.GetSettings(doc).InnerXml;
                System.Xml.XmlAttribute gameAttr = doc.CreateAttribute("gameName");
                gameAttr.Value = _gameBox?.Text ?? string.Empty;
                element.Attributes.Append(gameAttr);
                Run.AutoSplitterSettings = element;
                Run.HasChanged = true;
            }
            catch (Exception ex)
            {
                LiveSplit.Options.Log.Error(ex);
            }
        }
    }

    private void OpenAutoSplitterWebsite()
    {
        string url = Run.AutoSplitter?.Website;
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            LiveSplit.Options.Log.Error(ex);
        }
    }

    // ------------------------------------------------------------------------
    // Edit tab — segment list with add/remove/rename/reorder
    // ------------------------------------------------------------------------

    private ListBox BuildSegmentsList()
    {
        var list = new ListBox { Margin = new Thickness(0, 0, 8, 0) };
        list.ItemsSource = SegmentNames();
        list.DoubleTapped += async (_, _) => await RenameSegment();
        return list;
    }

    private Control BuildEditTab()
    {
        var addBtn = new Button { Content = "Add Segment" };
        addBtn.Click += async (_, _) => await AddSegment();
        var removeBtn = new Button { Content = "Remove Segment" };
        removeBtn.Click += (_, _) => RemoveSegment();
        var renameBtn = new Button { Content = "Rename Segment" };
        renameBtn.Click += async (_, _) => await RenameSegment();
        var upBtn = new Button { Content = "Move Up" };
        upBtn.Click += (_, _) => MoveSegment(-1);
        var downBtn = new Button { Content = "Move Down" };
        downBtn.Click += (_, _) => MoveSegment(1);

        var sideBar = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(8, 0, 0, 0),
            Children = { addBtn, removeBtn, renameBtn, upBtn, downBtn },
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(8) };
        Grid.SetColumn(_segmentsList, 0);
        Grid.SetColumn(sideBar, 1);
        grid.Children.Add(_segmentsList);
        grid.Children.Add(sideBar);
        return grid;
    }

    private List<string> SegmentNames()
    {
        var names = new List<string>(Run.Count);
        for (int i = 0; i < Run.Count; i++)
        {
            names.Add(Run[i].Name);
        }

        return names;
    }

    private void RefreshSegmentList()
    {
        _segmentsList.ItemsSource = SegmentNames();
        RebuildTimingRows();
    }

    private async Task AddSegment()
    {
        var prompt = new TextInputDialog("New Segment", "Segment name:");
        if (await prompt.ShowDialogAsync(this) is { } name && !string.IsNullOrWhiteSpace(name))
        {
            int idx = _segmentsList.SelectedIndex < 0 ? Run.Count : _segmentsList.SelectedIndex + 1;
            RunEditorDialogModel.InsertSegment(Run, idx, name);
            RefreshSegmentList();
            _segmentsList.SelectedIndex = idx;
        }
    }

    private void RemoveSegment()
    {
        int idx = _segmentsList.SelectedIndex;
        if (idx < 0 || idx >= Run.Count || Run.Count <= 1)
        {
            return;
        }

        RunEditorDialogModel.RemoveSegment(Run, idx);
        RefreshSegmentList();
    }

    private async Task RenameSegment()
    {
        int idx = _segmentsList.SelectedIndex;
        if (idx < 0 || idx >= Run.Count)
        {
            return;
        }

        var prompt = new TextInputDialog("Rename Segment", "New name:", Run[idx].Name);
        if (await prompt.ShowDialogAsync(this) is { } name && !string.IsNullOrWhiteSpace(name))
        {
            Run[idx].Name = name;
            RefreshSegmentList();
            _segmentsList.SelectedIndex = idx;
        }
    }

    private void MoveSegment(int dir)
    {
        int idx = _segmentsList.SelectedIndex;
        int newIdx = idx + dir;
        if (idx < 0 || newIdx < 0 || newIdx >= Run.Count)
        {
            return;
        }

        RunEditorDialogModel.MoveSegment(Run, idx, newIdx);
        RefreshSegmentList();
        _segmentsList.SelectedIndex = newIdx;
    }

    // ------------------------------------------------------------------------
    // Real Time / Game Time tabs — per-segment time matrix
    // ------------------------------------------------------------------------

    private Control BuildTimingTab(TimingMethod method, StackPanel rowsPanel)
    {
        var addCmpBtn = new Button { Content = "Add Comparison…" };
        addCmpBtn.Click += async (_, _) => await AddCustomComparison();

        var customComparisonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0),
            Children = { addCmpBtn },
        };

        var rowsScroll = new ScrollViewer
        {
            Content = rowsPanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        var stack = new DockPanel { LastChildFill = true, Margin = new Thickness(8) };
        DockPanel.SetDock(customComparisonsPanel, Dock.Top);
        stack.Children.Add(customComparisonsPanel);
        stack.Children.Add(rowsScroll);
        return stack;
    }

    private void RebuildTimingRows()
    {
        BuildTimingRowsFor(TimingMethod.RealTime, _realTimeRows);
        BuildTimingRowsFor(TimingMethod.GameTime, _gameTimeRows);
    }

    private void BuildTimingRowsFor(TimingMethod method, StackPanel target)
    {
        if (target is null)
        {
            return;
        }

        target.Children.Clear();
        List<string> comparisons = Run.CustomComparisons.ToList();

        // Header
        var header = MakeTimingRowGrid(comparisons.Count);
        header.Children.Add(MakeHeaderText("Segment", 0));
        header.Children.Add(MakeHeaderText("Split Time", 1));
        header.Children.Add(MakeHeaderText("Segment Time", 2));
        header.Children.Add(MakeHeaderText("Best Segment", 3));
        for (int c = 0; c < comparisons.Count; c++)
        {
            header.Children.Add(MakeHeaderText(comparisons[c], 4 + c));
        }

        target.Children.Add(header);

        for (int i = 0; i < Run.Count; i++)
        {
            int segIndex = i;
            ISegment segment = Run[i];

            Grid row = MakeTimingRowGrid(comparisons.Count);

            var segLabel = new TextBlock { Text = segment.Name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4) };
            Grid.SetColumn(segLabel, 0);
            row.Children.Add(segLabel);

            var splitBox = new TextBox { Text = TimeFormatter.Format(segment.PersonalBestSplitTime[method]) };
            Grid.SetColumn(splitBox, 1);
            splitBox.LostFocus += (_, _) =>
                RunEditorDialogModel.SetPersonalBestSplitTime(Run, segIndex, method, TimeSpanParser.ParseNullable(splitBox.Text));
            row.Children.Add(splitBox);

            var segmentBox = new TextBox { Text = TimeFormatter.Format(GetPersonalBestSegmentTime(segIndex, method)) };
            Grid.SetColumn(segmentBox, 2);
            segmentBox.LostFocus += (_, _) =>
                RunEditorDialogModel.SetPersonalBestSegmentTime(Run, segIndex, method, TimeSpanParser.ParseNullable(segmentBox.Text));
            row.Children.Add(segmentBox);

            var bestBox = new TextBox { Text = TimeFormatter.Format(segment.BestSegmentTime[method]) };
            Grid.SetColumn(bestBox, 3);
            bestBox.LostFocus += (_, _) =>
            {
                Time t = segment.BestSegmentTime;
                t[method] = TimeSpanParser.ParseNullable(bestBox.Text);
                segment.BestSegmentTime = t;
                Run.HasChanged = true;
            };
            row.Children.Add(bestBox);

            for (int c = 0; c < comparisons.Count; c++)
            {
                string cmp = comparisons[c];
                int colIndex = c;
                var box = new TextBox { Text = TimeFormatter.Format(segment.Comparisons[cmp][method]) };
                Grid.SetColumn(box, 4 + c);

                box.LostFocus += (_, _) =>
                {
                    try
                    {
                        Time t = segment.Comparisons[cmp];
                        t[method] = TimeSpanParser.ParseNullable(box.Text);
                        segment.Comparisons[cmp] = t;
                        Run.HasChanged = true;
                    }
                    catch (Exception ex)
                    {
                        LiveSplit.Options.Log.Error(ex);
                    }
                };

                row.Children.Add(box);
            }

            target.Children.Add(row);
        }
    }

    private static Grid MakeTimingRowGrid(int comparisonCount)
    {
        // Fixed columns (Segment, Split, Segment Time, Best Segment) + N comparison columns.
        var defs = new ColumnDefinitions("180,140,140,140");
        for (int i = 0; i < comparisonCount; i++)
        {
            defs.Add(new ColumnDefinition(140, GridUnitType.Pixel));
        }

        return new Grid { ColumnDefinitions = defs, Margin = new Thickness(2) };
    }

    private static TextBlock MakeHeaderText(string text, int col)
    {
        var t = new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(4),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(t, col);
        return t;
    }

    private async Task AddCustomComparison()
    {
        var prompt = new TextInputDialog("New Comparison", "Comparison name:");
        string name = await prompt.ShowDialogAsync(this);
        if (!RunEditorDialogModel.TryAddComparison(Run, name))
        {
            return;
        }
        RebuildTimingRows();
    }

    // ------------------------------------------------------------------------
    // History tab
    // ------------------------------------------------------------------------

    private DataGrid BuildHistoryGrid()
    {
        var grid = new DataGrid
        {
            IsReadOnly = true,
            CanUserSortColumns = true,
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "#", Binding = new global::Avalonia.Data.Binding(nameof(AttemptRow.Index)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Started", Binding = new global::Avalonia.Data.Binding(nameof(AttemptRow.Started)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Ended", Binding = new global::Avalonia.Data.Binding(nameof(AttemptRow.Ended)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Real Time", Binding = new global::Avalonia.Data.Binding(nameof(AttemptRow.RealTime)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Game Time", Binding = new global::Avalonia.Data.Binding(nameof(AttemptRow.GameTime)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Pause", Binding = new global::Avalonia.Data.Binding(nameof(AttemptRow.PauseTime)) });

        return grid;
    }

    private Control BuildHistoryTab()
    {
        var removeBtn = new Button { Content = "Remove Selected" };
        removeBtn.Click += (_, _) => RemoveSelectedAttempt();
        var clearBtn = new Button { Content = "Clear All" };
        clearBtn.Click += (_, _) => ClearHistory();
        var clearTimesBtn = new Button { Content = "Clear Times" };
        clearTimesBtn.Click += (_, _) => ClearTimes();
        var cleanSobBtn = new Button { Content = "Clean Sum of Best" };
        cleanSobBtn.Click += async (_, _) => await CleanSumOfBest();

        var topBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 8),
            Children = { removeBtn, clearBtn, clearTimesBtn, cleanSobBtn },
        };

        var dock = new DockPanel { LastChildFill = true, Margin = new Thickness(8) };
        DockPanel.SetDock(topBar, Dock.Top);
        dock.Children.Add(topBar);
        dock.Children.Add(_historyGrid);
        return dock;
    }

    private void RebuildHistoryRows()
    {
        _attemptRows = new ObservableCollection<AttemptRow>(
            Run.AttemptHistory.Select(a => new AttemptRow(a)));
        _historyGrid.ItemsSource = _attemptRows;
    }

    private void RemoveSelectedAttempt()
    {
        if (_historyGrid.SelectedItem is not AttemptRow row)
        {
            return;
        }

        int idx = row.Index;
        Attempt match = Run.AttemptHistory.FirstOrDefault(a => a.Index == idx);
        if (Run.AttemptHistory.Contains(match))
        {
            Run.AttemptHistory.Remove(match);
        }

        foreach (ISegment seg in Run)
        {
            seg.SegmentHistory.Remove(idx);
        }

        Run.HasChanged = true;
        RebuildHistoryRows();
    }

    private void ClearHistory()
    {
        Run.ClearHistory();
        Run.HasChanged = true;
        RebuildHistoryRows();
    }

    private void ClearTimes()
    {
        RunEditorDialogModel.ClearTimes(Run);
        _attemptBox.Value = Run.AttemptCount;
        RebuildHistoryRows();
        RebuildTimingRows();
    }

    private async Task CleanSumOfBest()
    {
        var dialog = new MessageDialog(
            "Clean Sum of Best",
            "Remove potentially invalid segment history elements from the Sum of Best?",
            MessageDialog.Buttons.YesNo);
        if (await dialog.ShowDialogResultAsync(this) != MessageResult.Yes)
        {
            return;
        }

        RunEditorDialogModel.CleanSumOfBest(Run, _ => true);
        RebuildHistoryRows();
        RebuildTimingRows();
    }

    // ------------------------------------------------------------------------
    // Variables tab
    // ------------------------------------------------------------------------

    private Control BuildVariablesTab()
    {
        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_variablesPanel, Dock.Top);
        dock.Children.Add(_variablesPanel);
        return dock;
    }

    private void RebuildVariablesPanel()
    {
        _variablesPanel.Children.Clear();

        _variablesPanel.Children.Add(new TextBlock
        {
            Text = "Additional Info",
            FontWeight = FontWeight.Bold,
        });
        _variablesPanel.Children.Add(LabeledRow("Platform:", _platformBox));
        _variablesPanel.Children.Add(LabeledRow("Region:", _regionBox));
        _variablesPanel.Children.Add(_emulatorCheckBox);

        // Speedrun.com-bound (read-only)
        if (Run.Metadata.VariableValueNames.Count > 0)
        {
            _variablesPanel.Children.Add(new TextBlock
            {
                Text = "Speedrun.com Variables (read-only)",
                FontWeight = FontWeight.Bold,
            });

            foreach (KeyValuePair<string, string> kv in Run.Metadata.VariableValueNames)
            {
                _variablesPanel.Children.Add(new TextBlock { Text = $"{kv.Key}: {kv.Value}", Foreground = Brushes.Gray });
            }
        }

        // Custom (editable)
        _variablesPanel.Children.Add(new TextBlock
        {
            Text = "Custom Variables",
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 12, 0, 0),
        });

        foreach (KeyValuePair<string, CustomVariable> entry in Run.Metadata.CustomVariables.ToList())
        {
            string name = entry.Key;
            CustomVariable variable = entry.Value;

            var nameLabel = new TextBlock { Text = name, Width = 160, VerticalAlignment = VerticalAlignment.Center };
            var valueBox = new TextBox { Text = variable.Value ?? string.Empty, Width = 240 };
            valueBox.TextChanged += (_, _) =>
            {
                variable.Value = valueBox.Text;
                Run.HasChanged = true;
            };

            var permanentChk = new CheckBox
            {
                Content = "Save",
                IsChecked = variable.IsPermanent,
                VerticalAlignment = VerticalAlignment.Center,
            };
            permanentChk.IsCheckedChanged += (_, _) =>
            {
                variable.IsPermanent = permanentChk.IsChecked == true;
                Run.HasChanged = true;
            };

            var removeBtn = new Button { Content = "Remove", VerticalAlignment = VerticalAlignment.Center };
            removeBtn.Click += (_, _) =>
            {
                Run.Metadata.CustomVariables.Remove(name);
                Run.HasChanged = true;
                RebuildVariablesPanel();
            };

            _variablesPanel.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { nameLabel, valueBox, permanentChk, removeBtn },
            });
        }

        var addBtn = new Button { Content = "Add Variable…", Margin = new Thickness(0, 12, 0, 0) };
        addBtn.Click += async (_, _) => await AddCustomVariable();
        _variablesPanel.Children.Add(addBtn);
    }

    private async Task AddCustomVariable()
    {
        var nameDlg = new TextInputDialog("New Variable", "Variable name:");
        string name = await nameDlg.ShowDialogAsync(this);
        if (string.IsNullOrWhiteSpace(name) || Run.Metadata.CustomVariables.ContainsKey(name))
        {
            return;
        }

        var valueDlg = new TextInputDialog("New Variable", "Value (optional):");
        string value = await valueDlg.ShowDialogAsync(this);

        Run.Metadata.CustomVariables[name] = new CustomVariable(value ?? string.Empty, isPermanent: true);
        Run.HasChanged = true;
        RebuildVariablesPanel();
    }

    // ------------------------------------------------------------------------
    // Game icon picker
    // ------------------------------------------------------------------------

    private async Task PickIcon()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Game Icon",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp" } },
                FilePickerFileTypes.All,
            },
        });

        IStorageFile picked = files?.FirstOrDefault();
        if (picked is null)
        {
            return;
        }

        try
        {
            await using Stream stream = await picked.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            byte[] raw = ms.ToArray();

            // Re-encode as PNG so the saved bytes are decoder-stable across platforms; SkiaSharp
            // is already a hard dependency, and decoding through SKBitmap dodges the System.Drawing
            // path that fails on Linux.
            using SKBitmap decoded = SKBitmap.Decode(raw);
            if (decoded is null)
            {
                return;
            }

            using SKImage image = SKImage.FromBitmap(decoded);
            using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 90);
            Run.GameIconPng = encoded.ToArray();
            Run.GameIcon = null; // Force editor + saver to use GameIconPng exclusively.
            Run.HasChanged = true;
            UpdateIconPreview();
        }
        catch (Exception ex)
        {
            LiveSplit.Options.Log.Error(ex);
        }
    }

    private void UpdateIconPreview()
    {
        try
        {
            if (Run.GameIconPng is { Length: > 0 } bytes)
            {
                using var ms = new MemoryStream(bytes);
                _iconImage.Source = new Bitmap(ms);
                _iconHint.Text = "Right-click to remove";
                return;
            }
        }
        catch (Exception ex)
        {
            LiveSplit.Options.Log.Error(ex);
        }

        _iconImage.Source = null;
        _iconHint.Text = "Click icon to set";
    }

    // ------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------

    private static StackPanel LabeledRow(string label, Control control)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Width = 110 },
                control,
            },
        };
    }

    private TimeSpan? GetPersonalBestSegmentTime(int segmentIndex, TimingMethod method)
    {
        TimeSpan previous = segmentIndex > 0
            ? Run[segmentIndex - 1].PersonalBestSplitTime[method] ?? TimeSpan.Zero
            : TimeSpan.Zero;
        return Run[segmentIndex].PersonalBestSplitTime[method] - previous;
    }

    /// <summary>Row projection for the History DataGrid. Strings let the grid format dates/times consistently.</summary>
    public sealed class AttemptRow
    {
        public int Index { get; }
        public string Started { get; }
        public string Ended { get; }
        public string RealTime { get; }
        public string GameTime { get; }
        public string PauseTime { get; }

        public AttemptRow(Attempt attempt)
        {
            Index = attempt.Index;
            Started = attempt.Started?.Time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
            Ended = attempt.Ended?.Time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
            RealTime = TimeFormatter.Format(attempt.Time.RealTime) ?? string.Empty;
            GameTime = TimeFormatter.Format(attempt.Time.GameTime) ?? string.Empty;
            PauseTime = attempt.PauseTime?.ToString() ?? string.Empty;
        }
    }
}

public static class RunEditorDialogModel
{
    private const string DefaultLayoutPath = "?default";

    public static void ApplyAcceptedRun(Run target, Run source)
    {
        target.GameName = source.GameName;
        target.CategoryName = source.CategoryName;
        target.Offset = source.Offset;
        target.AttemptCount = source.AttemptCount;
        target.GameIcon = source.GameIcon;
        target.GameIconPng = source.GameIconPng;
        target.AutoSplitter = source.AutoSplitter;
        target.AutoSplitterSettings = source.AutoSplitterSettings;
        target.LayoutPath = source.LayoutPath;
        target.AttemptHistory = new List<Attempt>(source.AttemptHistory);
        target.CustomComparisons = new List<string>(source.CustomComparisons);
        target.ComparisonGenerators = new List<IComparisonGenerator>(source.ComparisonGenerators);

        target.Clear();
        foreach (ISegment seg in source)
        {
            target.Add(seg.Clone() as ISegment);
        }

        CopyMetadata(target.Metadata, source.Metadata);
        target.HasChanged = true;
    }

    public static void SetPersonalBestSplitTime(IRun run, int segmentIndex, TimingMethod method, TimeSpan? time)
    {
        Time splitTime = run[segmentIndex].PersonalBestSplitTime;
        splitTime[method] = time;
        run[segmentIndex].PersonalBestSplitTime = splitTime;
        run.HasChanged = true;
    }

    public static void SetPersonalBestSegmentTime(IRun run, int segmentIndex, TimingMethod method, TimeSpan? segmentTime)
    {
        TimeSpan previous = segmentIndex > 0
            ? run[segmentIndex - 1].PersonalBestSplitTime[method] ?? TimeSpan.Zero
            : TimeSpan.Zero;

        SetPersonalBestSplitTime(run, segmentIndex, method, segmentTime + previous);
    }

    public static void InsertSegment(IRun run, int index, string name)
    {
        var segment = new Segment(name);
        foreach (string comparison in run.CustomComparisons)
        {
            segment.Comparisons[comparison] = new Time();
        }

        run.Insert(index, segment);
        run.FixSplits();
        run.HasChanged = true;
    }

    public static void RemoveSegment(IRun run, int index)
    {
        if (index < 0 || index >= run.Count || run.Count <= 1)
        {
            return;
        }

        run.RemoveAt(index);
        run.FixSplits();
        run.HasChanged = true;
    }

    public static void MoveSegment(IRun run, int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= run.Count || newIndex < 0 || newIndex >= run.Count || oldIndex == newIndex)
        {
            return;
        }

        ISegment moved = run[oldIndex];
        run.RemoveAt(oldIndex);
        run.Insert(newIndex, moved);
        run.HasChanged = true;
    }

    public static bool TryAddComparison(IRun run, string name)
    {
        if (!IsValidNewComparisonName(run, name))
        {
            return false;
        }

        run.CustomComparisons.Add(name);
        foreach (ISegment segment in run)
        {
            segment.Comparisons[name] = new Time();
        }

        run.HasChanged = true;
        return true;
    }

    public static bool TryRenameComparison(IRun run, string oldName, string newName)
    {
        if (string.Equals(oldName, Run.PersonalBestComparisonName, StringComparison.Ordinal)
            || !run.CustomComparisons.Contains(oldName)
            || !IsValidNewComparisonName(run, newName))
        {
            return false;
        }

        int index = run.CustomComparisons.IndexOf(oldName);
        run.CustomComparisons[index] = newName;
        foreach (ISegment segment in run)
        {
            Time oldTime = segment.Comparisons[oldName];
            segment.Comparisons.Remove(oldName);
            segment.Comparisons[newName] = oldTime;
        }

        run.HasChanged = true;
        return true;
    }

    public static bool TryRemoveComparison(IRun run, string name)
    {
        if (string.Equals(name, Run.PersonalBestComparisonName, StringComparison.Ordinal)
            || !run.CustomComparisons.Remove(name))
        {
            return false;
        }

        foreach (ISegment segment in run)
        {
            segment.Comparisons.Remove(name);
        }

        run.HasChanged = true;
        return true;
    }

    public static void SetLinkedLayout(IRun run, bool linked, string selectedPath)
    {
        run.LayoutPath = linked
            ? string.IsNullOrWhiteSpace(selectedPath) ? DefaultLayoutPath : selectedPath
            : null;
        run.HasChanged = true;
    }

    public static void SetAdditionalInfo(Run run, string platformName, string regionName, bool usesEmulator)
    {
        run.Metadata.PlatformName = platformName ?? string.Empty;
        run.Metadata.RegionName = regionName ?? string.Empty;
        run.Metadata.UsesEmulator = usesEmulator;
        run.HasChanged = true;
    }

    public static void ClearTimes(IRun run)
    {
        run.ClearTimes();
        run.HasChanged = true;
    }

    public static void CleanSumOfBest(IRun run, SumOfBest.CleanUpCallback callback)
    {
        SumOfBest.Clean(run, callback);
        run.HasChanged = true;
    }

    public static string DisplayLayoutPath(string layoutPath)
    {
        return string.Equals(layoutPath, DefaultLayoutPath, StringComparison.Ordinal) ? string.Empty : layoutPath ?? string.Empty;
    }

    private static bool IsValidNewComparisonName(IRun run, string name)
    {
        return !string.IsNullOrWhiteSpace(name)
            && !name.StartsWith("[Race]", StringComparison.Ordinal)
            && !run.Comparisons.Contains(name);
    }

    private static void CopyMetadata(RunMetadata target, RunMetadata source)
    {
        target.RunID = source.RunID;
        target.PlatformName = source.PlatformName;
        target.RegionName = source.RegionName;
        target.UsesEmulator = source.UsesEmulator;
        target.VariableValueNames = source.VariableValueNames.ToDictionary(x => x.Key, x => x.Value);

        target.CustomVariables.Clear();
        foreach (KeyValuePair<string, CustomVariable> kv in source.CustomVariables)
        {
            target.CustomVariables[kv.Key] = kv.Value.Clone();
        }
    }
}
