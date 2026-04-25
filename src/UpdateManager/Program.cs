using System;
using System.Collections.Generic;

namespace UpdateManager;

/// <summary>
/// The original UpdateManager was a WinForms-hosted updater (UpdateForm) that polled remote
/// XML manifests for new builds. The linux-port shipped through Flatpak/AppImage handles updates
/// out-of-band, so the GUI is gone — but the entry point and CLI argument parsing stay so any
/// hard-coded callers don't fail to launch the binary. A future net8.0 update flow can plug back
/// in here with an Avalonia <c>UpdateWindow</c> + <see cref="Updater"/>.
/// </summary>
internal static class Program
{
    private static void Main(string[] args)
    {
        try
        {
            if (args.Length >= 3)
            {
                List<IUpdateable> updateables = [];
                for (int i = 0; i + 2 < args.Length; i += 3)
                {
                    updateables.Add(new Updateable(args[i], args[i + 1], Version.Parse(args[i + 2])));
                }

                // No-op: the WinForms UpdateForm is gone on the linux-port. Construct the
                // Updateables to keep the parsing path warm in case a CLI consumer relies on
                // the binary returning successfully.
            }
        }
        catch (Exception) { }
    }
}
