using global::Avalonia;

using System;
using System.IO;

using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
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
            .With(new Win32PlatformOptions
            {
                DpiAwareness = GetWin32DpiAwareness(LoadEnableDpiAwarenessPreference()),
            })
            .LogToTrace();
    }

    internal static string GetWin32DpiAwarenessName(bool enableDpiAwareness)
        => GetWin32DpiAwareness(enableDpiAwareness).ToString();

    private static Win32DpiAwareness GetWin32DpiAwareness(bool enableDpiAwareness)
        => enableDpiAwareness ? Win32DpiAwareness.SystemDpiAware : Win32DpiAwareness.Unaware;

    private static bool LoadEnableDpiAwarenessPreference()
    {
        try
        {
            string path = UserDataPaths.SettingsFile;
            if (File.Exists(path))
            {
                using FileStream stream = File.OpenRead(path);
                return new XMLSettingsFactory(stream).Create().EnableDPIAwareness;
            }
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
        }

        return new StandardSettingsFactory().Create().EnableDPIAwareness;
    }
}
