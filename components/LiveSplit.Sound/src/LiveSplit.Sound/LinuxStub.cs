// Stub source used when LiveSplit.Sound is built on non-Windows hosts. The real
// SoundComponent + SoundFactory + SoundSettings are excluded by the OS-conditional
// ItemGroup in LiveSplit.Sound.csproj (NAudio is Windows-only). Phase 9 (post-MVP) can
// add a Linux audio backend if there's demand; until then, audio cues are a Windows-only
// feature.
namespace LiveSplit.Sound
{
    internal static class LinuxStub
    {
    }
}
