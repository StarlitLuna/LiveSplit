using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LiveSplit.ComponentUtil;

/// <summary>
/// Linux <see cref="IProcessMemory"/> implementation. Uses <c>process_vm_readv</c> /
/// <c>process_vm_writev</c> for fast cross-process memory I/O, with a <c>/proc/{pid}/mem</c>
/// fallback when those return EPERM on hardened kernels (yama/ptrace_scope >= 2). Module
/// enumeration parses <c>/proc/{pid}/maps</c>; <see cref="Suspend"/> / <see cref="Resume"/>
/// use SIGSTOP / SIGCONT.
///
/// Code-injection methods (<see cref="AllocateMemory"/>, <see cref="CreateThread"/>,
/// <see cref="VirtualProtect"/> on a foreign process) throw — those map to Windows-specific
/// remote-thread tricks that don't portably reproduce on Linux.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class LinuxProcessMemory : IProcessMemory
{
    [StructLayout(LayoutKind.Sequential)]
    private struct iovec
    {
        public IntPtr iov_base;
        public UIntPtr iov_len;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr process_vm_readv(int pid,
        [In] iovec[] local_iov, UIntPtr liovcnt,
        [In] iovec[] remote_iov, UIntPtr riovcnt,
        UIntPtr flags);

    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr process_vm_writev(int pid,
        [In] iovec[] local_iov, UIntPtr liovcnt,
        [In] iovec[] remote_iov, UIntPtr riovcnt,
        UIntPtr flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int sig);

    private const int SIGSTOP = 19;
    private const int SIGCONT = 18;

    public ProcessModuleWow64Safe[] EnumerateModules(Process process)
    {
        // /proc/{pid}/maps lines:
        //   start-end perms offset dev inode pathname
        // example:
        //   55b16ea7e000-55b16ea80000 r-xp 00002000 fd:00 12345    /usr/bin/vim
        // Group consecutive file-backed ranges by canonical pathname; the lowest start is the
        // "base", the highest end gives ModuleMemorySize. The main module is the one that
        // matches realpath(/proc/{pid}/exe).

        string mapsPath = $"/proc/{process.Id}/maps";
        if (!File.Exists(mapsPath))
        {
            return Array.Empty<ProcessModuleWow64Safe>();
        }

        var byPath = new Dictionary<string, (ulong lo, ulong hi)>(StringComparer.Ordinal);
        foreach (string raw in File.ReadAllLines(mapsPath))
        {
            (ulong start, ulong end, string path) = ParseMapLine(raw);
            if (string.IsNullOrEmpty(path) || path[0] == '[' || path == "/SYSV00000000")
            {
                // [stack], [vdso], [vsyscall], [heap], anonymous mappings — not modules.
                continue;
            }

            if (byPath.TryGetValue(path, out (ulong lo, ulong hi) existing))
            {
                byPath[path] = (Math.Min(existing.lo, start), Math.Max(existing.hi, end));
            }
            else
            {
                byPath[path] = (start, end);
            }
        }

        // Resolve the main module via /proc/{pid}/exe → realpath. Sort it first so the rest of
        // LiveSplit (DeepPointer.MainModuleWow64Safe) finds it at index 0.
        string exePath = ReadlinkSafe($"/proc/{process.Id}/exe");

        var modules = new List<ProcessModuleWow64Safe>(byPath.Count);
        foreach (KeyValuePair<string, (ulong lo, ulong hi)> kv in byPath)
        {
            modules.Add(new ProcessModuleWow64Safe
            {
                BaseAddress = (IntPtr)(long)kv.Value.lo,
                EntryPointAddress = (IntPtr)(long)kv.Value.lo,
                FileName = kv.Key,
                ModuleMemorySize = (int)Math.Min(int.MaxValue, kv.Value.hi - kv.Value.lo),
                ModuleName = Path.GetFileName(kv.Key),
            });
        }

        if (!string.IsNullOrEmpty(exePath))
        {
            int idx = modules.FindIndex(m => string.Equals(m.FileName, exePath, StringComparison.Ordinal));
            if (idx > 0)
            {
                ProcessModuleWow64Safe main = modules[idx];
                modules.RemoveAt(idx);
                modules.Insert(0, main);
            }
        }

        return modules.ToArray();
    }

    public IEnumerable<MemoryBasicInformation> EnumerateMemoryPages(Process process, bool all)
    {
        string mapsPath = $"/proc/{process.Id}/maps";
        if (!File.Exists(mapsPath))
        {
            yield break;
        }

        foreach (string raw in File.ReadAllLines(mapsPath))
        {
            (ulong start, ulong end, _) = ParseMapLine(raw);
            string perms = ParsePermsField(raw);
            if (perms == null)
            {
                continue;
            }

            MemPageProtect protect = MapPerms(perms);
            yield return new MemoryBasicInformation
            {
                BaseAddress = (IntPtr)(long)start,
                AllocationBase = (IntPtr)(long)start,
                AllocationProtect = protect,
                RegionSize = (UIntPtr)(end - start),
                State = MemPageState.MEM_COMMIT,
                Protect = protect,
                Type = perms.Length >= 4 && perms[3] == 'p' ? MemPageType.MEM_PRIVATE : MemPageType.MEM_MAPPED,
            };
        }
    }

    public bool Is64Bit(Process process)
    {
        // ELF header: bytes 0..3 are the magic 0x7f 'E' 'L' 'F'. Byte 4 (EI_CLASS) is 1 for
        // ELFCLASS32, 2 for ELFCLASS64.
        try
        {
            using FileStream fs = File.OpenRead($"/proc/{process.Id}/exe");
            Span<byte> header = stackalloc byte[5];
            int read = fs.Read(header);
            if (read < 5 || header[0] != 0x7f || header[1] != (byte)'E' || header[2] != (byte)'L' || header[3] != (byte)'F')
            {
                // Fallback: assume 64-bit on a 64-bit kernel.
                return Environment.Is64BitOperatingSystem;
            }

            return header[4] == 2;
        }
        catch
        {
            return Environment.Is64BitOperatingSystem;
        }
    }

    public bool ReadBytes(Process process, IntPtr addr, int count, out byte[] val)
    {
        val = new byte[count];
        if (count == 0)
        {
            return true;
        }

        if (TryProcessVm(process.Id, addr, val, count, write: false))
        {
            return true;
        }

        // Fallback to /proc/{pid}/mem for hardened kernels (yama/ptrace_scope >= 2).
        return TryProcMem(process.Id, addr, val, count, write: false);
    }

    public bool WriteBytes(Process process, IntPtr addr, byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return true;
        }

        if (TryProcessVm(process.Id, addr, bytes, bytes.Length, write: true))
        {
            return true;
        }

        return TryProcMem(process.Id, addr, bytes, bytes.Length, write: true);
    }

    public IntPtr AllocateMemory(Process process, int size) =>
        throw new PlatformNotSupportedException("Remote memory allocation is not supported on Linux.");

    public bool FreeMemory(Process process, IntPtr addr) =>
        throw new PlatformNotSupportedException("Remote memory free is not supported on Linux.");

    public bool VirtualProtect(Process process, IntPtr addr, int size, MemPageProtect protect, out MemPageProtect oldProtect) =>
        throw new PlatformNotSupportedException("Remote VirtualProtect is not supported on Linux.");

    public IntPtr CreateThread(Process process, IntPtr startAddress, IntPtr parameter) =>
        throw new PlatformNotSupportedException("Remote thread creation is not supported on Linux.");

    public void Suspend(Process process)
    {
        if (kill(process.Id, SIGSTOP) != 0)
        {
            int err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"kill(SIGSTOP) failed: errno={err}");
        }
    }

    public void Resume(Process process)
    {
        if (kill(process.Id, SIGCONT) != 0)
        {
            int err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"kill(SIGCONT) failed: errno={err}");
        }
    }

    // --- helpers ---

    private static unsafe bool TryProcessVm(int pid, IntPtr addr, byte[] buffer, int count, bool write)
    {
        fixed (byte* bufPtr = buffer)
        {
            var local = new[]
            {
                new iovec { iov_base = (IntPtr)bufPtr, iov_len = (UIntPtr)count },
            };
            var remote = new[]
            {
                new iovec { iov_base = addr, iov_len = (UIntPtr)count },
            };

            IntPtr ret = write
                ? process_vm_writev(pid, local, (UIntPtr)1, remote, (UIntPtr)1, UIntPtr.Zero)
                : process_vm_readv(pid, local, (UIntPtr)1, remote, (UIntPtr)1, UIntPtr.Zero);

            return ret.ToInt64() == count;
        }
    }

    private static bool TryProcMem(int pid, IntPtr addr, byte[] buffer, int count, bool write)
    {
        // /proc/{pid}/mem requires either matching uid + ptrace permission, or root. Sparse
        // pages will show as zero-length reads; we treat those as failure.
        try
        {
            FileMode mode = write ? FileMode.Open : FileMode.Open;
            FileAccess access = write ? FileAccess.Write : FileAccess.Read;
            using var fs = new FileStream($"/proc/{pid}/mem", mode, access, FileShare.ReadWrite);
            fs.Seek(addr.ToInt64(), SeekOrigin.Begin);

            if (write)
            {
                fs.Write(buffer, 0, count);
                return true;
            }

            int total = 0;
            while (total < count)
            {
                int read = fs.Read(buffer, total, count - total);
                if (read <= 0)
                {
                    return false;
                }

                total += read;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static (ulong start, ulong end, string path) ParseMapLine(string line)
    {
        // "55b16ea7e000-55b16ea80000 r-xp 00002000 fd:00 12345 /usr/bin/vim"
        int dash = line.IndexOf('-');
        int spaceAfterRange = dash >= 0 ? line.IndexOf(' ', dash) : -1;
        if (dash < 0 || spaceAfterRange < 0)
        {
            return (0, 0, null);
        }

        if (!ulong.TryParse(line.AsSpan(0, dash), System.Globalization.NumberStyles.HexNumber, null, out ulong start))
        {
            return (0, 0, null);
        }

        if (!ulong.TryParse(line.AsSpan(dash + 1, spaceAfterRange - dash - 1), System.Globalization.NumberStyles.HexNumber, null, out ulong end))
        {
            return (0, 0, null);
        }

        // Skip 4 whitespace-separated fields after the range: perms, offset, dev, inode.
        // Whatever remains is the pathname (may be empty for anonymous mappings).
        int idx = spaceAfterRange;
        for (int field = 0; field < 4; field++)
        {
            while (idx < line.Length && line[idx] == ' ')
            {
                idx++;
            }

            while (idx < line.Length && line[idx] != ' ')
            {
                idx++;
            }
        }

        while (idx < line.Length && line[idx] == ' ')
        {
            idx++;
        }

        string path = idx < line.Length ? line[idx..] : string.Empty;
        return (start, end, path);
    }

    private static string ParsePermsField(string line)
    {
        int dash = line.IndexOf('-');
        if (dash < 0)
        {
            return null;
        }

        int spaceAfter = line.IndexOf(' ', dash);
        if (spaceAfter < 0)
        {
            return null;
        }

        int permsStart = spaceAfter + 1;
        int permsEnd = line.IndexOf(' ', permsStart);
        if (permsEnd < 0 || permsEnd - permsStart != 4)
        {
            return null;
        }

        return line.Substring(permsStart, 4);
    }

    private static MemPageProtect MapPerms(string perms)
    {
        bool r = perms[0] == 'r';
        bool w = perms[1] == 'w';
        bool x = perms[2] == 'x';

        if (x && r && w)
        {
            return MemPageProtect.PAGE_EXECUTE_READWRITE;
        }

        if (x && r)
        {
            return MemPageProtect.PAGE_EXECUTE_READ;
        }

        if (x)
        {
            return MemPageProtect.PAGE_EXECUTE;
        }

        if (r && w)
        {
            return MemPageProtect.PAGE_READWRITE;
        }

        if (r)
        {
            return MemPageProtect.PAGE_READONLY;
        }

        return MemPageProtect.PAGE_NOACCESS;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int readlink(string path, byte[] buf, UIntPtr bufsiz);

    private static string ReadlinkSafe(string path)
    {
        try
        {
            byte[] buf = new byte[4096];
            int len = readlink(path, buf, (UIntPtr)buf.Length);
            if (len <= 0)
            {
                return null;
            }

            return System.Text.Encoding.UTF8.GetString(buf, 0, Math.Min(len, buf.Length));
        }
        catch
        {
            return null;
        }
    }
}
