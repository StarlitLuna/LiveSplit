using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;

using LiveSplit.Options;

namespace LiveSplit.Avalonia.Dialogs;

public sealed class RaceProviderManagingDialog : Window
{
    private readonly RaceProviderSettingsEditorModel _model;
    private readonly TaskCompletionSource<bool> _result = new();
    private readonly ListBox _providerList;
    private readonly CheckBox _enabledBox;
    private readonly TextBlock _providerTitle;
    private readonly Button _websiteButton;
    private readonly Button _rulesButton;
    private readonly ContentControl _settingsHost;

    public RaceProviderManagingDialog(IList<RaceProviderSettings> settings)
    {
        _model = new RaceProviderSettingsEditorModel(settings);

        Title = "Race Providers";
        Width = 640;
        Height = 460;
        MinWidth = 520;
        MinHeight = 360;

        _providerList = new ListBox
        {
            ItemsSource = _model.WorkingSettings.Select(x => x.DisplayName).ToList(),
            Width = 200
        };
        _providerList.SelectionChanged += (_, _) => RefreshSelectedProvider();

        _providerTitle = new TextBlock
        {
            FontWeight = FontWeight.Bold,
            FontSize = 16
        };

        _enabledBox = new CheckBox { Content = "Enabled" };
        _enabledBox.IsCheckedChanged += (_, _) =>
        {
            _model.SetEnabled(_providerList.SelectedIndex, _enabledBox.IsChecked == true);
        };

        _websiteButton = new Button { Content = "Website" };
        _websiteButton.Click += (_, _) => OpenLink(SelectedProvider?.WebsiteLink);
        _rulesButton = new Button { Content = "Rules" };
        _rulesButton.Click += (_, _) => OpenLink(SelectedProvider?.RulesLink);

        _settingsHost = new ContentControl();

        var providerActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _enabledBox, _websiteButton, _rulesButton }
        };

        var details = new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 10,
            Children =
            {
                _providerTitle,
                providerActions,
                _settingsHost
            }
        };

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("220,*")
        };
        Grid.SetColumn(_providerList, 0);
        Grid.SetColumn(details, 1);
        body.Children.Add(_providerList);
        body.Children.Add(details);

        var ok = new Button { Content = "OK", Width = 80, IsDefault = true };
        ok.Click += (_, _) =>
        {
            _model.Apply();
            _result.TrySetResult(true);
            Close();
        };
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        cancel.Click += (_, _) =>
        {
            _result.TrySetResult(false);
            Close();
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 0, 12, 12),
            Children = { cancel, ok }
        };

        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(body);
        Content = root;

        if (_model.WorkingSettings.Count > 0)
        {
            _providerList.SelectedIndex = 0;
        }
        else
        {
            RefreshSelectedProvider();
        }

        Closed += (_, _) =>
        {
            if (!_result.Task.IsCompleted)
            {
                _result.TrySetResult(false);
            }
        };
    }

    private RaceProviderSettings SelectedProvider
    {
        get
        {
            int index = _providerList.SelectedIndex;
            return index >= 0 && index < _model.WorkingSettings.Count
                ? _model.WorkingSettings[index]
                : null;
        }
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

    private void RefreshSelectedProvider()
    {
        RaceProviderSettings settings = SelectedProvider;
        if (settings is null)
        {
            _providerTitle.Text = "No race providers are available.";
            _enabledBox.IsEnabled = false;
            _websiteButton.IsEnabled = false;
            _rulesButton.IsEnabled = false;
            _settingsHost.Content = null;
            return;
        }

        _providerTitle.Text = settings.DisplayName;
        _enabledBox.IsEnabled = true;
        _enabledBox.IsChecked = settings.Enabled;
        _websiteButton.IsEnabled = !string.IsNullOrEmpty(settings.WebsiteLink);
        _rulesButton.IsEnabled = !string.IsNullOrEmpty(settings.RulesLink);
        _settingsHost.Content = settings.GetSettingsControl();
    }

    private static void OpenLink(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }
}
