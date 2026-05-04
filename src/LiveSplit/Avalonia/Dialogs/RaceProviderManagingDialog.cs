using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private readonly TextBlock _websiteLabel;
    private readonly TextBlock _rulesLabel;
    private readonly Button _websiteLink;
    private readonly Button _rulesLink;
    private readonly ContentControl _settingsHost;

    public RaceProviderManagingDialog(IList<RaceProviderSettings> settings)
    {
        _model = new RaceProviderSettingsEditorModel(settings);

        Title = "Manage Racing Services";
        Width = 450;
        Height = 260;
        MinWidth = 450;
        MinHeight = 230;
        DialogTheme.ApplyWindow(this);

        _providerList = new ListBox
        {
            Width = 144,
            Margin = new Thickness(3)
        };
        _providerList.SelectionChanged += (_, _) => RefreshSelectedProvider();
        for (int index = 0; index < _model.WorkingSettings.Count; index++)
        {
            RaceProviderSettings provider = _model.WorkingSettings[index];
            var providerBox = new CheckBox
            {
                Content = provider.DisplayName,
                IsChecked = provider.Enabled,
                Tag = index,
                Margin = new Thickness(2)
            };
            providerBox.IsCheckedChanged += (_, _) =>
            {
                if (providerBox.Tag is int providerIndex)
                {
                    _model.SetEnabled(providerIndex, providerBox.IsChecked == true);
                }
            };
            _providerList.Items.Add(providerBox);
        }

        _websiteLabel = DialogLabel("Website:");
        _rulesLabel = DialogLabel("Rules:");
        _websiteLink = LinkButton();
        _websiteLink.Click += (_, _) => OpenLink(SelectedProvider?.WebsiteLink);
        _rulesLink = LinkButton();
        _rulesLink.Click += (_, _) => OpenLink(SelectedProvider?.RulesLink);

        _settingsHost = new ContentControl();

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("150,*"),
            RowDefinitions = new RowDefinitions("40,*,32")
        };
        Grid.SetColumn(_providerList, 0);
        Grid.SetRowSpan(_providerList, 3);
        body.Children.Add(_providerList);

        var links = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("*,*"),
            Margin = new Thickness(3)
        };
        AddLinkRow(links, _websiteLabel, _websiteLink, 0);
        AddLinkRow(links, _rulesLabel, _rulesLink, 1);
        Grid.SetColumn(links, 1);
        Grid.SetRow(links, 0);
        body.Children.Add(links);

        _settingsHost.Margin = new Thickness(3);
        Grid.SetColumn(_settingsHost, 1);
        Grid.SetRow(_settingsHost, 1);
        body.Children.Add(_settingsHost);

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
            Margin = new Thickness(0, 3, 3, 3),
            Children = { ok, cancel }
        };
        Grid.SetColumn(buttons, 1);
        Grid.SetRow(buttons, 2);
        body.Children.Add(buttons);
        Content = body;

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
            _websiteLabel.IsVisible = false;
            _rulesLabel.IsVisible = false;
            _websiteLink.IsVisible = false;
            _rulesLink.IsVisible = false;
            _settingsHost.Content = null;
            return;
        }

        ConfigureLink(_websiteLabel, _websiteLink, settings.WebsiteLink);
        ConfigureLink(_rulesLabel, _rulesLink, settings.RulesLink);
        _settingsHost.Content = settings.GetSettingsControl();
    }

    private static TextBlock DialogLabel(string text)
    {
        var label = new TextBlock
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        DialogTheme.Apply(label);
        return label;
    }

    private static Button LinkButton()
    {
        return new Button
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = DialogTheme.LinkBrush,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private static void AddLinkRow(Grid grid, TextBlock label, Button link, int row)
    {
        Grid.SetColumn(label, 0);
        Grid.SetRow(label, row);
        Grid.SetColumn(link, 1);
        Grid.SetRow(link, row);
        grid.Children.Add(label);
        grid.Children.Add(link);
    }

    private static void ConfigureLink(TextBlock label, Button link, string url)
    {
        bool visible = !string.IsNullOrEmpty(url);
        label.IsVisible = visible;
        link.IsVisible = visible;
        link.Content = url ?? string.Empty;
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
