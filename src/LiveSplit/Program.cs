using System;

using LiveSplit.Avalonia;
using LiveSplit.Localization;

namespace LiveSplit;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        InitializeLocalization();
        return AvaloniaProgram.Run(args);
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
