using System.Threading.Tasks;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

namespace LiveSplit.Avalonia.Dialogs;

public sealed class AuthenticationDialog : Window
{
    public string Username { get; set; }
    public string Password { get; set; }
    public bool RememberPassword { get; set; }

    private readonly TaskCompletionSource<bool> _result = new();

    public AuthenticationDialog()
    {
        Title = "Authentication";
        Width = 270;
        Height = 180;
        CanResize = false;
        DialogTheme.ApplyWindow(this);

        var userBox = new TextBox();
        var passBox = new TextBox { PasswordChar = '*' };
        var rememberBox = new CheckBox { Content = "Remember Password" };

        var ok = new Button { Content = "OK", Width = 80, IsDefault = true };
        ok.Click += (_, _) =>
        {
            Username = userBox.Text;
            Password = passBox.Text;
            RememberPassword = rememberBox.IsChecked == true;
            _result.TrySetResult(true);
            Close();
        };
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        cancel.Click += (_, _) => { _result.TrySetResult(false); Close(); };

        Opened += (_, _) =>
        {
            userBox.Text = Username;
            passBox.Text = Password;
            rememberBox.IsChecked = RememberPassword;
            userBox.Focus();
        };

        var grid = new Grid
        {
            Margin = new Thickness(7),
            ColumnDefinitions = new ColumnDefinitions("74,*,81"),
            RowDefinitions = new RowDefinitions("29,29,*,29"),
        };
        Add(grid, new TextBlock { Text = "Username:", VerticalAlignment = VerticalAlignment.Center }, 0, 0);
        Add(grid, userBox, 0, 1, columnSpan: 2);
        Add(grid, new TextBlock { Text = "Password:", VerticalAlignment = VerticalAlignment.Center }, 1, 0);
        Add(grid, passBox, 1, 1, columnSpan: 2);
        Add(grid, rememberBox, 2, 1, columnSpan: 2);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { ok, cancel },
        };
        Add(grid, buttons, 3, 1, columnSpan: 2);

        Content = grid;

        Closed += (_, _) =>
        {
            if (!_result.Task.IsCompleted)
            {
                _result.TrySetResult(false);
            }
        };
    }

    private static void Add(Grid grid, Control control, int row, int column, int columnSpan = 1)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        if (columnSpan > 1)
        {
            Grid.SetColumnSpan(control, columnSpan);
        }

        grid.Children.Add(control);
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
