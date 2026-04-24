using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace LiveSplit.ComponentUtil;

/// <summary>
/// Linux placeholder for <see cref="IProcessMemory"/>. All operations throw
/// <see cref="PlatformNotSupportedException"/> until Phase 7 fills in the real implementation
/// using process_vm_readv/writev, /proc/{pid}/maps, SIGSTOP/SIGCONT, and friends.
/// Injection-style methods (AllocateMemory / FreeMemory / CreateThread / VirtualProtect) will
/// continue to throw on Linux even after Phase 7 — they only make sense for remote-process
/// code injection, which we do not support on Linux.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class LinuxProcessMemory : IProcessMemory
{
    private static Exception Unsupported([System.Runtime.CompilerServices.CallerMemberName] string member = null) =>
        new PlatformNotSupportedException(
            $"LinuxProcessMemory.{member} is not yet implemented. Memory-reading support is planned for Phase 7 of the Linux port.");

    public ProcessModuleWow64Safe[] EnumerateModules(Process process) => throw Unsupported();

    public IEnumerable<MemoryBasicInformation> EnumerateMemoryPages(Process process, bool all) => throw Unsupported();

    public bool Is64Bit(Process process) => throw Unsupported();

    public bool ReadBytes(Process process, IntPtr addr, int count, out byte[] val) => throw Unsupported();

    public bool WriteBytes(Process process, IntPtr addr, byte[] bytes) => throw Unsupported();

    public IntPtr AllocateMemory(Process process, int size) => throw Unsupported();

    public bool FreeMemory(Process process, IntPtr addr) => throw Unsupported();

    public bool VirtualProtect(Process process, IntPtr addr, int size, MemPageProtect protect, out MemPageProtect oldProtect) => throw Unsupported();

    public void Suspend(Process process) => throw Unsupported();

    public void Resume(Process process) => throw Unsupported();

    public IntPtr CreateThread(Process process, IntPtr startAddress, IntPtr parameter) => throw Unsupported();
}
