using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LiveSplit.AutoSplittingRuntime;

/// <summary>
/// Resolves the <c>asr_capi</c> native dependency out of the standard
/// <c>runtimes/{rid}/native/</c> layout. Duplicated from
/// <c>src/LiveSplit.Core/NativeLibraryResolver.cs</c> because
/// <see cref="NativeLibrary.SetDllImportResolver"/> is per-assembly: each assembly's
/// own DllImport declarations need their own module initializer to be intercepted.
/// </summary>
internal static class NativeLibraryResolver
{
#pragma warning disable CA2255 // Resolver must register before any [DllImport("asr_capi")] is bound; that's exactly what ModuleInitializer is for here.
    [ModuleInitializer]
    internal static void Register()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeLibraryResolver).Assembly, Resolve);
    }
#pragma warning restore CA2255

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != "asr_capi")
        {
            return IntPtr.Zero;
        }

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

            string fullPath = Path.Combine(nativeDir, PlatformFileName(libraryName));
            if (File.Exists(fullPath) && NativeLibrary.TryLoad(fullPath, out IntPtr handle))
            {
                return handle;
            }
        }

        // Legacy flat layout: Components/x64/asr_capi.dll or Components/x86/asr_capi.dll.
        string archDir = RuntimeInformation.ProcessArchitecture == Architecture.X86 ? "x86" : "x64";
        string legacy = Path.Combine(baseDir, archDir, PlatformFileName(libraryName));
        if (File.Exists(legacy) && NativeLibrary.TryLoad(legacy, out IntPtr legacyHandle))
        {
            return legacyHandle;
        }

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
