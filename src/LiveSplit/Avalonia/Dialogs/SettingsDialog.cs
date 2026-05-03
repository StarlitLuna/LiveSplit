using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Styling;

using LiveSplit.Model.Input;
using LiveSplit.Options;
using LiveSplit.Web;
using LiveSplit.Web.Share;
using LiveSplit.Web.SRL;

namespace LiveSplit.Avalonia.Dialogs;

public sealed class SettingsDialog : Window
{
    private readonly TaskCompletionSource<bool> _result = new();
    private readonly ISettings _settings;
    private readonly ComboBox _profileBox;
    private readonly Grid _hotkeyRows;
    private readonly Button _removeProfile;
    private readonly Control _profileSection;
    private TextBox _hotkeyDelayBox;
    private TextBox _serverPortBox;
    private TextBox _refreshRateBox;
    private string _validationError;

    public string SelectedHotkeyProfile { get; private set; }

    public SettingsDialog(ISettings settings, string selectedHotkeyProfile = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        SelectedHotkeyProfile = !string.IsNullOrEmpty(selectedHotkeyProfile)
            && settings.HotkeyProfiles.ContainsKey(selectedHotkeyProfile)
                ? selectedHotkeyProfile
                : settings.HotkeyProfiles.FirstOrDefault().Key;

        Title = "Settings";
        Width = SettingsDialogLayoutSpec.Master.InitialWindowWidth;
        Height = SettingsDialogLayoutSpec.Master.InitialWindowHeight;
        MinWidth = SettingsDialogLayoutSpec.Master.InitialWindowWidth;
        MinHeight = 200;
        FontSize = 12;
        RequestedThemeVariant = ThemeVariant.Dark;
        Background = DialogTheme.WindowBackgroundBrush;

        _profileBox = new ComboBox
        {
            Name = "ActiveHotkeyProfileComboBox",
            ItemsSource = settings.HotkeyProfiles.Keys.ToList(),
            SelectedItem = SelectedHotkeyProfile,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = SettingsDialogLayoutSpec.Master.ComboBoxHeight,
            MinHeight = 0,
        };
        _profileBox.SelectionChanged += (_, _) =>
        {
            SelectedHotkeyProfile = _profileBox.SelectedItem as string ?? SelectedHotkeyProfile;
            RebuildHotkeyRows();
            RefreshRemoveButton();
        };

        DialogTheme.Apply(_profileBox);

        _hotkeyRows = CreateHotkeyGrid();
        _removeProfile = CompactButton("Remove", "RemoveHotkeyProfileButton", SettingsDialogLayoutSpec.Master.ProfileButtonWidth);
        _removeProfile.Click += (_, _) => RemoveProfile();
        _profileSection = BuildProfileSection();

        var ok = CompactButton("OK", "OkButton", SettingsDialogLayoutSpec.Master.ProfileButtonWidth);
        ok.IsDefault = true;
        ok.Click += async (_, _) =>
        {
            if (!TryApplyPendingTextSettings())
            {
                await new MessageDialog(
                    "Invalid Settings",
                    _validationError ?? "Please enter valid numeric settings.").ShowDialogAsync(this);
                return;
            }

            _result.TrySetResult(true);
            Close();
        };
        var cancel = CompactButton("Cancel", "CancelButton", SettingsDialogLayoutSpec.Master.ProfileButtonWidth);
        cancel.IsCancel = true;
        cancel.Click += (_, _) => { _result.TrySetResult(false); Close(); };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 6,
            Margin = new Thickness(0, 6, 16, 12),
            Children = { ok, cancel },
        };

        var root = new DockPanel
        {
            LastChildFill = true,
            Background = DialogTheme.WindowBackgroundBrush,
        };
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(new ScrollViewer
        {
            Content = BuildSettingsSurface(),
            Margin = new Thickness(7, 7, 7, 0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });
        Content = root;

        Closed += (_, _) =>
        {
            if (!_result.Task.IsCompleted)
            {
                _result.TrySetResult(false);
            }
        };
    }

    private Control BuildSettingsSurface()
    {
        RebuildHotkeyRows();
        RefreshRemoveButton();

        var grid = new Grid
        {
            Width = 422,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(new GridLength(29)),
                new RowDefinition(new GridLength(29)),
                new RowDefinition(new GridLength(29)),
                new RowDefinition(new GridLength(29)),
                new RowDefinition(new GridLength(29)),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(new GridLength(29)),
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(206)),
                new ColumnDefinition(new GridLength(135)),
                new ColumnDefinition(new GridLength(26)),
                new ColumnDefinition(new GridLength(55)),
            },
        };

        AddToGrid(grid, GroupBox("HotkeysGroup", "Hotkeys", _hotkeyRows, 421), 0, 0, 4);
        AddToGrid(grid, CompactCheckBox("Simple Sum of Best Calculation", _settings.SimpleSumOfBest, value => _settings.SimpleSumOfBest = value), 1, 0);
        AddToGrid(grid, CompactCheckBox("Warn On Reset If Better Times", _settings.WarnOnReset, value => _settings.WarnOnReset = value), 1, 1, 3);

        AddToGrid(grid, Label("Race Viewer:"), 2, 0);
        AddToGrid(grid, BuildRaceViewer(), 2, 1, 3);
        AddToGrid(grid, Label("Racing Services:"), 3, 0);
        AddToGrid(grid, BuildProvidersButton(), 3, 1, 3);
        AddToGrid(grid, Label("Active Comparisons:"), 4, 0);
        AddToGrid(grid, BuildComparisonsButton(), 4, 1, 3);
        AddToGrid(grid, Label("Saved Accounts:"), 5, 0);
        AddToGrid(grid, BuildLogoutButton(), 5, 1, 3);
        AddToGrid(grid, GroupBox("LiveSplitServerGroup", "LiveSplit Server", BuildServerRows(), 78), 6, 0, 4);
        AddToGrid(grid, BuildRefreshRatePanel(), 7, 0);

        return grid;
    }

    private Control BuildProfileSection()
    {
        var add = CompactButton("New", "NewHotkeyProfileButton", SettingsDialogLayoutSpec.Master.ProfileButtonWidth);
        add.Click += async (_, _) => await AddProfile();
        var rename = CompactButton("Rename", "RenameHotkeyProfileButton", SettingsDialogLayoutSpec.Master.ProfileButtonWidth);
        rename.Click += async (_, _) => await RenameProfile();

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(new GridLength(29)),
                new RowDefinition(new GridLength(29)),
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(194)),
                new ColumnDefinition(new GridLength(41)),
                new ColumnDefinition(new GridLength(81)),
                new ColumnDefinition(new GridLength(82)),
            },
        };

        AddToGrid(grid, Label("Active Hotkey Profile:"), 0, 0);
        AddToGrid(grid, _profileBox, 0, 1, 3);
        AddToGrid(grid, add, 1, 0, 2, HorizontalAlignment.Right);
        AddToGrid(grid, rename, 1, 2, 1, HorizontalAlignment.Right);
        AddToGrid(grid, _removeProfile, 1, 3, 1, HorizontalAlignment.Right);

        return GroupBox("HotkeyProfilesGroup", "Hotkey Profiles", grid, 77);
    }

    private Control BuildRaceViewer()
    {
        var raceViewer = new ComboBox
        {
            Name = "RaceViewerComboBox",
            ItemsSource = new[] { "SpeedRunsLive", "MultiTwitch", "Kadgar", "Speedrun.tv" },
            SelectedItem = _settings.RaceViewer?.Name ?? "SpeedRunsLive",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = SettingsDialogLayoutSpec.Master.ComboBoxHeight,
            MinHeight = 0,
        };
        DialogTheme.Apply(raceViewer);
        raceViewer.SelectionChanged += (_, _) =>
        {
            if (raceViewer.SelectedItem is string name)
            {
                _settings.RaceViewer = RaceViewer.FromName(name);
            }
        };

        return raceViewer;
    }

    private Control BuildProvidersButton()
    {
        var providers = CompactButton("Manage Racing Services...", "ManageRacingServicesButton", double.NaN);
        providers.Click += async (_, _) =>
        {
            var edited = _settings.RaceProvider.Select(x => (RaceProviderSettings)x.Clone()).ToList();
            var dlg = new RaceProviderManagingDialog(edited);
            if (await dlg.ShowDialogAsync(this))
            {
                _settings.RaceProvider = edited;
            }
        };

        return providers;
    }

    private Control BuildComparisonsButton()
    {
        var comparisons = CompactButton("Choose Active Comparisons...", "ChooseActiveComparisonsButton", double.NaN);
        comparisons.Click += async (_, _) =>
        {
            var dialog = new ChooseComparisonsDialog
            {
                ComparisonGeneratorStates = new Dictionary<string, bool>(_settings.ComparisonGeneratorStates),
                HcpHistorySize = _settings.HcpHistorySize,
                HcpNBestRuns = _settings.HcpNBestRuns,
            };

            if (await dialog.ShowDialogAsync(this))
            {
                _settings.ComparisonGeneratorStates = new Dictionary<string, bool>(dialog.ComparisonGeneratorStates);
                _settings.HcpHistorySize = dialog.HcpHistorySize;
                _settings.HcpNBestRuns = dialog.HcpNBestRuns;
            }
        };

        return comparisons;
    }

    private Control BuildLogoutButton()
    {
        var logout = CompactButton("Log Out of All Accounts", "LogOutAccountsButton", double.NaN);
        logout.IsEnabled = WebCredentials.AnyCredentialsExist();
        logout.Click += (_, _) =>
        {
            SpeedrunCom.ClearAccessToken();
            Twitch.Instance.ClearAccessToken();
            WebCredentials.DeleteAllCredentials();
            logout.IsEnabled = WebCredentials.AnyCredentialsExist();
        };

        return logout;
    }

    private Control BuildServerRows()
    {
        _serverPortBox = CompactTextBox(
            "ServerPortTextBox",
            SettingsDialogModel.FormatServerPort(_settings),
            SettingsDialogLayoutSpec.Master.ServerPortTextBoxWidth,
            TextAlignment.Right);

        var startup = new ComboBox
        {
            Name = "ServerStartupComboBox",
            ItemsSource = new[]
            {
                "Don't start the Server",
                "Start TCP Server",
                "Start Websocket Server",
                "Restore Previous State",
            },
            SelectedIndex = (int)_settings.ServerStartup,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = SettingsDialogLayoutSpec.Master.ComboBoxHeight,
            MinHeight = 0,
        };
        DialogTheme.Apply(startup);
        startup.SelectionChanged += (_, _) =>
        {
            if (Enum.IsDefined(typeof(ServerStartupType), startup.SelectedIndex))
            {
                _settings.ServerStartup = (ServerStartupType)startup.SelectedIndex;
            }
        };

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(new GridLength(29)),
                new RowDefinition(new GridLength(29)),
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(200)),
                new ColumnDefinition(new GridLength(SettingsDialogLayoutSpec.Master.InputCellWidth)),
            },
        };

        AddToGrid(grid, Label("Server Port:"), 0, 0);
        AddToGrid(grid, _serverPortBox, 0, 1);
        AddToGrid(grid, Label("Startup Behavior:"), 1, 0);
        AddToGrid(grid, startup, 1, 1);

        return grid;
    }

    private Control BuildRefreshRatePanel()
    {
        _refreshRateBox = CompactTextBox(
            "RefreshRateTextBox",
            SettingsDialogModel.FormatRefreshRate(_settings),
            SettingsDialogLayoutSpec.Master.RefreshRateTextBoxVisibleWidth,
            TextAlignment.Right,
            new Thickness(0));

        var grid = new Grid
        {
            Width = 184,
            Height = 29,
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(130)),
                new ColumnDefinition(new GridLength(51)),
            },
        };

        AddToGrid(grid, Label("Refresh Rate (Hz):"), 0, 0);
        AddToGrid(grid, _refreshRateBox, 0, 1);
        return grid;
    }

    private void RebuildHotkeyRows()
    {
        _hotkeyRows.Children.Clear();
        if (string.IsNullOrEmpty(SelectedHotkeyProfile)
            || !_settings.HotkeyProfiles.TryGetValue(SelectedHotkeyProfile, out HotkeyProfile profile))
        {
            return;
        }

        AddKeyRow(0, "Start / Split:", "StartSplitHotkeyTextBox", () => profile.SplitKey, v => profile.SplitKey = v);
        AddKeyRow(1, "Reset:", "ResetHotkeyTextBox", () => profile.ResetKey, v => profile.ResetKey = v);
        AddKeyRow(2, "Undo Split:", "UndoSplitHotkeyTextBox", () => profile.UndoKey, v => profile.UndoKey = v);
        AddKeyRow(3, "Skip Split:", "SkipSplitHotkeyTextBox", () => profile.SkipKey, v => profile.SkipKey = v);
        AddKeyRow(4, "Pause:", "PauseHotkeyTextBox", () => profile.PauseKey, v => profile.PauseKey = v);
        AddKeyRow(5, "Switch Comparison (Previous):", "SwitchComparisonPreviousHotkeyTextBox", () => profile.SwitchComparisonPrevious, v => profile.SwitchComparisonPrevious = v);
        AddKeyRow(6, "Switch Comparison (Next):", "SwitchComparisonNextHotkeyTextBox", () => profile.SwitchComparisonNext, v => profile.SwitchComparisonNext = v);
        AddKeyRow(7, "Toggle Global Hotkeys:", "ToggleGlobalHotkeysTextBox", () => profile.ToggleGlobalHotkeys, v => profile.ToggleGlobalHotkeys = v);

        var deactivate = CompactCheckBox("Deactivate For Other Programs", profile.DeactivateHotkeysForOtherPrograms, value => profile.DeactivateHotkeysForOtherPrograms = value);
        deactivate.CheckedChanged += (_, _) =>
        {
            if (deactivate.IsEnabled)
            {
                profile.DeactivateHotkeysForOtherPrograms = deactivate.IsChecked;
            }
        };

        var global = CompactCheckBox("Global Hotkeys", profile.GlobalHotkeysEnabled, value => profile.GlobalHotkeysEnabled = value);
        global.CheckedChanged += (_, _) =>
        {
            profile.GlobalHotkeysEnabled = global.IsChecked;
            RefreshDeactivateHotkeysCheckbox(profile, deactivate);
        };
        AddToGrid(_hotkeyRows, global, 8, 0);
        AddToGrid(_hotkeyRows, deactivate, 8, 1, 2);
        RefreshDeactivateHotkeysCheckbox(profile, deactivate);

        AddToGrid(_hotkeyRows, CompactCheckBox("Double Tap Prevention", profile.DoubleTapPrevention, value => profile.DoubleTapPrevention = value), 9, 0);
        AddToGrid(_hotkeyRows, Label("Hotkey Delay (Seconds):"), 9, 1);
        _hotkeyDelayBox = CompactTextBox(
            "HotkeyDelayTextBox",
            SettingsDialogModel.FormatHotkeyDelay(profile),
            SettingsDialogLayoutSpec.Master.HotkeyDelayTextBoxVisibleWidth,
            TextAlignment.Right,
            new Thickness(0, 0, SettingsDialogLayoutSpec.Master.ControlHorizontalMargin, 0));
        AddToGrid(_hotkeyRows, _hotkeyDelayBox, 9, 2, 1, HorizontalAlignment.Right);

        var allowGamepads = CompactCheckBox("Allow Gamepads as Hotkeys", profile.AllowGamepadsAsHotkeys, value => profile.AllowGamepadsAsHotkeys = value);
        allowGamepads.Name = "AllowGamepadsHotkeysCheckBox";
        allowGamepads.IsEnabled = false;
        allowGamepads.SetTextBrush(DialogTheme.DisabledTextBrush);

        var dpiAware = CompactCheckBox("Enable DPI Aware", _settings.EnableDPIAwareness, value => _settings.EnableDPIAwareness = value);
        dpiAware.Name = "EnableDpiAwareCheckBox";

        AddToGrid(_hotkeyRows, allowGamepads, 10, 0);
        AddToGrid(_hotkeyRows, dpiAware, 10, 1, 2);
        AddToGrid(_hotkeyRows, _profileSection, 11, 0, 3);
    }

    private void AddKeyRow(int row, string label, string name, Func<KeyOrButton> get, Action<KeyOrButton> set)
    {
        AddToGrid(_hotkeyRows, Label(label), row, 0);
        AddToGrid(_hotkeyRows, KeyTextBox(name, get, set), row, 1, 2);
    }

    internal bool TryApplyPendingTextSettings()
    {
        bool ok = SettingsDialogModel.TryApplyNumericTextSettings(
            _settings,
            SelectedHotkeyProfile,
            _hotkeyDelayBox?.Text,
            _serverPortBox?.Text,
            _refreshRateBox?.Text);
        _validationError = ok ? null : "Hotkey Delay, Server Port, and Refresh Rate must be valid numbers.";
        if (ok && _hotkeyDelayBox is not null && CurrentProfile() is HotkeyProfile profile)
        {
            _hotkeyDelayBox.Text = SettingsDialogModel.FormatHotkeyDelay(profile);
        }

        return ok;
    }

    private static void RefreshDeactivateHotkeysCheckbox(HotkeyProfile profile, CompactSettingCheckBox deactivate)
    {
        deactivate.IsEnabled = profile.GlobalHotkeysEnabled;
        deactivate.IsChecked = profile.GlobalHotkeysEnabled && profile.DeactivateHotkeysForOtherPrograms;
    }

    private async Task AddProfile()
    {
        while (true)
        {
            string name = await new TextInputDialog("New Hotkey Profile", "Hotkey Profile Name:").ShowDialogAsync(this);
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            if (!_settings.HotkeyProfiles.ContainsKey(name))
            {
                HotkeyProfile source = CurrentProfile() ?? _settings.HotkeyProfiles.Values.FirstOrDefault();
                _settings.HotkeyProfiles[name] = source != null ? (HotkeyProfile)source.Clone() : new HotkeyProfile();
                SelectedHotkeyProfile = name;
                RefreshProfiles();
                return;
            }

            if (await ShowDuplicateProfileMessage() != MessageResult.Ok)
            {
                return;
            }
        }
    }

    private async Task RenameProfile()
    {
        if (CurrentProfile() is null || string.IsNullOrEmpty(SelectedHotkeyProfile))
        {
            return;
        }

        string oldName = SelectedHotkeyProfile;
        while (true)
        {
            string name = await new TextInputDialog("Rename Hotkey Profile", "Hotkey Profile Name:", oldName).ShowDialogAsync(this);
            if (string.IsNullOrWhiteSpace(name) || string.Equals(name, oldName, StringComparison.Ordinal))
            {
                return;
            }

            if (!_settings.HotkeyProfiles.ContainsKey(name))
            {
                for (int i = 0; i < _settings.RecentSplits.Count; i++)
                {
                    RecentSplitsFile file = _settings.RecentSplits[i];
                    if (file.LastHotkeyProfile == oldName)
                    {
                        _settings.RecentSplits[i] = new RecentSplitsFile(
                            file.Path,
                            file.LastTimingMethod,
                            name,
                            file.GameName,
                            file.CategoryName);
                    }
                }

                HotkeyProfile profile = _settings.HotkeyProfiles[oldName];
                _settings.HotkeyProfiles.Remove(oldName);
                _settings.HotkeyProfiles[name] = profile;
                SelectedHotkeyProfile = name;
                RefreshProfiles();
                return;
            }

            if (await ShowDuplicateProfileMessage() != MessageResult.Ok)
            {
                return;
            }
        }
    }

    private async Task<MessageResult> ShowDuplicateProfileMessage()
    {
        var message = new MessageDialog(
            "Hotkey Profile Already Exists",
            "A Hotkey Profile with this name already exists.",
            MessageDialog.Buttons.RetryCancel);
        return await message.ShowDialogResultAsync(this);
    }

    private void RemoveProfile()
    {
        if (_settings.HotkeyProfiles.Count <= 1 || string.IsNullOrEmpty(SelectedHotkeyProfile))
        {
            return;
        }

        _settings.HotkeyProfiles.Remove(SelectedHotkeyProfile);
        SelectedHotkeyProfile = _settings.HotkeyProfiles.Keys.Last();
        RefreshProfiles();
    }

    private void RefreshProfiles()
    {
        _profileBox.ItemsSource = _settings.HotkeyProfiles.Keys.ToList();
        _profileBox.SelectedItem = SelectedHotkeyProfile;
        RebuildHotkeyRows();
        RefreshRemoveButton();
    }

    private void RefreshRemoveButton()
        => _removeProfile.IsEnabled = _settings.HotkeyProfiles.Count > 1;

    private HotkeyProfile CurrentProfile()
    {
        return !string.IsNullOrEmpty(SelectedHotkeyProfile)
            && _settings.HotkeyProfiles.TryGetValue(SelectedHotkeyProfile, out HotkeyProfile profile)
                ? profile
                : null;
    }

    private static TextBox KeyTextBox(string name, Func<KeyOrButton> get, Action<KeyOrButton> set)
    {
        var box = CompactTextBox(name, FormatKey(get()), SettingsDialogLayoutSpec.Master.HotkeyTextBoxWidth, TextAlignment.Left);
        box.IsReadOnly = true;

        bool capturing = false;
        string oldText = null;

        void BeginCapture()
        {
            if (capturing)
            {
                return;
            }

            capturing = true;
            oldText = box.Text;
            box.Text = "Set Hotkey...";
            box.CaretIndex = 0;
        }

        void Commit(KeyOrButton binding)
        {
            set(binding);
            box.Text = FormatKey(binding);
            capturing = false;
        }

        box.GotFocus += (_, _) => BeginCapture();
        box.PointerPressed += (_, _) => BeginCapture();
        box.LostFocus += (_, _) =>
        {
            if (!capturing)
            {
                return;
            }

            capturing = false;
            box.Text = oldText ?? FormatKey(get());
        };

        box.KeyDown += (_, e) =>
        {
            if (!capturing)
            {
                return;
            }

            Key? mapped = HotkeyService.ToLiveSplitKey(e.Key);
            if (mapped is null)
            {
                return;
            }

            Key keyCode = mapped.Value.GetKeyCode();
            if (keyCode == Key.Escape)
            {
                Commit(null);
                e.Handled = true;
                return;
            }

            if (keyCode.IsModifierKeyCode())
            {
                return;
            }

            Commit(new KeyOrButton(keyCode.WithModifiers(HotkeyService.ToLiveSplitModifiers(e.KeyModifiers))));
            e.Handled = true;
        };

        box.KeyUp += (_, e) =>
        {
            if (!capturing)
            {
                return;
            }

            Key? mapped = HotkeyService.ToLiveSplitKey(e.Key);
            if (mapped is null)
            {
                return;
            }

            Key keyCode = mapped.Value.GetKeyCode();
            if (!keyCode.IsModifierKeyCode())
            {
                return;
            }

            Commit(new KeyOrButton(keyCode.WithModifiers(HotkeyService.ToLiveSplitModifiers(e.KeyModifiers) | ModifierForKey(keyCode))));
            e.Handled = true;
        };

        return box;
    }

    private static Key ModifierForKey(Key keyCode)
    {
        return keyCode switch
        {
            Key.ShiftKey or Key.LShiftKey or Key.RShiftKey => Key.Shift,
            Key.ControlKey or Key.LControlKey or Key.RControlKey => Key.Control,
            Key.Menu or Key.LMenu or Key.RMenu => Key.Alt,
            _ => Key.None,
        };
    }

    private static Grid CreateHotkeyGrid()
    {
        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(new GridLength(29)),
                new RowDefinition(new GridLength(29)),
                new RowDefinition(new GridLength(29)),
                new RowDefinition(new GridLength(29)),
                new RowDefinition(new GridLength(29)),
                new RowDefinition(new GridLength(29)),
                new RowDefinition(new GridLength(29)),
                new RowDefinition(new GridLength(29)),
                new RowDefinition(new GridLength(29)),
                new RowDefinition(new GridLength(29)),
                new RowDefinition(new GridLength(29)),
                new RowDefinition(new GridLength(83)),
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(200)),
                new ColumnDefinition(new GridLength(154)),
                new ColumnDefinition(new GridLength(56)),
            },
        };

        return grid;
    }

    private static TextBlock Label(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = DialogTheme.TextBrush,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            Margin = new Thickness(SettingsDialogLayoutSpec.Master.ControlHorizontalMargin, 0),
        };
    }

    private static CompactSettingCheckBox CompactCheckBox(string text, bool value, Action<bool> update)
    {
        var box = new CompactSettingCheckBox(text, value);
        box.CheckedChanged += (_, _) => update(box.IsChecked);
        return box;
    }

    private static TextBox CompactTextBox(string name, string text, double width, TextAlignment alignment, Thickness? margin = null)
    {
        var box = new TextBox
        {
            Name = name,
            Text = text,
            Width = width,
            Height = SettingsDialogLayoutSpec.Master.TextBoxHeight,
            MinHeight = 0,
            Margin = margin ?? new Thickness(SettingsDialogLayoutSpec.Master.ControlHorizontalMargin, 0),
            Background = DialogTheme.ControlBackgroundBrush,
            Foreground = DialogTheme.TextBrush,
            BorderBrush = DialogTheme.ControlBorderBrush,
            BorderThickness = new Thickness(1),
            TextAlignment = alignment,
            FontSize = 12,
            Padding = new Thickness(4, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        return box;
    }

    private static Button CompactButton(string text, string name, double width)
    {
        var button = new Button
        {
            Name = name,
            Content = text,
            Width = width,
            Height = SettingsDialogLayoutSpec.Master.ButtonHeight,
            MinHeight = 0,
            Margin = new Thickness(SettingsDialogLayoutSpec.Master.ControlHorizontalMargin, 0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = DialogTheme.ButtonBackgroundBrush,
            Foreground = DialogTheme.TextBrush,
            BorderBrush = DialogTheme.ControlBorderBrush,
            BorderThickness = new Thickness(1),
            FontSize = 12,
            Padding = new Thickness(8, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        return button;
    }

    private static Control GroupBox(string name, string title, Control content, double height)
    {
        var root = new Grid
        {
            Height = height,
            Margin = new Thickness(SettingsDialogLayoutSpec.Master.GroupHorizontalMargin, 0),
        };

        var border = new Border
        {
            Name = name,
            BorderBrush = DialogTheme.GroupBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(
                SettingsDialogLayoutSpec.Master.GroupContentHorizontalPadding,
                SettingsDialogLayoutSpec.Master.GroupContentTopPadding,
                SettingsDialogLayoutSpec.Master.GroupContentHorizontalPadding,
                6),
            Margin = new Thickness(0, 7, 0, 0),
            Child = content,
            Background = DialogTheme.WindowBackgroundBrush,
        };

        var label = Label(title);
        label.FontWeight = FontWeight.Bold;
        label.Background = DialogTheme.WindowBackgroundBrush;
        label.Padding = new Thickness(4, 0);
        label.Margin = new Thickness(8, 0, 0, 0);
        label.HorizontalAlignment = HorizontalAlignment.Left;
        label.VerticalAlignment = VerticalAlignment.Top;

        root.Children.Add(border);
        root.Children.Add(label);
        return root;
    }

    private static void AddToGrid(Grid grid, Control control, int row, int column, int columnSpan = 1, HorizontalAlignment horizontalAlignment = HorizontalAlignment.Stretch)
    {
        control.HorizontalAlignment = horizontalAlignment;
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        if (columnSpan > 1)
        {
            Grid.SetColumnSpan(control, columnSpan);
        }

        grid.Children.Add(control);
    }

    private static string FormatKey(KeyOrButton binding)
    {
        if (binding is null)
        {
            return "None";
        }

        string keyString = binding.ToString();
        if (binding.IsButton)
        {
            int lastSpaceIndex = keyString.LastIndexOf(' ');
            if (lastSpaceIndex != -1)
            {
                keyString = keyString[..lastSpaceIndex];
            }
        }

        return keyString;
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
}

internal sealed class SettingsDialogLayoutSpec
{
    public static SettingsDialogLayoutSpec Master { get; } = new();

    public IReadOnlyList<string> StructuralOrder { get; } = new[]
    {
        "HotkeysGroup",
        "HotkeyProfilesGroup",
        "LiveSplitServerGroup",
        "RefreshRateTextBox",
    };

    public int LabelColumnWidth { get; } = 200;
    public int HotkeyTextBoxWidth { get; } = 204;
    public int ServerPortTextBoxWidth { get; } = 204;
    public int HotkeyDelayTextBoxWidth { get; } = 50;
    public int RefreshRateTextBoxWidth { get; } = 51;
    public int HotkeyDelayTextBoxVisibleWidth { get; } = 42;
    public int RefreshRateTextBoxVisibleWidth { get; } = 48;
    public int ProfileButtonWidth { get; } = 75;
    public int InitialWindowWidth { get; } = 442;
    public int InitialWindowHeight { get; } = 734;
    public int TextBoxHeight { get; } = 20;
    public int ComboBoxHeight { get; } = 26;
    public int ButtonHeight { get; } = 23;
    public int CheckBoxHeight { get; } = 17;
    public int CheckBoxGlyphSize { get; } = 13;
    public int ControlHorizontalMargin { get; } = 3;
    public int GroupContentHorizontalPadding { get; } = 3;
    public int GroupContentTopPadding { get; } = 8;
    public int GroupHorizontalMargin { get; } = 3;
    public int InputCellWidth { get; } = 210;
    public int ModernCheckBoxCornerRadius { get; } = 2;
    public IReadOnlyList<string> NumericSpinnerControlNames { get; } = Array.Empty<string>();
}

internal static class SettingsDialogModel
{
    public static bool TryApplyNumericTextSettings(
        ISettings settings,
        string selectedHotkeyProfile,
        string hotkeyDelayText,
        string serverPortText,
        string refreshRateText)
    {
        if (settings is null
            || string.IsNullOrEmpty(selectedHotkeyProfile)
            || !settings.HotkeyProfiles.TryGetValue(selectedHotkeyProfile, out HotkeyProfile profile)
            || !TryParseFloat(hotkeyDelayText, out float hotkeyDelay)
            || !TryParseInt(serverPortText, out int serverPort)
            || !TryParseInt(refreshRateText, out int refreshRate))
        {
            return false;
        }

        profile.HotkeyDelay = Math.Max(hotkeyDelay, 0f);
        settings.ServerPort = serverPort;
        settings.RefreshRate = Math.Min(Math.Max(refreshRate, 20), 300);
        return true;
    }

    public static string FormatHotkeyDelay(HotkeyProfile profile)
        => (profile?.HotkeyDelay ?? 0f).ToString(CultureInfo.CurrentCulture);

    public static string FormatServerPort(ISettings settings)
        => (settings?.ServerPort ?? 16834).ToString(CultureInfo.CurrentCulture);

    public static string FormatRefreshRate(ISettings settings)
        => (settings?.RefreshRate ?? 40).ToString(CultureInfo.CurrentCulture);

    private static bool TryParseInt(string text, out int value)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
            || int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryParseFloat(string text, out float value)
        => float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            || float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}

internal static class DialogTheme
{
    public static Color WindowBackgroundColor { get; } = Color.Parse("#202020");
    public static IBrush WindowBackgroundBrush { get; } = new SolidColorBrush(WindowBackgroundColor);
    public static IBrush TextBrush { get; } = new SolidColorBrush(Colors.White);
    public static IBrush DisabledTextBrush { get; } = new SolidColorBrush(Color.Parse("#9A9A9A"));
    public static IBrush GroupBorderBrush { get; } = new SolidColorBrush(Color.Parse("#3D3D3D"));
    public static IBrush ControlBackgroundBrush { get; } = new SolidColorBrush(Color.Parse("#2A2A2A"));
    public static IBrush ButtonBackgroundBrush { get; } = new SolidColorBrush(Color.Parse("#3A3A3A"));
    public static IBrush ControlBorderBrush { get; } = new SolidColorBrush(Color.Parse("#8A8A8A"));
    public static IBrush AccentBrush { get; } = new SolidColorBrush(Color.Parse("#0078D4"));

    public static void Apply(Control control)
    {
        switch (control)
        {
            case TextBlock text:
                text.Foreground = TextBrush;
                text.FontSize = 12;
                break;
            case ComboBox combo:
                combo.Foreground = TextBrush;
                combo.Background = ControlBackgroundBrush;
                combo.BorderBrush = ControlBorderBrush;
                combo.BorderThickness = new Thickness(1);
                combo.FontSize = 12;
                combo.MinHeight = 0;
                combo.Margin = new Thickness(SettingsDialogLayoutSpec.Master.ControlHorizontalMargin, 0);
                break;
        }
    }
}

internal sealed class CompactSettingCheckBox : StackPanel
{
    private readonly Border _box;
    private readonly TextBlock _mark;
    private readonly TextBlock _label;
    private bool _isChecked;

    public event EventHandler CheckedChanged;

    public CompactSettingCheckBox(string text, bool isChecked)
    {
        Orientation = Orientation.Horizontal;
        Spacing = 4;
        Height = SettingsDialogLayoutSpec.Master.CheckBoxHeight;
        MinHeight = 0;
        Margin = new Thickness(7, 0, SettingsDialogLayoutSpec.Master.ControlHorizontalMargin, 0);
        VerticalAlignment = VerticalAlignment.Center;

        _mark = new TextBlock
        {
            Text = "\u2713",
            Foreground = DialogTheme.TextBrush,
            FontSize = 10,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = isChecked,
        };

        _box = new Border
        {
            Width = SettingsDialogLayoutSpec.Master.CheckBoxGlyphSize,
            Height = SettingsDialogLayoutSpec.Master.CheckBoxGlyphSize,
            CornerRadius = new CornerRadius(SettingsDialogLayoutSpec.Master.ModernCheckBoxCornerRadius),
            BorderThickness = new Thickness(1),
            BorderBrush = DialogTheme.ControlBorderBrush,
            Background = isChecked ? DialogTheme.AccentBrush : DialogTheme.WindowBackgroundBrush,
            Child = _mark,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _label = new TextBlock
        {
            Text = text,
            Foreground = DialogTheme.TextBrush,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Children.Add(_box);
        Children.Add(_label);

        _isChecked = isChecked;
        PointerPressed += (_, e) =>
        {
            if (!IsEnabled)
            {
                return;
            }

            IsChecked = !IsChecked;
            e.Handled = true;
        };
    }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
            {
                return;
            }

            _isChecked = value;
            UpdateVisual();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetTextBrush(IBrush brush)
    {
        _label.Foreground = brush;
    }

    private void UpdateVisual()
    {
        _mark.IsVisible = _isChecked;
        _box.Background = _isChecked ? DialogTheme.AccentBrush : DialogTheme.WindowBackgroundBrush;
    }
}
