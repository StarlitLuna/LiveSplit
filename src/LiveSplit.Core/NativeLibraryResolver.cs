using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LiveSplit;

/// <summary>
/// Resolves the <c>livesplit_core</c> native dependency out of the standard .NET
/// <c>runtimes/{rid}/native/</c> layout. Registered from a
/// <see cref="ModuleInitializerAttribute"/> so it's in place before any
/// <c>[DllImport("livesplit_core")]</c> attribute is resolved for the first time.
/// </summary>
internal static class NativeLibraryResolver
{
    [ModuleInitializer]
    internal static void Register()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeLibraryResolver).Assembly, Resolve);
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != "livesplit_core")
        {
            return IntPtr.Zero;
        }

        return ResolveFromRuntimes(libraryName, assembly);
    }

    /// <summary>
    /// Walks <c>runtimes/{rid}/native/{prefix}{name}{ext}</c> for the current platform +
    /// architecture, falling back to the library's default search behavior if no file is found
    /// (which lets <c>dotnet publish -r <rid></c> single-file layouts keep working).
    /// </summary>
    internal static IntPtr ResolveFromRuntimes(string libraryName, Assembly assembly)
    {
        string baseDir = Path.GetDirectoryName(assembly.Location);
        if (string.IsNullOrEmpty(baseDir))
        {
            return IntPtr.Zero;
        }

        foreach (string rid in CandidateRids())
        {
            string nativeDir = Path.Combine(baseDir, "runtimes", rid, "native");
            if (!Directory.Exists(nativeDir))
            {
                continue;
            }

            string filename = PlatformFileName(libraryName);
            string fullPath = Path.Combine(nativeDir, filename);
            if (File.Exists(fullPath) && NativeLibrary.TryLoad(fullPath, out IntPtr handle))
            {
                return handle;
            }
        }

        // Legacy flat layout: bare x64/ and x86/ folders next to the assembly.
        string archDir = RuntimeInformation.ProcessArchitecture == Architecture.X86 ? "x86" : "x64";
        string legacy = Path.Combine(baseDir, archDir, PlatformFileName(libraryName));
        if (File.Exists(legacy) && NativeLibrary.TryLoad(legacy, out IntPtr legacyHandle))
        {
            return legacyHandle;
        }

        // Fall through to the default DllImport resolver — it tries the OS library-search path,
        // which lets a system-installed .so on LD_LIBRARY_PATH satisfy the import.
        return IntPtr.Zero;
    }

    private static string[] CandidateRids()
    {
        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => "x64",
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new[] { $"win-{arch}", "win" };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new[] { $"linux-{arch}", "linux" };
        }

        return Array.Empty<string>();
    }

    private static string PlatformFileName(string libraryName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return libraryName + ".dll";
        }

        return "lib" + libraryName + ".so";
    }
}
