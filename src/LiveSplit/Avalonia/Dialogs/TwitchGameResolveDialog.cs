using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;

using LiveSplit.Options;
using LiveSplit.Web.Share;

namespace LiveSplit.Avalonia.Dialogs;

internal sealed class TwitchGameResolveDialog : Window
{
    private readonly Func<string, IEnumerable<Twitch.TwitchGame>> _findGame;
    private readonly TaskCompletionSource<ShareRunDialog.TwitchGameResolveResult> _result = new();
    private readonly TextBox _queryBox;
    private readonly ComboBox _gamesBox;
    private readonly TextBlock _statusBlock;

    public TwitchGameResolveDialog(string oldGame, Func<string, IEnumerable<Twitch.TwitchGame>> findGame)
    {
        _findGame = findGame ?? throw new ArgumentNullException(nameof(findGame));

        Title = "Game Not Found";
        Width = 372;
        Height = 170;
        MinWidth = 372;
        MinHeight = 170;
        CanResize = false;
        DialogTheme.ApplyWindow(this);

        _queryBox = new TextBox
        {
            Text = oldGame ?? string.Empty,
            Width = 240,
        };
        _gamesBox = new ComboBox
        {
            Width = 240,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _statusBlock = new TextBlock
        {
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap,
        };

        Content = BuildContent();
        Loaded += (_, _) => Search();
        Closed += (_, _) =>
        {
            if (!_result.Task.IsCompleted)
            {
                _result.TrySetResult(ShareRunDialog.TwitchGameResolveResult.Canceled());
            }
        };
    }

    public async Task<ShareRunDialog.TwitchGameResolveResult> ShowDialogAsync(Window owner)
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

    private Control BuildContent()
    {
        var search = new Button
        {
            Content = "Search",
            Width = 75,
        };
        search.Click += (_, _) => Search();

        var noGame = new Button
        {
            Content = "No Game",
            Width = 75,
        };
        noGame.Click += (_, _) => Complete(ShareRunDialog.TwitchGameResolveResult.NoGame());

        var ok = new Button
        {
            Content = "OK",
            Width = 75,
            IsDefault = true,
        };
        ok.Click += (_, _) =>
            Complete(ShareRunDialog.TwitchGameResolveResult.Selected(_gamesBox.SelectedItem as Twitch.TwitchGame));

        var cancel = new Button
        {
            Content = "Cancel",
            Width = 75,
            IsCancel = true,
        };
        cancel.Click += (_, _) => Complete(ShareRunDialog.TwitchGameResolveResult.Canceled());

        return new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "The game you're playing was not found on Twitch." },
                new TextBlock { Text = "Select a different game:" },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children = { _queryBox, search }
                },
                _gamesBox,
                _statusBlock,
                new Grid
                {
                    Margin = new Thickness(0, 4, 0, 0),
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto),
                        new ColumnDefinition(GridLength.Auto),
                        new ColumnDefinition(GridLength.Auto),
                    },
                    Children =
                    {
                        Place(noGame, 0),
                        Place(ok, 1),
                        Place(cancel, 2),
                    }
                }
            }
        };
    }

    private void Search()
    {
        try
        {
            List<Twitch.TwitchGame> games = _findGame(_queryBox.Text ?? string.Empty).ToList();
            _gamesBox.ItemsSource = games;
            _gamesBox.SelectedIndex = games.Count > 0 ? 0 : -1;
            _statusBlock.Text = games.Count == 0 ? "No matching Twitch games were found." : string.Empty;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            _gamesBox.ItemsSource = Array.Empty<Twitch.TwitchGame>();
            _gamesBox.SelectedIndex = -1;
            _statusBlock.Text = "Twitch game search failed.";
        }
    }

    private void Complete(ShareRunDialog.TwitchGameResolveResult result)
    {
        _result.TrySetResult(result);
        Close();
    }

    private static Control Place(Control control, int column)
    {
        control.Margin = new Thickness(column == 0 ? 0 : 6, 0, 0, 0);
        Grid.SetColumn(control, column);
        return control;
    }
}
