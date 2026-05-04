using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
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
using LiveSplit.Model.RunFactories;
using LiveSplit.TimeFormatters;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.Web.Share;

using SkiaSharp;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>
/// Avalonia editor for an <see cref="IRun"/>. Mirrors the original WinForms RunEditorDialog
/// layout with Real Time, Game Time, and Additional Info pages plus command-rail actions for
/// segment, comparison, history, autosplitter, and metadata editing.
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
    private readonly ComboBox _layoutPathBox;
    private readonly ComboBox _platformBox;
    private readonly ComboBox _regionBox;
    private readonly CheckBox _emulatorCheckBox;

    private readonly ListBox _segmentsList;
    private readonly StackPanel _realTimeRows;
    private readonly StackPanel _gameTimeRows;
    private readonly DataGrid _historyGrid;
    private readonly StackPanel _variablesPanel;
    private readonly List<ComboBox> _comparisonSelectors = [];

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
        Width = RunEditorDialogLayoutSpec.Master.InitialClientWidth;
        Height = RunEditorDialogLayoutSpec.Master.InitialClientHeight;
        MinWidth = RunEditorDialogLayoutSpec.Master.MinimumWindowWidth;
        MinHeight = RunEditorDialogLayoutSpec.Master.MinimumWindowHeight;
        DialogTheme.ApplyWindow(this);
        CanResize = true;

        _iconImage = new Image
        {
            Width = 120,
            Height = 120,
            Stretch = Stretch.Uniform,
        };
        _iconHint = new TextBlock
        {
            Text = "Click icon to set",
            FontSize = 10,
            Foreground = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var iconBorder = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Width = 120,
            Height = 120,
            Margin = new Thickness(10),
            Child = _iconImage,
            Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Hand),
        };
        iconBorder.PointerReleased += async (_, _) => await PickIcon();
        var iconContextMenu = new ContextMenu();
        var setIconItem = new MenuItem { Header = "Set Icon..." };
        setIconItem.Click += async (_, _) => await PickIcon();
        var downloadBoxArtItem = new MenuItem { Header = "Download Box Art" };
        downloadBoxArtItem.Click += async (_, _) => await DownloadSpeedrunComGameIcon(boxArt: true);
        var downloadIconItem = new MenuItem { Header = "Download Icon" };
        downloadIconItem.Click += async (_, _) => await DownloadSpeedrunComGameIcon(boxArt: false);
        var openFromUrlItem = new MenuItem { Header = "Open from URL..." };
        openFromUrlItem.Click += async (_, _) => await OpenGameIconFromUrl();
        var removeItem = new MenuItem { Header = "Remove Icon" };
        removeItem.Click += (_, _) => { Run.GameIconPng = null; Run.GameIcon = null; UpdateIconPreview(); Run.HasChanged = true; };
        iconContextMenu.Items.Add(setIconItem);
        iconContextMenu.Items.Add(downloadBoxArtItem);
        iconContextMenu.Items.Add(downloadIconItem);
        iconContextMenu.Items.Add(openFromUrlItem);
        iconContextMenu.Items.Add(removeItem);
        iconBorder.ContextMenu = iconContextMenu;

        _gameBox = new TextBox { Text = Run.GameName ?? "" };
        _categoryBox = new TextBox { Text = Run.CategoryName ?? "" };
        _offsetBox = new TextBox { Text = TimeFormatter.Format(Run.Offset) };
        _linkedLayoutCheckBox = new CheckBox { Content = "Use Layout", IsChecked = !string.IsNullOrEmpty(Run.LayoutPath) };
        string displayedLayoutPath = RunEditorDialogModel.DisplayLayoutPath(Run.LayoutPath);
        _layoutPathBox = new ComboBox
        {
            ItemsSource = BuildLayoutChoices(displayedLayoutPath),
            SelectedItem = displayedLayoutPath,
            Width = 245,
        };
        _linkedLayoutCheckBox.IsCheckedChanged += (_, _) => UpdateLayoutControls();
        UpdateLayoutControls();
        _platformBox = BuildMetadataChoiceBox();
        _regionBox = BuildMetadataChoiceBox();
        RefreshMetadataChoiceBoxes();
        _emulatorCheckBox = new CheckBox { Content = "Uses Emulator", IsChecked = Run.Metadata.UsesEmulator };
        _attemptBox = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 999999,
            Value = Run.AttemptCount,
            Width = 120,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        _segmentsList = BuildSegmentsList();
        _realTimeRows = new StackPanel { Spacing = 2 };
        _gameTimeRows = new StackPanel { Spacing = 2 };
        _historyGrid = BuildHistoryGrid();
        _variablesPanel = new StackPanel { Spacing = 8, Margin = new Thickness(20) };

        var root = BuildMasterRootGrid();
        Grid.SetColumn(iconBorder, 0);
        Grid.SetRow(iconBorder, 1);
        Grid.SetRowSpan(iconBorder, 4);
        root.Children.Add(iconBorder);

        AddToRoot(root, Label("Game Name:"), 1, 1, 3);
        AddToRoot(root, _gameBox, 4, 1, 5);
        AddToRoot(root, Label("Run Category:"), 1, 2, 3);
        AddToRoot(root, _categoryBox, 4, 2, 5);
        AddToRoot(root, Label("Start Timer at:"), 1, 3, 3);
        AddToRoot(root, _offsetBox, 4, 3, 2);
        AddToRoot(root, Label("Attempts:"), 6, 3, 2);
        AddToRoot(root, _attemptBox, 8, 3, 2);

        var autoSplitterDescription = BuildAutoSplitterDescription();
        AddToRoot(root, autoSplitterDescription, 1, 4, 4);
        AddToRoot(root, BuildAutoSplitterButtonPanel(), 5, 4, 3);
        AddToRoot(root, _linkedLayoutCheckBox, 4, 5);
        AddToRoot(root, BuildLayoutRow(), 5, 5, 3);

        var insertAboveBtn = RailButton("Insert Above");
        insertAboveBtn.Click += async (_, _) => await InsertSegment(above: true);
        var insertBelowBtn = RailButton("Insert Below");
        insertBelowBtn.Click += async (_, _) => await InsertSegment(above: false);
        var removeSegmentBtn = RailButton("Remove Segment");
        removeSegmentBtn.Click += (_, _) => RemoveSegment();
        var moveUpBtn = RailButton("Move Up");
        moveUpBtn.Click += (_, _) => MoveSegment(-1);
        var moveDownBtn = RailButton("Move Down");
        moveDownBtn.Click += (_, _) => MoveSegment(1);
        var addComparisonBtn = RailButton("Add Comparison");
        addComparisonBtn.Click += async (_, _) => await AddCustomComparison();
        var importComparisonBtn = RailButton("Import Comparison...");
        AttachImportComparisonMenu(importComparisonBtn);
        var otherBtn = RailButton("Other...");
        AttachOtherMenu(otherBtn);
        AddToRoot(root, insertAboveBtn, 0, 7);
        AddToRoot(root, insertBelowBtn, 0, 8);
        AddToRoot(root, removeSegmentBtn, 0, 9);
        AddToRoot(root, moveUpBtn, 0, 10);
        AddToRoot(root, moveDownBtn, 0, 11);
        AddToRoot(root, addComparisonBtn, 0, 12);
        AddToRoot(root, importComparisonBtn, 0, 13);
        AddToRoot(root, otherBtn, 0, 14);

        var runGrid = new ContentControl
        {
            Content = BuildTimingTab(TimingMethod.RealTime, _realTimeRows),
        };
        Grid.SetColumn(runGrid, 1);
        Grid.SetRow(runGrid, 7);
        Grid.SetColumnSpan(runGrid, 9);
        Grid.SetRowSpan(runGrid, 8);
        root.Children.Add(runGrid);

        var tabStrip = new TabControl
        {
            Items =
            {
                new TabItem { Header = "Real Time" },
                new TabItem { Header = "Game Time" },
                new TabItem { Header = "Additional Info" },
            },
            SelectedIndex = 0,
        };
        tabStrip.SelectionChanged += (_, _) =>
        {
            runGrid.Content = tabStrip.SelectedIndex switch
            {
                1 => BuildTimingTab(TimingMethod.GameTime, _gameTimeRows),
                2 => BuildVariablesTab(),
                _ => BuildTimingTab(TimingMethod.RealTime, _realTimeRows),
            };
        };
        Grid.SetColumn(tabStrip, 1);
        Grid.SetRow(tabStrip, 6);
        Grid.SetColumnSpan(tabStrip, 9);
        root.Children.Add(tabStrip);

        // Game-name to autosplitter lookup mirrors the original WinForms cbxGameName_TextChanged:
        // create the matching AutoSplitter from AutoSplitterFactory and auto-activate when the
        // game appears in Settings.ActiveAutoSplitters.
        _gameBox.TextChanged += (_, _) => RefreshAutoSplitterFromGameName();
        RefreshAutoSplitterFromGameName();

        var ok = new Button { Content = "OK", Width = 75, IsDefault = true };
        ok.Click += (_, _) => OkClicked();
        var cancel = new Button { Content = "Cancel", Width = 75, IsCancel = true, Margin = new Thickness(8, 0, 0, 0) };
        cancel.Click += (_, _) => CancelAndClose();
        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(ok, 0);
        Grid.SetColumn(cancel, 1);
        footer.Children.Add(ok);
        footer.Children.Add(cancel);
        AddToRoot(root, footer, 6, 15, 4);
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
        if (Run is Run editedRunForNames)
        {
            RunEditorDialogModel.SetGameName(editedRunForNames, _gameBox.Text ?? string.Empty);
            RunEditorDialogModel.SetCategoryName(editedRunForNames, _categoryBox.Text ?? string.Empty);
        }
        else
        {
            Run.GameName = _gameBox.Text ?? string.Empty;
            Run.CategoryName = _categoryBox.Text ?? string.Empty;
        }

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
            SelectedLayoutPath());
        if (Run is Run editedRunForMetadata)
        {
            RunEditorDialogModel.SetAdditionalInfo(
                editedRunForMetadata,
                SelectedMetadataChoice(_platformBox),
                SelectedMetadataChoice(_regionBox),
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

    private static Grid BuildMasterRootGrid()
        => new()
        {
            ColumnDefinitions = new ColumnDefinitions("140,39,62,49,136,65,104,88,*,115"),
            RowDefinitions = new RowDefinitions("5,35,35,35,35,35,25,29,29,29,29,29,29,29,*,36,20"),
        };

    private static void AddToRoot(Grid root, Control control, int column, int row, int columnSpan = 1, int rowSpan = 1)
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

        root.Children.Add(control);
    }

    private static TextBlock Label(string text)
        => new()
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
        };

    private static Button RailButton(string text)
        => new()
        {
            Content = text,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(5, 1),
        };

    private Control BuildLayoutRow()
    {
        var browseBtn = new Button { Content = "Browse...", Width = 86 };
        browseBtn.Click += async (_, _) => await PickLayoutPath();

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { _layoutPathBox, browseBtn },
        };
    }

    private void UpdateLayoutControls()
    {
        if (_layoutPathBox is not null)
        {
            _layoutPathBox.IsEnabled = _linkedLayoutCheckBox?.IsChecked == true;
        }
    }

    private string SelectedLayoutPath()
        => _layoutPathBox.SelectedItem as string ?? string.Empty;

    private static IReadOnlyList<string> BuildLayoutChoices(string selectedPath)
    {
        var choices = new List<string> { string.Empty };
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            choices.Add(selectedPath);
        }

        return choices;
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
            string path = picked.TryGetLocalPath() ?? picked.Path.LocalPath;
            _layoutPathBox.ItemsSource = BuildLayoutChoices(path);
            _layoutPathBox.SelectedItem = path;
            UpdateLayoutControls();
        }
    }

    private void AttachImportComparisonMenu(Button importComparisonBtn)
    {
        var importMenu = new ContextMenu();
        var fromFile = new MenuItem { Header = "From File..." };
        fromFile.Click += async (_, _) => await ImportComparisonFromFile();
        var fromUrl = new MenuItem { Header = "From URL..." };
        fromUrl.Click += async (_, _) => await ImportComparisonFromUrl();
        importMenu.Items.Add(fromFile);
        importMenu.Items.Add(fromUrl);
        importComparisonBtn.ContextMenu = importMenu;
        importComparisonBtn.Click += (_, _) => importMenu.Open(importComparisonBtn);
    }

    private void AttachOtherMenu(Button otherBtn)
    {
        var menu = new ContextMenu();
        var renameSegment = new MenuItem { Header = "Rename Segment" };
        renameSegment.Click += async (_, _) => await RenameSegment();
        var renameComparison = new MenuItem { Header = "Rename Comparison" };
        renameComparison.Click += async (_, _) => await RenameSelectedComparison(EnsureCommandComparisonSelector());
        var removeComparison = new MenuItem { Header = "Remove Comparison" };
        removeComparison.Click += (_, _) => RemoveSelectedComparison(EnsureCommandComparisonSelector());
        var removeAttempt = new MenuItem { Header = "Remove Selected Attempt" };
        removeAttempt.Click += (_, _) => RemoveSelectedAttempt();
        var clearHistory = new MenuItem { Header = "Clear History" };
        clearHistory.Click += (_, _) => ClearHistory();
        var clearTimes = new MenuItem { Header = "Clear Times" };
        clearTimes.Click += (_, _) => ClearTimes();
        var cleanSob = new MenuItem { Header = "Clean Sum of Best" };
        cleanSob.Click += async (_, _) => await CleanSumOfBest();
        menu.Items.Add(renameSegment);
        menu.Items.Add(renameComparison);
        menu.Items.Add(removeComparison);
        menu.Items.Add(removeAttempt);
        menu.Items.Add(clearHistory);
        menu.Items.Add(clearTimes);
        menu.Items.Add(cleanSob);
        otherBtn.ContextMenu = menu;
        otherBtn.Click += (_, _) => menu.Open(otherBtn);
    }

    private ComboBox EnsureCommandComparisonSelector()
    {
        ComboBox selector = _comparisonSelectors.FirstOrDefault();
        if (selector is null)
        {
            selector = new ComboBox
            {
                ItemsSource = EditableComparisons().ToList(),
                SelectedIndex = 0,
            };
            _comparisonSelectors.Add(selector);
        }

        return selector;
    }

    // ------------------------------------------------------------------------
    // Auto Splitter tab - activate/deactivate + per-component settings
    // ------------------------------------------------------------------------

    private TextBlock BuildAutoSplitterDescription()
    {
        _autoSplitterDescription = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        return _autoSplitterDescription;
    }

    private Control BuildAutoSplitterButtonPanel()
    {
        _autoSplitterActivateBtn = new Button { Content = "Activate", Width = 86 };
        _autoSplitterActivateBtn.Click += (_, _) => ToggleAutoSplitterActivation();

        _autoSplitterSettingsBtn = new Button { Content = "Settings...", Width = 86 };
        _autoSplitterSettingsBtn.Click += async (_, _) => await OpenAutoSplitterSettings();

        _autoSplitterWebsiteBtn = new Button { Content = "Website", Width = 86 };
        _autoSplitterWebsiteBtn.Click += (_, _) => OpenAutoSplitterWebsite();

        RefreshAutoSplitterUI();
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _autoSplitterActivateBtn, _autoSplitterSettingsBtn, _autoSplitterWebsiteBtn },
        };
    }

    private Control BuildAutoSplitterTab()
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 8,
            Children = { BuildAutoSplitterDescription(), BuildAutoSplitterButtonPanel() },
        };

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
    // Edit tab - segment list with add/remove/rename/reorder
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
        => await InsertSegment(above: false);

    private async Task InsertSegment(bool above)
    {
        var prompt = new TextInputDialog("New Segment", "Segment name:");
        if (await prompt.ShowDialogAsync(this) is { } name && !string.IsNullOrWhiteSpace(name))
        {
            int idx = _segmentsList.SelectedIndex < 0
                ? Run.Count
                : _segmentsList.SelectedIndex + (above ? 0 : 1);
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
    // Real Time / Game Time tabs - per-segment time matrix
    // ------------------------------------------------------------------------

    private Control BuildTimingTab(TimingMethod method, StackPanel rowsPanel)
    {
        var comparisonSelector = new ComboBox
        {
            Width = 180,
            ItemsSource = EditableComparisons().ToList(),
            SelectedIndex = 0,
        };
        _comparisonSelectors.Add(comparisonSelector);

        return new ScrollViewer
        {
            Content = rowsPanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(8),
        };
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
            segLabel.PointerPressed += (_, _) => _segmentsList.SelectedIndex = segIndex;
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

        if (Run.Count > 0 && (_segmentsList.SelectedIndex < 0 || _segmentsList.SelectedIndex >= Run.Count))
        {
            _segmentsList.SelectedIndex = 0;
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

        RefreshComparisonSelectors(name);
        RebuildTimingRows();
    }

    private async Task ImportComparisonFromFile()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Comparison from File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("LiveSplit Splits") { Patterns = ["*.lss"] },
                FilePickerFileTypes.All,
            ],
        });

        IStorageFile file = files?.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        try
        {
            await using Stream stream = await file.OpenReadAsync();
            IRun imported = LoadRunForComparison(stream, file.Path?.LocalPath ?? string.Empty);
            await ImportComparisonWithNamePrompt(imported, Path.GetFileNameWithoutExtension(file.Name));
        }
        catch (Exception ex)
        {
            LiveSplit.Options.Log.Error(ex);
            await new MessageDialog("Error", "The selected file was not recognized as a splits file.").ShowDialogAsync(this);
        }
    }

    private async Task ImportComparisonFromUrl()
    {
        var urlPrompt = new TextInputDialog("Import Comparison from URL", "URL:");
        string url = await urlPrompt.ShowDialogAsync(this);
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            using var client = new HttpClient();
            await using Stream stream = await client.GetStreamAsync(url);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            memory.Position = 0;
            IRun imported = LoadRunForComparison(memory, string.Empty);
            string defaultName = Uri.TryCreate(url, UriKind.Absolute, out Uri uri)
                ? Path.GetFileNameWithoutExtension(uri.LocalPath)
                : string.Empty;
            await ImportComparisonWithNamePrompt(imported, defaultName);
        }
        catch (Exception ex)
        {
            LiveSplit.Options.Log.Error(ex);
            await new MessageDialog("Error", "The splits file couldn't be downloaded.").ShowDialogAsync(this);
        }
    }

    private async Task ImportComparisonWithNamePrompt(IRun imported, string defaultName)
    {
        if (imported is null)
        {
            return;
        }

        var namePrompt = new TextInputDialog("Enter Comparison Name", "Name:", defaultName ?? string.Empty);
        string name = await namePrompt.ShowDialogAsync(this);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (!RunEditorDialogModel.TryImportComparisonFromRun(Run, imported, name))
        {
            await new MessageDialog("Invalid Comparison Name", "A Comparison with this name already exists or is reserved.").ShowDialogAsync(this);
            return;
        }

        RefreshComparisonSelectors(name);
        RebuildTimingRows();
    }

    private static IRun LoadRunForComparison(Stream stream, string filePath)
    {
        var runFactory = new StandardFormatsRunFactory
        {
            Stream = stream,
            FilePath = filePath,
        };
        return runFactory.Create(new StandardComparisonGeneratorsFactory());
    }

    private async Task RenameSelectedComparison(ComboBox selector)
    {
        string oldName = selector?.SelectedItem as string;
        if (string.IsNullOrEmpty(oldName))
        {
            return;
        }

        var prompt = new TextInputDialog("Rename Comparison", "Comparison Name:", oldName);
        string newName = await prompt.ShowDialogAsync(this);
        if (!RunEditorDialogModel.TryRenameComparison(Run, oldName, newName))
        {
            return;
        }

        State.CallComparisonRenamed(new RenameEventArgs
        {
            OldName = oldName,
            NewName = newName,
        });
        RefreshComparisonSelectors(newName);
        RebuildTimingRows();
    }

    private void RemoveSelectedComparison(ComboBox selector)
    {
        string oldName = selector?.SelectedItem as string;
        if (string.IsNullOrEmpty(oldName) || !RunEditorDialogModel.TryRemoveComparison(Run, oldName))
        {
            return;
        }

        State.CallComparisonRenamed(new RenameEventArgs
        {
            OldName = oldName,
            NewName = "Current Comparison",
        });
        RefreshComparisonSelectors();
        RebuildTimingRows();
    }

    private IEnumerable<string> EditableComparisons()
        => Run.CustomComparisons.Where(x => !string.Equals(x, LiveSplit.Model.Run.PersonalBestComparisonName, StringComparison.Ordinal));

    private void RefreshComparisonSelectors(string preferred = null)
    {
        foreach (ComboBox selector in _comparisonSelectors)
        {
            var comparisons = EditableComparisons().ToList();
            string selected = preferred ?? (selector.SelectedItem as string);
            selector.ItemsSource = comparisons;
            selector.SelectedItem = comparisons.Contains(selected) ? selected : comparisons.FirstOrDefault();
        }
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

    private static ComboBox BuildMetadataChoiceBox()
        => new()
        {
            Width = 180,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

    private static string SelectedMetadataChoice(ComboBox box)
        => box.SelectedItem as string ?? string.Empty;

    private void RefreshMetadataChoiceBoxes()
    {
        RefreshMetadataChoiceBox(
            _platformBox,
            RunEditorDialogModel.GetPlatformChoiceList(Run.Metadata),
            Run.Metadata.PlatformName);
        RefreshMetadataChoiceBox(
            _regionBox,
            RunEditorDialogModel.GetRegionChoiceList(Run.Metadata),
            Run.Metadata.RegionName);
    }

    private static void RefreshMetadataChoiceBox(ComboBox box, IReadOnlyList<string> choices, string selected)
    {
        box.ItemsSource = choices;
        box.SelectedItem = choices.Contains(selected ?? string.Empty, StringComparer.Ordinal)
            ? selected ?? string.Empty
            : string.Empty;
        box.IsEnabled = choices.Count > 1;
    }

    private void BuildSpeedrunComVariableControls(Grid metadataGrid)
    {
        IDictionary<SpeedrunComSharp.Variable, SpeedrunComSharp.VariableValue> variables;
        try
        {
            variables = Run.Metadata.VariableValues;
        }
        catch
        {
            variables = new Dictionary<SpeedrunComSharp.Variable, SpeedrunComSharp.VariableValue>();
        }

        if (variables.Count == 0)
        {
            return;
        }

        int variableIndex = 0;
        foreach (KeyValuePair<SpeedrunComSharp.Variable, SpeedrunComSharp.VariableValue> entry in variables)
        {
            SpeedrunComSharp.Variable variable = entry.Key;
            if (variable is null)
            {
                continue;
            }

            string current = entry.Value?.Value ?? string.Empty;
            Control editor = variable.IsUserDefined
                ? BuildUserDefinedSpeedrunComVariableControl(variable, current)
                : BuildFixedSpeedrunComVariableControl(variable, current);

            int row = 3 + (variableIndex / 2);
            int labelColumn = variableIndex % 2 == 0 ? 0 : 3;
            int editorColumn = variableIndex % 2 == 0 ? 1 : 4;
            AddMetadataControl(metadataGrid, new TextBlock
            {
                Text = variable.Name + ":",
                VerticalAlignment = VerticalAlignment.Center,
            }, labelColumn, row);
            AddMetadataControl(metadataGrid, editor, editorColumn, row);
            variableIndex++;
        }
    }

    private static void AddMetadataControl(Grid metadataGrid, Control control, int column, int row, int columnSpan = 1)
    {
        while (metadataGrid.RowDefinitions.Count <= row)
        {
            metadataGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        Grid.SetColumn(control, column);
        Grid.SetRow(control, row);
        if (columnSpan > 1)
        {
            Grid.SetColumnSpan(control, columnSpan);
        }

        metadataGrid.Children.Add(control);
    }

    private Control BuildFixedSpeedrunComVariableControl(SpeedrunComSharp.Variable variable, string current)
    {
        var choices = RunEditorDialogModel.BuildMetadataChoiceList(variable.Values.Select(x => x.Value), current);
        var combo = new ComboBox
        {
            ItemsSource = choices,
            SelectedItem = choices.Contains(current, StringComparer.Ordinal) ? current : string.Empty,
            Width = 240,
        };
        combo.SelectionChanged += (_, _) =>
        {
            RunEditorDialogModel.SetSpeedrunComVariableValue(
                Run.Metadata,
                variable.Name,
                variable.Values.Select(x => x.Value),
                variable.IsUserDefined,
                combo.SelectedItem as string ?? string.Empty);
        };
        return combo;
    }

    private Control BuildUserDefinedSpeedrunComVariableControl(SpeedrunComSharp.Variable variable, string current)
    {
        var box = new TextBox
        {
            Text = current,
            Width = 240,
        };
        box.TextChanged += (_, _) =>
        {
            RunEditorDialogModel.SetSpeedrunComVariableValue(
                Run.Metadata,
                variable.Name,
                variable.Values.Select(x => x.Value),
                variable.IsUserDefined,
                box.Text ?? string.Empty);
        };
        return box;
    }

    private static Control BuildSpeedrunComRulesControl(string rules)
    {
        var panel = new StackPanel
        {
            Spacing = 4,
        };
        panel.Children.Add(new TextBox
        {
            Text = rules,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 70,
        });

        foreach (string link in RunEditorDialogModel.ExtractRulesLinks(rules))
        {
            var linkButton = new Button
            {
                Content = link,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            linkButton.Click += (_, _) => PlatformLauncher.Open(link);
            panel.Children.Add(linkButton);
        }

        return panel;
    }

    private void RebuildVariablesPanel()
    {
        _variablesPanel.Children.Clear();

        var metadataGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("103,*,20,112,*"),
            RowDefinitions = new RowDefinitions("Auto,85,Auto,Auto,Auto,Auto,Auto,*,Auto"),
        };

        var rulesLabel = Label("Rules:");
        Grid.SetRow(rulesLabel, 0);
        metadataGrid.Children.Add(rulesLabel);
        string rules = RunEditorDialogModel.BuildSpeedrunComRulesText(Run.Metadata);
        Control rulesBox = BuildSpeedrunComRulesControl(rules);
        Grid.SetRow(rulesBox, 1);
        Grid.SetColumnSpan(rulesBox, 5);
        metadataGrid.Children.Add(rulesBox);

        var platformLabel = Label("Platform:");
        Grid.SetRow(platformLabel, 2);
        metadataGrid.Children.Add(platformLabel);
        AddMetadataControl(metadataGrid, _platformBox, 1, 2);
        var regionLabel = Label("Region:");
        Grid.SetColumn(regionLabel, 3);
        Grid.SetRow(regionLabel, 2);
        metadataGrid.Children.Add(regionLabel);
        AddMetadataControl(metadataGrid, _regionBox, 4, 2);

        _emulatorCheckBox.IsVisible = RunEditorDialogModel.ShouldShowEmulatorCheckbox(Run.Metadata);
        AddMetadataControl(metadataGrid, _emulatorCheckBox, 0, 7, 5);

        SpeedrunComAssociationState association = RunEditorDialogModel.GetSpeedrunComAssociationState(Run.Metadata);
        var submitRun = new Button
        {
            Content = "Submit Run...",
            IsEnabled = association.CanSubmit,
            Width = 140,
        };
        submitRun.Click += async (_, _) => await SubmitSpeedrunComRun();
        var associateRun = new Button
        {
            Content = association.AssociateButtonText,
            Width = 210,
        };
        associateRun.Click += async (_, _) =>
        {
            if (string.IsNullOrEmpty(Run.Metadata.RunID))
            {
                await AssociateSpeedrunComRun();
            }
            else
            {
                ShowSpeedrunComRun();
            }
        };
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { submitRun, associateRun },
        };
        Grid.SetRow(buttonPanel, 8);
        Grid.SetColumnSpan(buttonPanel, 5);
        metadataGrid.Children.Add(buttonPanel);

        BuildSpeedrunComVariableControls(metadataGrid);
        _variablesPanel.Children.Add(metadataGrid);

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

        var addBtn = new Button { Content = "Add Variable...", Margin = new Thickness(0, 12, 0, 0) };
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

    private async Task AssociateSpeedrunComRun()
    {
        var prompt = new TextInputDialog("Enter Speedrun.com URL", "Speedrun.com Run URL:");
        string url = await prompt.ShowDialogAsync(this);
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            SpeedrunComSharp.Run speedrunComRun = SpeedrunCom.Client.Runs.GetRunFromSiteUri(url);
            if (speedrunComRun is null)
            {
                await new MessageDialog("Invalid URL", "The URL provided is not a valid speedrun.com Run URL.").ShowDialogAsync(this);
                return;
            }

            Run.PatchRun(speedrunComRun);
            RefreshMetadataFieldsFromRun();
            RebuildVariablesPanel();
        }
        catch (Exception ex)
        {
            LiveSplit.Options.Log.Error(ex);
            await new MessageDialog("Error", "The run could not be associated.").ShowDialogAsync(this);
        }
    }

    private void ShowSpeedrunComRun()
    {
        try
        {
            if (Run.Metadata.Run?.WebLink is { } link)
            {
                PlatformLauncher.Open(link.AbsoluteUri);
            }
        }
        catch (Exception ex)
        {
            LiveSplit.Options.Log.Error(ex);
        }
    }

    private async Task SubmitSpeedrunComRun()
    {
        if (!SpeedrunCom.ValidateRun(Run, out string reason))
        {
            await new MessageDialog("Submitting Failed", reason).ShowDialogAsync(this);
            return;
        }

        var submitDialog = new SpeedrunComSubmitDialog(Run.Metadata);
        if (await submitDialog.ShowDialogAsync(this))
        {
            RebuildVariablesPanel();
        }
    }

    private void RefreshMetadataFieldsFromRun()
    {
        _gameBox.Text = Run.GameName ?? string.Empty;
        _categoryBox.Text = Run.CategoryName ?? string.Empty;
        RefreshMetadataChoiceBoxes();
        _emulatorCheckBox.IsChecked = Run.Metadata.UsesEmulator;
        Run.HasChanged = true;
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
            if (!SetGameIconFromBytes(ms.ToArray()))
            {
                return;
            }
        }
        catch (Exception ex)
        {
            LiveSplit.Options.Log.Error(ex);
        }
    }

    private async Task OpenGameIconFromUrl()
    {
        var prompt = new TextInputDialog("Open Game Icon from URL", "URL:");
        string url = await prompt.ShowDialogAsync(this);
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            await SetGameIconFromUri(new Uri(url));
        }
        catch (Exception ex)
        {
            LiveSplit.Options.Log.Error(ex);
            await new MessageDialog("Error", "The Game Icon couldn't be downloaded.").ShowDialogAsync(this);
        }
    }

    private async Task DownloadSpeedrunComGameIcon(bool boxArt)
    {
        try
        {
            Uri uri = boxArt
                ? Run.Metadata.Game?.Assets?.CoverMedium?.Uri
                : Run.Metadata.Game?.Assets?.Icon?.Uri;
            if (uri is not null)
            {
                await SetGameIconFromUri(uri);
                return;
            }
        }
        catch (Exception ex)
        {
            LiveSplit.Options.Log.Error(ex);
        }

        await new MessageDialog(
            "Error",
            boxArt ? "Could not download the box art of the game!" : "Could not download the icon of the game!")
            .ShowDialogAsync(this);
    }

    private async Task SetGameIconFromUri(Uri uri)
    {
        using var client = new HttpClient();
        byte[] bytes = await client.GetByteArrayAsync(uri);
        if (!SetGameIconFromBytes(bytes))
        {
            await new MessageDialog("Error", "The URL was not recognized as an image.").ShowDialogAsync(this);
        }
    }

    private bool SetGameIconFromBytes(byte[] raw)
    {
        using SKBitmap decoded = SKBitmap.Decode(raw);
        if (decoded is null)
        {
            return false;
        }

        using SKImage image = SKImage.FromBitmap(decoded);
        using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 90);
        Run.GameIconPng = encoded.ToArray();
        Run.GameIcon = null;
        Run.HasChanged = true;
        UpdateIconPreview();
        return true;
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

public readonly record struct SpeedrunComAssociationState(bool CanSubmit, string AssociateButtonText);

internal sealed class RunEditorDialogLayoutSpec
{
    public static RunEditorDialogLayoutSpec Master { get; } = new();

    public IReadOnlyList<int> ColumnWidths { get; } = [140, 39, 62, 49, 136, 65, 104, 88, -1, 115];
    public IReadOnlyList<int> RowHeights { get; } = [5, 35, 35, 35, 35, 35, 25, 29, 29, 29, 29, 29, 29, 29, -1, 36, 20];
    public IReadOnlyList<string> VisibleTabHeaders { get; } = ["Real Time", "Game Time", "Additional Info"];
    public IReadOnlyList<string> HeaderLabels { get; } = ["Game Name:", "Run Category:", "Start Timer at:", "Attempts:"];
    public IReadOnlyList<string> CommandRailLabels { get; } =
    [
        "Insert Above",
        "Insert Below",
        "Remove Segment",
        "Move Up",
        "Move Down",
        "Add Comparison",
        "Import Comparison...",
        "Other...",
    ];
    public IReadOnlyList<int> MetadataColumnWidths { get; } = [103, -1, 20, 112, -1];

    public int InitialClientWidth => 684;
    public int InitialClientHeight => 511;
    public int MinimumWindowWidth => 700;
    public int MinimumWindowHeight => 510;
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

    public static bool TryImportComparisonFromRun(IRun target, IRun comparisonRun, string name)
    {
        if (target is null || comparisonRun is null || comparisonRun.Count == 0 || !IsValidNewComparisonName(target, name))
        {
            return false;
        }

        target.CustomComparisons.Add(name);
        int maxMatched = -1;
        foreach (ISegment segment in comparisonRun)
        {
            if (ReferenceEquals(segment, comparisonRun.Last()))
            {
                target.Last().Comparisons[name] = comparisonRun.Last().PersonalBestSplitTime;
                continue;
            }

            int matchingIndex = FindMatchingSegmentIndex(target, segment, maxMatched + 1);
            if (matchingIndex >= 0)
            {
                target[matchingIndex].Comparisons[name] = segment.PersonalBestSplitTime;
                maxMatched = matchingIndex;
            }
        }

        target.HasChanged = true;
        target.FixSplits();
        return true;
    }

    public static void SetLinkedLayout(IRun run, bool linked, string selectedPath)
    {
        run.LayoutPath = linked
            ? string.IsNullOrWhiteSpace(selectedPath) ? DefaultLayoutPath : selectedPath
            : null;
        run.HasChanged = true;
    }

    public static void SetGameName(Run run, string gameName)
    {
        gameName ??= string.Empty;
        if (run.GameName == gameName)
        {
            return;
        }

        run.GameName = gameName;
        run.Metadata.RunID = null;
        run.HasChanged = true;
    }

    public static void SetCategoryName(Run run, string categoryName)
    {
        categoryName ??= string.Empty;
        if (run.CategoryName == categoryName)
        {
            return;
        }

        run.CategoryName = categoryName;
        run.Metadata.RunID = null;
        run.HasChanged = true;
    }

    public static void SetAdditionalInfo(Run run, string platformName, string regionName, bool usesEmulator)
    {
        platformName ??= string.Empty;
        regionName ??= string.Empty;
        bool changed = run.Metadata.PlatformName != platformName
            || run.Metadata.RegionName != regionName
            || run.Metadata.UsesEmulator != usesEmulator;

        run.Metadata.PlatformName = platformName;
        run.Metadata.RegionName = regionName;
        run.Metadata.UsesEmulator = usesEmulator;
        if (changed)
        {
            run.Metadata.RunID = null;
        }

        run.HasChanged = true;
    }

    public static bool SetSpeedrunComVariableValue(
        RunMetadata metadata,
        string variableName,
        IEnumerable<string> validValues,
        bool isUserDefined,
        string value)
    {
        if (metadata is null || string.IsNullOrWhiteSpace(variableName))
        {
            return false;
        }

        string variableValue = null;
        if (!string.IsNullOrEmpty(value))
        {
            IReadOnlyCollection<string> choices = validValues?.ToArray() ?? [];
            bool knownChoice = choices.Contains(value, StringComparer.Ordinal);
            if (knownChoice || isUserDefined)
            {
                variableValue = value;
            }
            else
            {
                return false;
            }
        }

        if (variableValue is null)
        {
            if (!metadata.VariableValueNames.Remove(variableName))
            {
                return false;
            }
        }
        else
        {
            metadata.VariableValueNames[variableName] = variableValue;
        }

        metadata.RunID = null;
        metadata.LiveSplitRun.HasChanged = true;
        return true;
    }

    public static SpeedrunComAssociationState GetSpeedrunComAssociationState(RunMetadata metadata)
        => string.IsNullOrEmpty(metadata?.RunID)
            ? new SpeedrunComAssociationState(true, "Associate with Speedrun.com...")
            : new SpeedrunComAssociationState(false, "Show on Speedrun.com...");

    public static string BuildSpeedrunComRulesText(RunMetadata metadata)
    {
        try
        {
            SpeedrunComSharp.Game game = metadata?.Game;
            return game is null
                ? string.Empty
                : BuildSpeedrunComRulesText(
                    game.Ruleset.DefaultTimingMethod,
                    game.Ruleset.RequiresVideo,
                    metadata.Category?.Rules ?? string.Empty);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string BuildSpeedrunComRulesText(
        SpeedrunComSharp.TimingMethod defaultTimingMethod,
        bool requiresVideo,
        string categoryRules)
    {
        var additionalRules = new List<string>();
        if (defaultTimingMethod != SpeedrunComSharp.TimingMethod.RealTime)
        {
            additionalRules.Add(defaultTimingMethod == SpeedrunComSharp.TimingMethod.RealTimeWithoutLoads
                ? "are timed without the loading times"
                : "are timed with the Game Time");
        }

        if (requiresVideo)
        {
            additionalRules.Add("require video proof");
        }

        var parts = new List<string>();
        if (additionalRules.Count > 0)
        {
            string rulesText = additionalRules.Count == 1
                ? additionalRules[0]
                : string.Join(", ", additionalRules.Take(additionalRules.Count - 1)) + " and " + additionalRules.Last();
            parts.Add($"Runs of this game {rulesText}.");
        }

        if (!string.IsNullOrWhiteSpace(categoryRules))
        {
            parts.Add(categoryRules);
        }

        return string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

    public static bool ShouldShowEmulatorCheckbox(RunMetadata metadata)
    {
        try
        {
            SpeedrunComSharp.Game game = metadata?.Game;
            return ShouldShowEmulatorCheckbox(gameAvailable: game != null, emulatorsAllowed: game?.Ruleset.EmulatorsAllowed == true);
        }
        catch
        {
            return false;
        }
    }

    public static bool ShouldShowEmulatorCheckbox(bool gameAvailable, bool emulatorsAllowed)
        => gameAvailable && emulatorsAllowed;

    public static IReadOnlyList<string> GetPlatformChoiceList(RunMetadata metadata)
    {
        try
        {
            return BuildMetadataChoiceList(
                metadata?.Game?.Platforms.Select(x => x.Name) ?? [],
                metadata?.PlatformName);
        }
        catch
        {
            return BuildMetadataChoiceList([], metadata?.PlatformName);
        }
    }

    public static IReadOnlyList<string> GetRegionChoiceList(RunMetadata metadata)
    {
        try
        {
            return BuildMetadataChoiceList(
                metadata?.Game?.Regions.Select(x => x.Name) ?? [],
                metadata?.RegionName);
        }
        catch
        {
            return BuildMetadataChoiceList([], metadata?.RegionName);
        }
    }

    public static IReadOnlyList<string> BuildMetadataChoiceList(IEnumerable<string> choices, string currentValue)
    {
        var result = new List<string> { string.Empty };
        foreach (string choice in choices ?? [])
        {
            if (!string.IsNullOrEmpty(choice) && !result.Contains(choice, StringComparer.Ordinal))
            {
                result.Add(choice);
            }
        }

        if (!string.IsNullOrEmpty(currentValue) && !result.Contains(currentValue, StringComparer.Ordinal))
        {
            result.Add(currentValue);
        }

        return result;
    }

    public static IReadOnlyList<string> ExtractRulesLinks(string rules)
        => Regex.Matches(rules ?? string.Empty, @"https?://[^\s\]\)""<>]+")
            .Select(x => x.Value.TrimEnd('.', ',', ';', ':'))
            .Distinct(StringComparer.Ordinal)
            .ToList();

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

    private static int FindMatchingSegmentIndex(IRun target, ISegment segment, int startIndex)
    {
        for (int i = startIndex; i < target.Count; i++)
        {
            if (string.Equals(
                target[i].Name?.Trim(),
                segment.Name?.Trim(),
                StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
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
