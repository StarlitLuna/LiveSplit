using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

namespace LiveSplit.Avalonia.Dialogs;

/// <summary>
/// Avalonia counterpart of <c>NewRaceInputBox</c>. Asks the user for a game name + category
/// when starting a new race. The WinForms version autocompletes from the SpeedRunsLive game
/// list — the Avalonia version ships without autocomplete because the web APIs aren't wired
/// through the Avalonia credential store yet.
/// </summary>
public sealed class NewRaceInputBox : Window
{
    public string Game { get; private set; }
    public string Category { get; private set; }

    private readonly TaskCompletionSource<bool> _result = new();

    public NewRaceInputBox(string initialGame = "", string initialCategory = "")
    {
        Title = "New Race";
        Width = 420;
        Height = 240;
        CanResize = false;

        var gameBox = new TextBox { Text = initialGame };
        var categoryBox = new TextBox { Text = initialCategory };

        var ok = new Button { Content = "OK", Width = 80, IsDefault = true };
        ok.Click += (_, _) =>
        {
            Game = gameBox.Text ?? string.Empty;
            Category = categoryBox.Text ?? string.Empty;
            _result.TrySetResult(true);
            Close();
        };
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        cancel.Click += (_, _) => { _result.TrySetResult(false); Close(); };

        Opened += (_, _) => gameBox.Focus();

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Game:" },
                gameBox,
                new TextBlock { Text = "Category:" },
                categoryBox,
                new TextBlock
                {
                    Text = "Creating a race without any opponents is against the rules.",
                    Foreground = global::Avalonia.Media.Brushes.Gray,
                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 0),
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Margin = new Thickness(0, 12, 0, 0),
                    Children = { cancel, ok },
                },
            },
        };

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
