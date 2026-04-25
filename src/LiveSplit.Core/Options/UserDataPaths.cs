using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LiveSplit.Options;

/// <summary>
/// Per-platform locations for LiveSplit's user-config data. On Windows the legacy
/// "next to the executable" layout is kept so existing installs continue to find their
/// <c>settings.cfg</c>. On Linux the XDG convention applies — files live under
/// <c>${XDG_CONFIG_HOME:-~/.config}/LiveSplit/</c>.
/// </summary>
public static class UserDataPaths
{
    private const string AppFolder = "LiveSplit";
    private const string SettingsFileName = "settings.cfg";

    /// <summary>
    /// Directory containing the running assembly. Used as the base for plugin lookup
    /// (<c>ComponentManager.BasePath</c>) and for the legacy Windows settings location.
    /// </summary>
    public static string ExecutableDir => AppContext.BaseDirectory;

    /// <summary>
    /// Directory that holds <see cref="SettingsFile"/> and any future user-config blobs.
    /// Created on demand.
    /// </summary>
    public static string ConfigDir
    {
        get
        {
            string dir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? ExecutableDir
                : LinuxConfigDir();

            try
            {
                Directory.CreateDirectory(dir);
            }
            catch
            {
                // Creation failure (read-only fs, permissions). Callers fall back to
                // in-memory defaults.
            }

            return dir;
        }
    }

    /// <summary>Full path to <c>settings.cfg</c> for the current platform.</summary>
    public static string SettingsFile => Path.Combine(ConfigDir, SettingsFileName);

    private static string LinuxConfigDir()
    {
        string xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrEmpty(xdgConfig))
        {
            string home = Environment.GetEnvironmentVariable("HOME") ?? "/tmp";
            xdgConfig = Path.Combine(home, ".config");
        }

        return Path.Combine(xdgConfig, AppFolder);
    }
}
