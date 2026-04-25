using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace LiveSplit.ComponentUtil;

/// <summary>
/// Wine/Proton-aware wrapper around <see cref="Process.GetProcessesByName(string)"/>.
/// On Windows this is identical to the .NET implementation. On Linux it adds three fallbacks
/// to handle Wine writing variable comm values for the same PE — see
/// <see cref="GetProcessesByName(string)"/> for the lookup strategy.
/// </summary>
public static class WineAwareProcess
{
    /// <summary>
    /// Looks up processes by name, with Linux/Wine fallbacks. .NET on Linux matches against
    /// <c>/proc/&lt;pid&gt;/comm</c> (truncated to 15 chars), and Wine writes the PE basename
    /// there — but whether the <c>.exe</c> suffix is included varies by Wine/Proton version,
    /// and for long EXE names the suffix may be truncated off entirely. To handle every case:
    /// <list type="number">
    ///   <item>Try <paramref name="name"/> as-is. Matches when Wine strips the suffix or when
    ///         the target is a native Linux process.</item>
    ///   <item>Try the alternate-suffix form (bare if <paramref name="name"/> already ends in
    ///         <c>.exe</c>, suffixed if it doesn't). Catches whichever Wine convention the
    ///         caller didn't anticipate.</item>
    ///   <item>Walk <c>/proc/*/maps</c> for any process with a file-backed mapping whose
    ///         basename matches the <c>.exe</c>-suffixed form. The PE in <c>maps</c> is what
    ///         Wine actually loaded, regardless of what the kernel comm says — this catches
    ///         truncated comms, wrapper names (<c>wine-preloader</c>), and threads that have
    ///         overwritten comm via <c>prctl(PR_SET_NAME)</c>.</item>
    /// </list>
    /// </summary>
    public static Process[] GetProcessesByName(string name)
    {
        Process[] direct = Process.GetProcessesByName(name);
        if (direct.Length > 0 || !RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return direct;
        }

        (string peName, string altName) = NormalizeForLookup(name);

        Process[] alt = Process.GetProcessesByName(altName);
        if (alt.Length > 0)
        {
            return alt;
        }

        return FindByPeMapping(peName);
    }

    /// <summary>
    /// Splits an input name into the two forms the Linux fallbacks need:
    /// <c>peName</c> is always the <c>.exe</c>-suffixed canonical PE basename (used for the
    /// <c>/proc/*/maps</c> walk, which sees the on-disk filename Wine loaded). <c>altName</c>
    /// is the alternate kernel-comm form to try after the caller-supplied name fails: bare if
    /// the input had <c>.exe</c>, otherwise the same as <c>peName</c>.
    /// </summary>
    internal static (string peName, string altName) NormalizeForLookup(string name)
    {
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return (peName: name, altName: name[..^4]);
        }
        return (peName: name + ".exe", altName: name + ".exe");
    }

    private static Process[] FindByPeMapping(string peName)
    {
        var matches = new List<Process>();

        foreach (Process candidate in Process.GetProcesses())
        {
            if (HasPeMapping(candidate.Id, peName))
            {
                matches.Add(candidate);
            }
            else
            {
                candidate.Dispose();
            }
        }

        return matches.ToArray();
    }

    private static bool HasPeMapping(int pid, string peName)
    {
        try
        {
            string mapsPath = $"/proc/{pid}/maps";
            if (!File.Exists(mapsPath))
            {
                return false;
            }

            foreach (string line in File.ReadLines(mapsPath))
            {
                if (LineMatchesPe(line, peName))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Process might have exited, /proc entry might be unreadable on hardened
            // kernels, etc. Treat as a non-match.
        }

        return false;
    }

    /// <summary>
    /// Returns true if the given <c>/proc/&lt;pid&gt;/maps</c> line is a file-backed mapping
    /// whose basename matches <paramref name="peName"/>. Pulled out as a static helper so the
    /// parsing logic stays unit-testable without needing real /proc state.
    /// </summary>
    internal static bool LineMatchesPe(string line, string peName)
    {
        int slash = line.LastIndexOf('/');
        if (slash < 0)
        {
            return false;
        }

        ReadOnlySpan<char> basename = line.AsSpan(slash + 1).TrimEnd();
        return basename.Equals(peName.AsSpan(), StringComparison.OrdinalIgnoreCase);
    }
}
