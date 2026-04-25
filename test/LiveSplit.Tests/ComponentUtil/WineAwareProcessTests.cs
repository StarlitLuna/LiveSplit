using System;

using LiveSplit.ComponentUtil;

using Xunit;

namespace LiveSplit.Tests.ComponentUtil;

public class WineAwareProcessTests
{
    // Real /proc/<pid>/maps lines collected from a Wine'd 32-bit Win32 process.
    private const string GameExeMapping =
        "00400000-005d4000 r-xp 00000000 fd:00 12345    /home/user/.steam/steamapps/common/SonicAdventure2/sonic2app.exe";
    private const string LibmapMapping =
        "f7c00000-f7d80000 r-xp 00000000 fd:00 67890    /usr/lib/wine/i386-unix/ntdll.so";
    private const string AnonymousMapping =
        "7f1234000000-7f1234021000 rw-p 00000000 00:00 0";
    private const string StackMapping =
        "7ffd12300000-7ffd12321000 rw-p 00000000 00:00 0                          [stack]";

    [Fact]
    public void LineMatchesPe_MatchesWindowsExeBasename()
    {
        Assert.True(WineAwareProcess.LineMatchesPe(GameExeMapping, "sonic2app.exe"));
    }

    [Fact]
    public void LineMatchesPe_IsCaseInsensitive()
    {
        Assert.True(WineAwareProcess.LineMatchesPe(GameExeMapping, "Sonic2App.EXE"));
    }

    [Fact]
    public void LineMatchesPe_RejectsDifferentBasename()
    {
        Assert.False(WineAwareProcess.LineMatchesPe(GameExeMapping, "other.exe"));
    }

    [Fact]
    public void LineMatchesPe_RejectsLinuxLibrary()
    {
        Assert.False(WineAwareProcess.LineMatchesPe(LibmapMapping, "ntdll.exe"));
    }

    [Fact]
    public void LineMatchesPe_HandlesAnonymousMapping()
    {
        Assert.False(WineAwareProcess.LineMatchesPe(AnonymousMapping, "anything.exe"));
    }

    [Fact]
    public void LineMatchesPe_IgnoresPseudoPathsLikeStack()
    {
        // [stack] / [heap] etc. don't contain '/', so the helper should reject them.
        Assert.False(WineAwareProcess.LineMatchesPe(StackMapping, "stack"));
    }

    [Fact]
    public void LineMatchesPe_RejectsPartialMatch()
    {
        // "sonic2app" is not a valid match against "sonic2app.exe" — basename equality only.
        Assert.False(WineAwareProcess.LineMatchesPe(GameExeMapping, "sonic2app"));
    }

    // --- NormalizeForLookup ---

    [Fact]
    public void NormalizeForLookup_AppendsExeWhenMissing()
    {
        (string peName, string altName) = WineAwareProcess.NormalizeForLookup("sonic2app");
        // Both forms collapse to the .exe-suffixed name: tier 2 retries with the suffix,
        // tier 3 walks /proc/*/maps for the same filename.
        Assert.Equal("sonic2app.exe", peName);
        Assert.Equal("sonic2app.exe", altName);
    }

    [Fact]
    public void NormalizeForLookup_StripsExeForAltName()
    {
        // Caller passed "sonic2app.exe" — the alt form for the comm fallback is the bare
        // name (covers Wine versions that drop the suffix), but the maps walk still uses
        // the canonical .exe filename.
        (string peName, string altName) = WineAwareProcess.NormalizeForLookup("sonic2app.exe");
        Assert.Equal("sonic2app.exe", peName);
        Assert.Equal("sonic2app", altName);
    }

    [Fact]
    public void NormalizeForLookup_ExtensionStripIsCaseInsensitive()
    {
        (string peName, string altName) = WineAwareProcess.NormalizeForLookup("Sonic2App.EXE");
        Assert.Equal("Sonic2App.EXE", peName);
        Assert.Equal("Sonic2App", altName);
    }

    [Fact]
    public void NormalizeForLookup_HandlesMixedCaseExe()
    {
        (string peName, string altName) = WineAwareProcess.NormalizeForLookup("Foo.Exe");
        Assert.Equal("Foo.Exe", peName);
        Assert.Equal("Foo", altName);
    }

    [Fact]
    public void NormalizeForLookup_DoesNotStripUnrelatedDottedSuffix()
    {
        // "some.app" doesn't end with ".exe"; the embedded dot must not trigger stripping.
        (string peName, string altName) = WineAwareProcess.NormalizeForLookup("some.app");
        Assert.Equal("some.app.exe", peName);
        Assert.Equal("some.app.exe", altName);
    }

    [Fact]
    public void NormalizeForLookup_DoesNotMistakeExeAsSuffixOfLongerExtension()
    {
        // "Foo.exec" ends with ".exec", not ".exe" — the substring isn't a real suffix here.
        (string peName, string altName) = WineAwareProcess.NormalizeForLookup("Foo.exec");
        Assert.Equal("Foo.exec.exe", peName);
        Assert.Equal("Foo.exec.exe", altName);
    }

    // --- LineMatchesPe with .exe-suffixed input upstream ---

    [Fact]
    public void LineMatchesPe_MatchesAgainstNormalizedPeName()
    {
        // Sanity check that LineMatchesPe works correctly when callers feed it the
        // peName that NormalizeForLookup produces from a pre-suffixed input.
        (string peName, _) = WineAwareProcess.NormalizeForLookup("sonic2app.exe");
        Assert.True(WineAwareProcess.LineMatchesPe(GameExeMapping, peName));
    }

    [Fact]
    public void LineMatchesPe_DoesNotDoubleSuffix()
    {
        // Regression guard: if NormalizeForLookup ever started returning "sonic2app.exe.exe"
        // for ".exe"-suffixed inputs, this assertion would fire because no real PE has
        // that basename.
        (string peName, _) = WineAwareProcess.NormalizeForLookup("sonic2app.exe");
        Assert.DoesNotContain(".exe.exe", peName, StringComparison.OrdinalIgnoreCase);
    }
}
