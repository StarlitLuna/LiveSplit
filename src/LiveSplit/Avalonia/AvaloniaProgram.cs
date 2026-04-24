using global::Avalonia;

using LiveSplit.UI.Drawing;
using LiveSplit.UI.Drawing.Skia;

namespace LiveSplit.Avalonia;

/// <summary>
/// Alternate entry point used when LiveSplit is launched with the <c>--avalonia</c> flag
/// (Phase 5.2b vertical slice). Builds an <see cref="AppBuilder"/> configured for the Skia
/// desktop backend and registers the Skia drawing factory so any component rendering that
/// happens inside the Avalonia window goes through <see cref="SkiaDrawingContext"/>.
/// </summary>
public static class AvaloniaProgram
{
    public static int Run(string[] args)
    {
        // Route all component rendering through the Skia backing while Avalonia owns the UI.
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
