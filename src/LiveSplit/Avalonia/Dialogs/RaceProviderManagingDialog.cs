using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

using LiveSplit.Options;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>
/// Lists configured race providers (e.g. SRL) and lets the user toggle which ones are enabled.
/// Provider-specific settings panels are WinForms-based (<see cref="RaceProviderSettings.GetSettingsControl"/>)
/// and are not surfaced here; enabling a provider applies the defaults defined by the plugin.
/// </summary>
public sealed class RaceProviderManagingDialog : Window
{
    private readonly TaskCompletionSource<bool> _result = new();
    private readonly ISettings _settings;

    public RaceProviderManagingDialog(ISettings settings)
    {
        _settings = settings;
        Title = "Race Providers";
        Width = 460;
        Height = 320;
        CanResize = true;

        var stack = new StackPanel { Margin = new Thickness(20), Spacing = 4 };
        if (_settings?.RaceProvider == null || _settings.RaceProvider.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "No race providers are configured.",
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            });
        }
        else
        {
            foreach (RaceProviderSettings provider in _settings.RaceProvider)
            {
                var captured = provider;
                var checkbox = new CheckBox
                {
                    Content = string.IsNullOrEmpty(provider.DisplayName) ? provider.Name : provider.DisplayName,
                    IsChecked = provider.Enabled,
                };
                checkbox.IsCheckedChanged += (_, _) => captured.Enabled = checkbox.IsChecked == true;
                stack.Children.Add(checkbox);
            }
        }

        var close = new Button { Content = "Close", Width = 80, IsDefault = true, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 12, 12) };
        close.Click += (_, _) => { _result.TrySetResult(true); Close(); };

        var scroll = new ScrollViewer { Content = stack };

        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(close, Dock.Bottom);
        root.Children.Add(close);
        root.Children.Add(scroll);
        Content = root;

        Closed += (_, _) =>
        {
            if (!_result.Task.IsCompleted)
            {
                _result.TrySetResult(false);
            }
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
}
