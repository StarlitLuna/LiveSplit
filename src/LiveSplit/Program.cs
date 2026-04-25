using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using LiveSplit.Avalonia;
using LiveSplit.Localization;
using LiveSplit.UI.Drawing;
using LiveSplit.UI.Drawing.GdiPlus;
using LiveSplit.View;

namespace LiveSplit;

internal static class Program
{
    /// <summary>
    /// Der Haupteinstiegspunkt für die Anwendung.
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        // Use the Avalonia front-end on non-Windows platforms (the WinForms TimerForm path
        // hard-depends on System.Drawing.Common's GDI+ bindings + Win32 P/Invokes for layered
        // window transparency, which only work on Windows). On Windows the user can still opt in
        // explicitly with `--avalonia` to exercise the cross-platform path during development.
        // Phase 5.3d migrates the dialogs and lets Avalonia become the only path; until then,
        // Windows defaults to the legacy WinForms front-end so the layout/run editor dialogs
        // remain available.
        if (args.Contains("--avalonia") || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            InitializeLocalization();
            AvaloniaProgram.Run(args);
            return;
        }

        try
        {
            InitializeLocalization();
            DrawingApi.Register(new GdiPlusDrawingFactory());
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Environment.CurrentDirectory = Path.GetDirectoryName(Application.ExecutablePath);

#if !DEBUG
            Options.FiletypeRegistryHelper.RegisterFileFormatsIfNotAlreadyRegistered();
#endif

            string splitsPath = null;
            string layoutPath = null;

            for (int i = 0; i < args.Length; ++i)
            {
                if (args[i] == "-s")
                {
                    splitsPath = args[++i];
                }
                else if (args[i] == "-l")
                {
                    layoutPath = args[++i];
                }
            }

            Application.Run(new TimerForm(splitsPath: splitsPath, layoutPath: layoutPath));
        }
#if !DEBUG
        catch (Exception e)
        {
            Options.Log.Error(e);
            string message = string.Format(
                UiLocalizer.TranslateKey(LocalizationKeys.CrashReason, "LiveSplit has crashed due to the following reason:\n\n{0}"),
                e.Message);
            MessageBox.Show(message, UiLocalizer.TranslateKey(LocalizationKeys.Error, "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
#endif
        finally
        {
        }
    }

    private static void InitializeLocalization()
    {
        try
        {
            UiTextCatalog.Initialize(Path.GetDirectoryName(Application.ExecutablePath) ?? string.Empty);
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
        }
    }
}
