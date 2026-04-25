using System;

using LiveSplit.Avalonia;
using LiveSplit.Localization;

namespace LiveSplit;

internal static class Program
{
    /// <summary>
    /// Main entry point. Delegates to the Avalonia bootstrap. The WinForms TimerForm +
    /// supporting dialogs were retired at the end of Phase 5 — see
    /// <c>src/LiveSplit/Avalonia/</c> for the runtime UI.
    /// </summary>
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
