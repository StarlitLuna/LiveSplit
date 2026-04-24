using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

using SizeT = System.UIntPtr;

namespace LiveSplit.ComponentUtil;

/// <summary>
/// Windows implementation of <see cref="IProcessMemory"/> backed by kernel32/psapi/ntdll P/Invokes
/// (see <see cref="WinAPI"/>).
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsProcessMemory : IProcessMemory
{
    private readonly Dictionary<int, ProcessModuleWow64Safe[]> _moduleCache = [];

    public ProcessModuleWow64Safe[] EnumerateModules(Process process)
    {
        if (_moduleCache.Count > 100)
        {
            _moduleCache.Clear();
        }

        const int LIST_MODULES_ALL = 3;
        const int MAX_PATH = 260;

        if (!WinAPI.EnumProcessModulesEx(process.Handle, null, 0, out uint cbNeeded, LIST_MODULES_ALL))
        {
            throw new Win32Exception();
        }

        uint numMods = cbNeeded / (uint)Unsafe.SizeOf<IntPtr>();

        int hash = process.StartTime.GetHashCode() + process.Id + (int)numMods;
        if (_moduleCache.TryGetValue(hash, out ProcessModuleWow64Safe[] cached))
        {
            return cached;
        }

        IntPtr[] hModules = new IntPtr[(int)numMods];
        if (!WinAPI.EnumProcessModulesEx(process.Handle, hModules, cbNeeded, out _, LIST_MODULES_ALL))
        {
            throw new Win32Exception();
        }

        var ret = new List<ProcessModuleWow64Safe>();

        // everything below is fairly expensive, which is why we cache!
        var sb = new StringBuilder(MAX_PATH);
        for (int i = 0; i < numMods; i++)
        {
            sb.Clear();
            if (WinAPI.GetModuleFileNameExW(process.Handle, hModules[i], sb, (uint)sb.Capacity) == 0)
            {
                throw new Win32Exception();
            }

            string fileName = sb.ToString();

            sb.Clear();
            if (WinAPI.GetModuleBaseNameW(process.Handle, hModules[i], sb, (uint)sb.Capacity) == 0)
            {
                throw new Win32Exception();
            }

            string baseName = sb.ToString();

            var moduleInfo = new WinAPI.MODULEINFO();
            if (!WinAPI.GetModuleInformation(process.Handle, hModules[i], out moduleInfo, (uint)Marshal.SizeOf(moduleInfo)))
            {
                throw new Win32Exception();
            }

            ret.Add(new ProcessModuleWow64Safe
            {
                FileName = fileName,
                BaseAddress = moduleInfo.lpBaseOfDll,
                ModuleMemorySize = (int)moduleInfo.SizeOfImage,
                EntryPointAddress = moduleInfo.EntryPoint,
                ModuleName = baseName,
            });
        }

        ProcessModuleWow64Safe[] arr = [.. ret];
        _moduleCache[hash] = arr;
        return arr;
    }

    public IEnumerable<MemoryBasicInformation> EnumerateMemoryPages(Process process, bool all)
    {
        // hardcoded values because GetSystemInfo / GetNativeSystemInfo can't return info for remote process
        long min = 0x10000L;
        long max = Is64Bit(process) ? 0x00007FFFFFFEFFFFL : 0x7FFEFFFFL;

        SizeT mbiSize = (SizeT)Marshal.SizeOf(typeof(MemoryBasicInformation));

        long addr = min;
        do
        {
            if (WinAPI.VirtualQueryEx(process.Handle, (IntPtr)addr, out MemoryBasicInformation mbi, mbiSize) == SizeT.Zero)
            {
                break;
            }

            addr += (long)mbi.RegionSize;

            // don't care about reserved/free pages
            if (mbi.State != MemPageState.MEM_COMMIT)
            {
                continue;
            }

            // probably don't care about guarded pages
            if (!all && (mbi.Protect & MemPageProtect.PAGE_GUARD) != 0)
            {
                continue;
            }

            // probably don't care about image/file maps
            if (!all && mbi.Type != MemPageType.MEM_PRIVATE)
            {
                continue;
            }

            yield return mbi;
        }
        while (addr < max);
    }

    public bool Is64Bit(Process process)
    {
        WinAPI.IsWow64Process(process.Handle, out bool procWow64);
        return Environment.Is64BitOperatingSystem && !procWow64;
    }

    public bool ReadBytes(Process process, IntPtr addr, int count, out byte[] val)
    {
        byte[] bytes = new byte[count];

        val = null;
        if (!WinAPI.ReadProcessMemory(process.Handle, addr, bytes, (SizeT)bytes.Length, out SizeT read)
            || read != (SizeT)bytes.Length)
        {
            return false;
        }

        val = bytes;
        return true;
    }

    public bool WriteBytes(Process process, IntPtr addr, byte[] bytes)
    {
        return WinAPI.WriteProcessMemory(process.Handle, addr, bytes, (SizeT)bytes.Length, out SizeT written)
            && written == (SizeT)bytes.Length;
    }

    public IntPtr AllocateMemory(Process process, int size)
    {
        return WinAPI.VirtualAllocEx(process.Handle, IntPtr.Zero, (SizeT)size, (uint)MemPageState.MEM_COMMIT,
            MemPageProtect.PAGE_EXECUTE_READWRITE);
    }

    public bool FreeMemory(Process process, IntPtr addr)
    {
        const uint MEM_RELEASE = 0x8000;
        return WinAPI.VirtualFreeEx(process.Handle, addr, SizeT.Zero, MEM_RELEASE);
    }

    public bool VirtualProtect(Process process, IntPtr addr, int size, MemPageProtect protect, out MemPageProtect oldProtect)
    {
        return WinAPI.VirtualProtectEx(process.Handle, addr, (SizeT)size, protect, out oldProtect);
    }

    public void Suspend(Process process) => WinAPI.NtSuspendProcess(process.Handle);

    public void Resume(Process process) => WinAPI.NtResumeProcess(process.Handle);

    public IntPtr CreateThread(Process process, IntPtr startAddress, IntPtr parameter)
    {
        return WinAPI.CreateRemoteThread(process.Handle, IntPtr.Zero, SizeT.Zero, startAddress, parameter, 0, out _);
    }
}
