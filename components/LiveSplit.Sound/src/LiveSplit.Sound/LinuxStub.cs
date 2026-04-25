// Placeholder so the project compiles on non-Windows hosts where NAudio (and therefore the
// real SoundComponent / SoundFactory / SoundSettings sources) is unavailable. The
// OS-conditional ItemGroup in the .csproj excludes the real sources on those platforms.
namespace LiveSplit.Sound
{
    internal static class LinuxStub
    {
    }
}
