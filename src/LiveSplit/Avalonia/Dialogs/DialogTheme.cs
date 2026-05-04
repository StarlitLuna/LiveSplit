using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Styling;

namespace LiveSplit.Avalonia.Dialogs;

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
    public static IBrush LinkBrush { get; } = new SolidColorBrush(Color.Parse("#66B7FF"));

    public static void ApplyWindow(Window window)
    {
        if (window is null)
        {
            return;
        }

        window.RequestedThemeVariant = ThemeVariant.Dark;
        window.Background = WindowBackgroundBrush;
        window.FontSize = 12;
    }

    public static void Apply(Control control)
    {
        switch (control)
        {
            case TextBlock text:
                text.Foreground = TextBrush;
                text.FontSize = 12;
                break;
            case Button button:
                button.Foreground = TextBrush;
                button.Background = ButtonBackgroundBrush;
                button.BorderBrush = ControlBorderBrush;
                button.BorderThickness = new Thickness(1);
                button.FontSize = 12;
                break;
            case TextBox textBox:
                textBox.Foreground = TextBrush;
                textBox.Background = ControlBackgroundBrush;
                textBox.BorderBrush = ControlBorderBrush;
                textBox.BorderThickness = new Thickness(1);
                textBox.FontSize = 12;
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
