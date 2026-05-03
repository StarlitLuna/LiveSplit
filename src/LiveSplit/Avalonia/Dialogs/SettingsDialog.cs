using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;

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
    private readonly StackPanel _hotkeyRows;
    private readonly Button _removeProfile;

    public string SelectedHotkeyProfile { get; private set; }

    public SettingsDialog(ISettings settings, string selectedHotkeyProfile = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        SelectedHotkeyProfile = !string.IsNullOrEmpty(selectedHotkeyProfile)
            && settings.HotkeyProfiles.ContainsKey(selectedHotkeyProfile)
                ? selectedHotkeyProfile
                : settings.HotkeyProfiles.FirstOrDefault().Key;

        Title = "Settings";
        Width = 520;
        Height = 820;
        MinWidth = 458;
        MinHeight = 360;

        _profileBox = new ComboBox
        {
            MinWidth = 220,
            ItemsSource = settings.HotkeyProfiles.Keys.ToList(),
            SelectedItem = SelectedHotkeyProfile,
        };
        _profileBox.SelectionChanged += (_, _) =>
        {
            SelectedHotkeyProfile = _profileBox.SelectedItem as string ?? SelectedHotkeyProfile;
            RebuildHotkeyRows();
            RefreshRemoveButton();
        };

        _hotkeyRows = new StackPanel { Spacing = 6 };
        _removeProfile = new Button { Content = "Remove", Width = 90 };
        _removeProfile.Click += (_, _) => RemoveProfile();

        var ok = new Button { Content = "OK", Width = 90, IsDefault = true };
        ok.Click += (_, _) => { _result.TrySetResult(true); Close(); };
        var cancel = new Button { Content = "Cancel", Width = 90, IsCancel = true };
        cancel.Click += (_, _) => { _result.TrySetResult(false); Close(); };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 8, 12, 12),
            Children = { ok, cancel },
        };

        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(new ScrollViewer { Content = BuildSettingsSurface() });
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

        return new StackPanel
        {
            Margin = new Thickness(14, 12, 14, 8),
            Spacing = 9,
            Children =
            {
                Heading("Hotkeys"),
                _hotkeyRows,
                BuildProfileSection(),
                BuildGeneralRows(),
                BuildServerRows(),
            },
        };
    }

    private Control BuildProfileSection()
    {
        var add = new Button { Content = "New", Width = 90 };
        add.Click += async (_, _) => await AddProfile();
        var rename = new Button { Content = "Rename", Width = 90 };
        rename.Click += async (_, _) => await RenameProfile();

        return new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(0, 8, 0, 0),
            Children =
            {
                Heading("Hotkey Profiles"),
                Row("Active Hotkey Profile:", _profileBox),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { add, rename, _removeProfile },
                },
            },
        };
    }

    private Control BuildGeneralRows()
    {
        var simpleSob = new CheckBox { Content = "Simple Sum of Best Calculation", IsChecked = _settings.SimpleSumOfBest };
        simpleSob.IsCheckedChanged += (_, _) => _settings.SimpleSumOfBest = simpleSob.IsChecked == true;

        var warnReset = new CheckBox { Content = "Warn On Reset If Better Times", IsChecked = _settings.WarnOnReset };
        warnReset.IsCheckedChanged += (_, _) => _settings.WarnOnReset = warnReset.IsChecked == true;

        var raceViewer = new ComboBox
        {
            ItemsSource = new[] { "SpeedRunsLive", "MultiTwitch", "Kadgar", "Speedrun.tv" },
            SelectedItem = _settings.RaceViewer?.Name ?? "SpeedRunsLive",
            MinWidth = 220,
        };
        raceViewer.SelectionChanged += (_, _) =>
        {
            if (raceViewer.SelectedItem is string name)
            {
                _settings.RaceViewer = RaceViewer.FromName(name);
            }
        };

        var providers = new Button { Content = "Manage Racing Services...", MinWidth = 220 };
        providers.Click += async (_, _) =>
        {
            var edited = _settings.RaceProvider.Select(x => (RaceProviderSettings)x.Clone()).ToList();
            var dlg = new RaceProviderManagingDialog(edited);
            if (await dlg.ShowDialogAsync(this))
            {
                _settings.RaceProvider = edited;
            }
        };

        var comparisons = new Button { Content = "Choose Active Comparisons...", MinWidth = 220 };
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

        var logout = new Button
        {
            Content = "Log Out of All Accounts",
            MinWidth = 220,
            IsEnabled = WebCredentials.AnyCredentialsExist(),
        };
        logout.Click += (_, _) =>
        {
            SpeedrunCom.ClearAccessToken();
            Twitch.Instance.ClearAccessToken();
            WebCredentials.DeleteAllCredentials();
            logout.IsEnabled = WebCredentials.AnyCredentialsExist();
        };

        return new StackPanel
        {
            Spacing = 7,
            Margin = new Thickness(0, 8, 0, 0),
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 16,
                    Children = { simpleSob, warnReset },
                },
                Row("Race Viewer:", raceViewer),
                Row("Racing Services:", providers),
                Row("Active Comparisons:", comparisons),
                Row("Saved Accounts:", logout),
            },
        };
    }

    private Control BuildServerRows()
    {
        var serverPort = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 65535,
            Increment = 1,
            Value = _settings.ServerPort,
            Width = 120,
        };
        serverPort.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue)
            {
                _settings.ServerPort = (int)e.NewValue.Value;
            }
        };

        var startup = new ComboBox
        {
            ItemsSource = new[]
            {
                "Don't start the Server",
                "Start TCP Server",
                "Start Websocket Server",
                "Restore Previous State",
            },
            SelectedIndex = (int)_settings.ServerStartup,
            MinWidth = 220,
        };
        startup.SelectionChanged += (_, _) =>
        {
            if (Enum.IsDefined(typeof(ServerStartupType), startup.SelectedIndex))
            {
                _settings.ServerStartup = (ServerStartupType)startup.SelectedIndex;
            }
        };

        var refresh = new NumericUpDown
        {
            Minimum = 20,
            Maximum = 300,
            Increment = 1,
            Value = _settings.RefreshRate,
            Width = 120,
        };
        refresh.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue)
            {
                _settings.RefreshRate = Math.Min(Math.Max((int)e.NewValue.Value, 20), 300);
            }
        };

        return new StackPanel
        {
            Spacing = 7,
            Margin = new Thickness(0, 8, 0, 0),
            Children =
            {
                Heading("LiveSplit Server"),
                Row("Server Port:", serverPort),
                Row("Startup Behavior:", startup),
                Row("Refresh Rate (Hz):", refresh),
            },
        };
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
        _hotkeyRows.Children.Add(KeyRow("Undo Split:", () => profile.UndoKey, v => profile.UndoKey = v));
        _hotkeyRows.Children.Add(KeyRow("Skip Split:", () => profile.SkipKey, v => profile.SkipKey = v));
        _hotkeyRows.Children.Add(KeyRow("Pause:", () => profile.PauseKey, v => profile.PauseKey = v));
        _hotkeyRows.Children.Add(KeyRow("Switch Comparison (Previous):", () => profile.SwitchComparisonPrevious, v => profile.SwitchComparisonPrevious = v));
        _hotkeyRows.Children.Add(KeyRow("Switch Comparison (Next):", () => profile.SwitchComparisonNext, v => profile.SwitchComparisonNext = v));
        _hotkeyRows.Children.Add(KeyRow("Toggle Global Hotkeys:", () => profile.ToggleGlobalHotkeys, v => profile.ToggleGlobalHotkeys = v));

        var deactivate = new CheckBox { Content = "Deactivate For Other Programs", IsChecked = profile.DeactivateHotkeysForOtherPrograms };
        deactivate.IsCheckedChanged += (_, _) =>
        {
            if (deactivate.IsEnabled)
            {
                profile.DeactivateHotkeysForOtherPrograms = deactivate.IsChecked == true;
            }
        };

        var global = new CheckBox { Content = "Global Hotkeys", IsChecked = profile.GlobalHotkeysEnabled };
        global.IsCheckedChanged += (_, _) =>
        {
            profile.GlobalHotkeysEnabled = global.IsChecked == true;
            RefreshDeactivateHotkeysCheckbox(profile, deactivate);
        };
        _hotkeyRows.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Children = { global, deactivate },
        });
        RefreshDeactivateHotkeysCheckbox(profile, deactivate);

        var doubleTap = new CheckBox { Content = "Double Tap Prevention", IsChecked = profile.DoubleTapPrevention };
        doubleTap.IsCheckedChanged += (_, _) => profile.DoubleTapPrevention = doubleTap.IsChecked == true;

        var delay = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 60,
            Increment = 0.1m,
            Value = (decimal)profile.HotkeyDelay,
            Width = 80,
        };
        delay.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue)
            {
                profile.HotkeyDelay = Math.Max((float)e.NewValue.Value, 0f);
            }
        };

        _hotkeyRows.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Children =
            {
                doubleTap,
                new TextBlock { Text = "Hotkey Delay (Seconds):", VerticalAlignment = VerticalAlignment.Center },
                delay,
            },
        });

        var allowGamepads = new CheckBox
        {
            Content = "Allow Gamepads as Hotkeys",
            IsChecked = profile.AllowGamepadsAsHotkeys,
            IsEnabled = false,
        };
        allowGamepads.IsCheckedChanged += (_, _) => profile.AllowGamepadsAsHotkeys = allowGamepads.IsChecked == true;

        var dpiAware = new CheckBox { Content = "Enable DPI Aware", IsChecked = _settings.EnableDPIAwareness };
        dpiAware.IsCheckedChanged += (_, _) => _settings.EnableDPIAwareness = dpiAware.IsChecked == true;

        _hotkeyRows.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Children = { allowGamepads, dpiAware },
        });
    }

    private static void RefreshDeactivateHotkeysCheckbox(HotkeyProfile profile, CheckBox deactivate)
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

    private static Control KeyRow(string label, Func<KeyOrButton> get, Action<KeyOrButton> set)
    {
        var box = new TextBox
        {
            Text = FormatKey(get()),
            Width = 230,
            IsReadOnly = true,
        };

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

        return Row(label, box);
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

    private static StackPanel Row(string label, Control control)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = label, Width = 205, VerticalAlignment = VerticalAlignment.Center },
                control,
            },
        };
    }

    private static TextBlock Heading(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 4, 0, 2),
        };
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
