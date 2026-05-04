using System;
using System.IO;

using LiveSplit.ComponentUtil;

using Xunit;

namespace LiveSplit.Tests.ComponentUtil;

public class LinuxProcessMemoryTests
{
    [Fact]
    public void ExcludesFileBackedPagesWhenAllIsFalse()
    {
        string line = "7f1000000000-7f1000010000 r-xp 00000000 08:01 12345 /home/user/game.exe";

        Assert.True(LinuxProcessMemory.TryParseMemoryPage(line, all: true, out MemoryBasicInformation allPage));
        Assert.Equal(MemPageType.MEM_MAPPED, allPage.Type);
        Assert.False(LinuxProcessMemory.TryParseMemoryPage(line, all: false, out _));
    }

    [Fact]
    public void IncludesAnonymousPrivatePagesWhenAllIsFalse()
    {
        string line = "7f2000000000-7f2000020000 rw-p 00000000 00:00 0";

        Assert.True(LinuxProcessMemory.TryParseMemoryPage(line, all: false, out MemoryBasicInformation page));

        Assert.Equal(0x7f2000000000, page.BaseAddress.ToInt64());
        Assert.Equal((UIntPtr)0x20000, page.RegionSize);
        Assert.Equal(MemPageType.MEM_PRIVATE, page.Type);
        Assert.Equal(MemPageProtect.PAGE_READWRITE, page.Protect);
    }

    [Fact]
    public void DetectsMappedPeBitnessBeforeWineElfWrapper()
    {
        string pe32Path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.exe");
        try
        {
            WritePeImage(pe32Path, machine: 0x014c);
            string[] maps =
            [
                $"00400000-005d4000 r-xp 00000000 fd:00 12345 {pe32Path}",
                "7f3000000000-7f3000100000 r-xp 00000000 fd:00 67890 /usr/bin/wine64-preloader",
            ];

            bool? bitness = LinuxProcessMemory.TryFindMappedPeBitness(maps);

            Assert.False(bitness);
        }
        finally
        {
            if (File.Exists(pe32Path))
            {
                File.Delete(pe32Path);
            }
        }
    }

    [Fact]
    public void DetectsDeletedMappedPeBitness()
    {
        string pe32Path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.exe");
        try
        {
            WritePeImage(pe32Path, machine: 0x014c);
            string[] maps =
            [
                $"00400000-005d4000 r-xp 00000000 fd:00 12345 {pe32Path} (deleted)",
            ];

            bool? bitness = LinuxProcessMemory.TryFindMappedPeBitness(maps);

            Assert.False(bitness);
        }
        finally
        {
            if (File.Exists(pe32Path))
            {
                File.Delete(pe32Path);
            }
        }
    }

    [Fact]
    public void DetectsPe64Bitness()
    {
        string pe64Path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.exe");
        try
        {
            WritePeImage(pe64Path, machine: 0x8664);

            Assert.True(LinuxProcessMemory.TryReadPeBitness(pe64Path));
        }
        finally
        {
            if (File.Exists(pe64Path))
            {
                File.Delete(pe64Path);
            }
        }
    }

    private static void WritePeImage(string path, ushort machine)
    {
        byte[] bytes = new byte[0x100];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        bytes[0x3c] = 0x80;
        bytes[0x80] = (byte)'P';
        bytes[0x81] = (byte)'E';
        bytes[0x82] = 0;
        bytes[0x83] = 0;
        bytes[0x84] = (byte)(machine & 0xff);
        bytes[0x85] = (byte)(machine >> 8);
        File.WriteAllBytes(path, bytes);
    }
}
