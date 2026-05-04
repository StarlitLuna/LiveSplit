using System;
using System.Runtime.InteropServices;

using LiveSplit.Avalonia;
using LiveSplit.Localization;
using LiveSplit.Register;

namespace LiveSplit;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        InitializeLocalization();
        StartupOptions.Parse(args);
        if (StartupOptions.SmokeTest)
        {
            return SmokeTestRunner.Run(new SmokeTestOptions
            {
                SplitsPath = StartupOptions.SplitsPath,
                LayoutPath = StartupOptions.LayoutPath
            });
        }

        RegisterWindowsFileFormatsIfNeeded();
        return AvaloniaProgram.Run(args);
    }

    private static void RegisterWindowsFileFormatsIfNeeded()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        try
        {
            FiletypeRegistryHelper.RegisterFileFormatsIfNotAlreadyRegistered();
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
        }
    }

    private static void InitializeLocalization()
    {
        try
        {
            UiTextCatalog.Initialize(AppContext.BaseDirectory);
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
        }
    }
}
