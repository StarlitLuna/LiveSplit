using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

using LiveSplit.Model.Input;
using LiveSplit.Options;
using LiveSplit.UI;
using LiveSplit.Web;
using LiveSplit.Web.SRL;

namespace LiveSplit.Avalonia.Dialogs;

public sealed class SettingsDialog : Window
{
    private readonly TaskCompletionSource<bool> _result = new();
    private readonly ISettings _settings;
    private readonly ComboBox _profileBox;
    private readonly StackPanel _hotkeyRows;

    public string SelectedHotkeyProfile { get; private set; }

    public SettingsDialog(ISettings settings, string selectedHotkeyProfile = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        SelectedHotkeyProfile = !string.IsNullOrEmpty(selectedHotkeyProfile)
            && settings.HotkeyProfiles.ContainsKey(selectedHotkeyProfile)
                ? selectedHotkeyProfile
                : settings.HotkeyProfiles.FirstOrDefault().Key;

        Title = "LiveSplit Settings";
        Width = 680;
        Height = 680;
        MinWidth = 560;
        MinHeight = 520;

        _profileBox = new ComboBox
        {
            MinWidth = 180,
            ItemsSource = settings.HotkeyProfiles.Keys.ToList(),
            SelectedItem = SelectedHotkeyProfile,
        };
        _profileBox.SelectionChanged += (_, _) =>
        {
            SelectedHotkeyProfile = _profileBox.SelectedItem as string ?? SelectedHotkeyProfile;
            RebuildHotkeyRows();
        };

        _hotkeyRows = new StackPanel { Spacing = 6 };

        var tabs = new TabControl
        {
            Items =
            {
                new TabItem { Header = "Hotkeys", Content = BuildHotkeyTab() },
                new TabItem { Header = "Comparisons", Content = BuildComparisonTab() },
                new TabItem { Header = "Racing", Content = BuildRacingTab() },
                new TabItem { Header = "General", Content = BuildGeneralTab() },
            }
        };

        var ok = new Button { Content = "OK", Width = 80, IsDefault = true };
        ok.Click += (_, _) => { _result.TrySetResult(true); Close(); };
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        cancel.Click += (_, _) => { _result.TrySetResult(false); Close(); };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 0, 12, 12),
            Children = { cancel, ok },
        };

        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(tabs);
        Content = root;

        Closed += (_, _) =>
        {
            if (!_result.Task.IsCompleted)
            {
                _result.TrySetResult(false);
            }
        };
    }

    private Control BuildHotkeyTab()
    {
        var add = new Button { Content = "Add", Width = 74 };
        add.Click += async (_, _) => await AddProfile();
        var rename = new Button { Content = "Rename", Width = 74 };
        rename.Click += async (_, _) => await RenameProfile();
        var remove = new Button { Content = "Remove", Width = 74 };
        remove.Click += (_, _) => RemoveProfile();

        var profileBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(12),
            Children =
            {
                new TextBlock { Text = "Profile:", VerticalAlignment = VerticalAlignment.Center },
                _profileBox,
                add,
                rename,
                remove,
            },
        };

        RebuildHotkeyRows();

        var stack = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                profileBar,
                _hotkeyRows,
            },
        };

        return new ScrollViewer { Content = stack };
    }

    private Control BuildComparisonTab()
    {
        var chooser = new Button
        {
            Content = "Choose Comparisons...",
            Width = 180,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(12),
        };
        chooser.Click += async (_, _) =>
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

        return new StackPanel { Children = { chooser } };
    }

    private Control BuildRacingTab()
    {
        var raceViewer = new ComboBox
        {
            ItemsSource = new[] { "SpeedRunsLive", "MultiTwitch", "Speedrun.tv", "Kadgar" },
            SelectedItem = _settings.RaceViewer?.Name ?? "SpeedRunsLive",
            MinWidth = 180,
        };
        raceViewer.SelectionChanged += (_, _) =>
        {
            if (raceViewer.SelectedItem is string name)
            {
                _settings.RaceViewer = RaceViewer.FromName(name);
            }
        };

        var providers = new Button { Content = "Manage Race Providers...", Width = 190 };
        providers.Click += async (_, _) =>
        {
            var dlg = new RaceProviderManagingDialog(_settings.RaceProvider);
            await dlg.ShowDialogAsync(this);
        };

        var logout = new Button { Content = "Log Out Saved Accounts", Width = 190 };
        logout.Click += (_, _) => WebCredentials.DeleteAllCredentials();

        return new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 10,
            Children =
            {
                Row("Race viewer:", raceViewer),
                providers,
                logout,
            },
        };
    }

    private Control BuildGeneralTab()
    {
        Control settingsControl = AvaloniaSettingsBuilder.Build(new GeneralSettingsAdapter(_settings), "Settings");
        return new ScrollViewer { Content = settingsControl };
    }

    private void RebuildHotkeyRows()
    {
        _hotkeyRows.Children.Clear();
        if (string.IsNullOrEmpty(SelectedHotkeyProfile)
            || !_settings.HotkeyProfiles.TryGetValue(SelectedHotkeyProfile, out HotkeyProfile profile))
        {
            return;
        }

        _hotkeyRows.Children.Add(KeyRow("Start / Split:", () => profile.SplitKey, v => profile.SplitKey = v));
        _hotkeyRows.Children.Add(KeyRow("Reset:", () => profile.ResetKey, v => profile.ResetKey = v));
        _hotkeyRows.Children.Add(KeyRow("Skip Split:", () => profile.SkipKey, v => profile.SkipKey = v));
        _hotkeyRows.Children.Add(KeyRow("Undo Split:", () => profile.UndoKey, v => profile.UndoKey = v));
        _hotkeyRows.Children.Add(KeyRow("Pause:", () => profile.PauseKey, v => profile.PauseKey = v));
        _hotkeyRows.Children.Add(KeyRow("Toggle Global Hotkeys:", () => profile.ToggleGlobalHotkeys, v => profile.ToggleGlobalHotkeys = v));
        _hotkeyRows.Children.Add(KeyRow("Previous Comparison:", () => profile.SwitchComparisonPrevious, v => profile.SwitchComparisonPrevious = v));
        _hotkeyRows.Children.Add(KeyRow("Next Comparison:", () => profile.SwitchComparisonNext, v => profile.SwitchComparisonNext = v));
        _hotkeyRows.Children.Add(BoolRow("Global Hotkeys:", profile.GlobalHotkeysEnabled, v => profile.GlobalHotkeysEnabled = v));
        _hotkeyRows.Children.Add(BoolRow("Double Tap Prevention:", profile.DoubleTapPrevention, v => profile.DoubleTapPrevention = v));
        _hotkeyRows.Children.Add(BoolRow("Deactivate for Other Programs:", profile.DeactivateHotkeysForOtherPrograms, v => profile.DeactivateHotkeysForOtherPrograms = v));
        _hotkeyRows.Children.Add(NumberRow("Hotkey Delay:", profile.HotkeyDelay, v => profile.HotkeyDelay = v));
    }

    private async Task AddProfile()
    {
        string name = await new TextInputDialog("Add Hotkey Profile", "Profile name:").ShowDialogAsync(this);
        if (string.IsNullOrWhiteSpace(name) || _settings.HotkeyProfiles.ContainsKey(name))
        {
            return;
        }

        HotkeyProfile source = CurrentProfile() ?? _settings.HotkeyProfiles.Values.FirstOrDefault();
        _settings.HotkeyProfiles[name] = source != null
            ? (HotkeyProfile)source.Clone()
            : new HotkeyProfile();
        SelectedHotkeyProfile = name;
        RefreshProfiles();
    }

    private async Task RenameProfile()
    {
        if (CurrentProfile() is null || string.IsNullOrEmpty(SelectedHotkeyProfile))
        {
            return;
        }

        string oldName = SelectedHotkeyProfile;
        string name = await new TextInputDialog("Rename Hotkey Profile", "Profile name:", oldName).ShowDialogAsync(this);
        if (string.IsNullOrWhiteSpace(name) || name == oldName || _settings.HotkeyProfiles.ContainsKey(name))
        {
            return;
        }

        HotkeyProfile profile = _settings.HotkeyProfiles[oldName];
        _settings.HotkeyProfiles.Remove(oldName);
        _settings.HotkeyProfiles[name] = profile;
        SelectedHotkeyProfile = name;
        RefreshProfiles();
    }

    private void RemoveProfile()
    {
        if (_settings.HotkeyProfiles.Count <= 1 || string.IsNullOrEmpty(SelectedHotkeyProfile))
        {
            return;
        }

        _settings.HotkeyProfiles.Remove(SelectedHotkeyProfile);
        SelectedHotkeyProfile = _settings.HotkeyProfiles.Keys.First();
        RefreshProfiles();
    }

    private void RefreshProfiles()
    {
        _profileBox.ItemsSource = _settings.HotkeyProfiles.Keys.ToList();
        _profileBox.SelectedItem = SelectedHotkeyProfile;
        RebuildHotkeyRows();
    }

    private HotkeyProfile CurrentProfile()
    {
        return !string.IsNullOrEmpty(SelectedHotkeyProfile)
            && _settings.HotkeyProfiles.TryGetValue(SelectedHotkeyProfile, out HotkeyProfile profile)
                ? profile
                : null;
    }

    private static Control KeyRow(string label, Func<KeyOrButton> get, Action<KeyOrButton> set)
    {
        var button = new Button { Content = FormatKey(get()), Width = 150 };
        button.Click += (_, _) =>
        {
            button.Content = "Press key...";
            button.Focus();
        };
        button.KeyDown += (_, e) =>
        {
            LiveSplit.Model.Input.Key? mapped = ToLiveSplitKey(e.Key);
            if (mapped.HasValue)
            {
                set(new KeyOrButton(mapped.Value));
                button.Content = FormatKey(get());
                e.Handled = true;
            }
        };

        var clear = new Button { Content = "Clear", Width = 70 };
        clear.Click += (_, _) =>
        {
            set(null);
            button.Content = FormatKey(get());
        };

        return Row(label, new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { button, clear },
        });
    }

    private static Control BoolRow(string label, bool initial, Action<bool> set)
    {
        var box = new CheckBox { IsChecked = initial };
        box.IsCheckedChanged += (_, _) => set(box.IsChecked == true);
        return Row(label, box);
    }

    private static Control NumberRow(string label, float initial, Action<float> set)
    {
        var box = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 60,
            Increment = 0.1m,
            Value = (decimal)initial,
            Width = 90,
        };
        box.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue)
            {
                set((float)e.NewValue.Value);
            }
        };
        return Row(label, box);
    }

    private static StackPanel Row(string label, Control control)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(12, 0, 12, 0),
            Children =
            {
                new TextBlock { Text = label, Width = 180, VerticalAlignment = VerticalAlignment.Center },
                control,
            },
        };
    }

    private static string FormatKey(KeyOrButton binding)
        => binding?.ToString() ?? "";

    private static LiveSplit.Model.Input.Key? ToLiveSplitKey(global::Avalonia.Input.Key key)
    {
        if (Enum.TryParse(key.ToString(), out LiveSplit.Model.Input.Key parsed))
        {
            return parsed;
        }

        return key switch
        {
            global::Avalonia.Input.Key.D0 => LiveSplit.Model.Input.Key.D0,
            global::Avalonia.Input.Key.D1 => LiveSplit.Model.Input.Key.D1,
            global::Avalonia.Input.Key.D2 => LiveSplit.Model.Input.Key.D2,
            global::Avalonia.Input.Key.D3 => LiveSplit.Model.Input.Key.D3,
            global::Avalonia.Input.Key.D4 => LiveSplit.Model.Input.Key.D4,
            global::Avalonia.Input.Key.D5 => LiveSplit.Model.Input.Key.D5,
            global::Avalonia.Input.Key.D6 => LiveSplit.Model.Input.Key.D6,
            global::Avalonia.Input.Key.D7 => LiveSplit.Model.Input.Key.D7,
            global::Avalonia.Input.Key.D8 => LiveSplit.Model.Input.Key.D8,
            global::Avalonia.Input.Key.D9 => LiveSplit.Model.Input.Key.D9,
            global::Avalonia.Input.Key.Back => LiveSplit.Model.Input.Key.Back,
            _ => null,
        };
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

    private sealed class GeneralSettingsAdapter
    {
        private readonly ISettings _settings;

        public GeneralSettingsAdapter(ISettings settings)
        {
            _settings = settings;
        }

        public bool WarnOnReset { get => _settings.WarnOnReset; set => _settings.WarnOnReset = value; }
        public bool SimpleSumOfBest { get => _settings.SimpleSumOfBest; set => _settings.SimpleSumOfBest = value; }
        public int RefreshRate { get => _settings.RefreshRate; set => _settings.RefreshRate = value; }
        public int ServerPort { get => _settings.ServerPort; set => _settings.ServerPort = value; }
        public ServerStartupType ServerStartup { get => _settings.ServerStartup; set => _settings.ServerStartup = value; }
        public ServerStateType ServerState { get => _settings.ServerState; set => _settings.ServerState = value; }
        public bool UpdateCheckEnabled { get => _settings.UpdateCheckEnabled; set => _settings.UpdateCheckEnabled = value; }
        public bool EnableDPIAwareness { get => _settings.EnableDPIAwareness; set => _settings.EnableDPIAwareness = value; }
        public string UILanguage { get => _settings.UILanguage; set => _settings.UILanguage = value; }
        public int HcpHistorySize { get => _settings.HcpHistorySize; set => _settings.HcpHistorySize = value; }
        public int HcpNBestRuns { get => _settings.HcpNBestRuns; set => _settings.HcpNBestRuns = value; }
    }
}
