namespace LiveSplit.Avalonia;

/// <summary>
/// Parsed command-line arguments handed off from <see cref="Program.Main"/> to the Avalonia
/// app shell. <see cref="App.OnFrameworkInitializationCompleted"/> reads these to construct
/// the <see cref="TimerWindow"/> with the user's requested splits / layout files.
/// </summary>
public static class StartupOptions
{
    public static string SplitsPath { get; set; }
    public static string LayoutPath { get; set; }

    public static void Parse(string[] args)
    {
        SplitsPath = null;
        LayoutPath = null;

        if (args == null)
        {
            return;
        }

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-s" && i + 1 < args.Length)
            {
                SplitsPath = args[++i];
            }
            else if (args[i] == "-l" && i + 1 < args.Length)
            {
                LayoutPath = args[++i];
            }
        }
    }
}
