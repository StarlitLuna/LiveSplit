using global::Avalonia;

using LiveSplit.UI.Drawing;
using LiveSplit.UI.Drawing.Skia;

namespace LiveSplit.Avalonia;

/// <summary>
/// Builds an <see cref="AppBuilder"/> configured for the Skia desktop backend and registers
/// the Skia drawing factory so component rendering inside the Avalonia window goes through
/// <see cref="SkiaDrawingContext"/>.
/// </summary>
public static class AvaloniaProgram
{
    public static int Run(string[] args)
    {
        DrawingApi.Register(new SkiaDrawingFactory());

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
