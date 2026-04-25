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
        Width = 360;
        Height = 220;
        CanResize = false;

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

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Username:" },
                userBox,
                new TextBlock { Text = "Password:" },
                passBox,
                rememberBox,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Margin = new Thickness(0, 8, 0, 0),
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
