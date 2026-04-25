using System;
using System.Collections.Generic;

using LiveSplit.Options;

namespace LiveSplit.Updates;

/// <summary>
/// The original UpdateHelper polled for new builds and prompted via a WinForms
/// <c>ScrollableMessageBox</c>. The linux-port ships through Flatpak/AppImage where the host
/// package manager handles updates, so the prompt path is gone — only the version-string
/// helpers stay (used by the user-agent and by GitInfo display in About dialogs).
/// </summary>
public static class UpdateHelper
{
    public static readonly Version Version = GetVersionFromGit();
    public static string UserAgent => GetUserAgent();

    public static readonly List<Type> AlreadyChecked = [];

    private static Version GetVersionFromGit()
    {
        try
        {
            return Version.Parse($"{Git.LastTag}.{Git.CommitsSinceLastTag}");
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return null;
        }
    }

    private static string GetUserAgent()
    {
        string versionString = (Version != null) ? Version.ToString() : "Unknown";
        return $"LiveSplit/{versionString}";
    }
}
