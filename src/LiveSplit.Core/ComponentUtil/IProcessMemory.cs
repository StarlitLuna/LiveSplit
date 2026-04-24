using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LiveSplit.ComponentUtil;

/// <summary>
/// Platform-specific primitives for reading, writing, enumerating, and controlling a remote process's
/// memory. The platform-agnostic conveniences (ReadValue&lt;T&gt;, ReadString, WriteDetour, etc.) live
/// in <see cref="ExtensionMethods"/> and delegate to an implementation of this interface via
/// <see cref="ProcessMemory.Current"/>.
/// </summary>
public interface IProcessMemory
{
    ProcessModuleWow64Safe[] EnumerateModules(Process process);
    IEnumerable<MemoryBasicInformation> EnumerateMemoryPages(Process process, bool all);
    bool Is64Bit(Process process);

    bool ReadBytes(Process process, IntPtr addr, int count, out byte[] val);
    bool WriteBytes(Process process, IntPtr addr, byte[] bytes);

    IntPtr AllocateMemory(Process process, int size);
    bool FreeMemory(Process process, IntPtr addr);
    bool VirtualProtect(Process process, IntPtr addr, int size, MemPageProtect protect, out MemPageProtect oldProtect);

    void Suspend(Process process);
    void Resume(Process process);
    IntPtr CreateThread(Process process, IntPtr startAddress, IntPtr parameter);
}

/// <summary>
/// Selects the <see cref="IProcessMemory"/> implementation for the current OS at load time.
/// </summary>
public static class ProcessMemory
{
    public static IProcessMemory Current { get; } = Create();

    private static IProcessMemory Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsProcessMemory();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxProcessMemory();
        }

        throw new PlatformNotSupportedException(
            $"Process memory access is not supported on {RuntimeInformation.OSDescription}.");
    }
}
