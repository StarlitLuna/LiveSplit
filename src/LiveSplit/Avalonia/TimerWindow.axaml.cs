using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;

namespace LiveSplit.Avalonia;

public sealed partial class TimerWindow : Window
{
    public TimerWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
