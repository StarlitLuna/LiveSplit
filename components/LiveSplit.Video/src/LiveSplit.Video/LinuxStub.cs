// Placeholder so the project compiles on non-Windows hosts where the VLC ActiveX COM
// interop bindings (and therefore the real VideoComponent / VideoFactory / VideoSettings
// sources) are unavailable. The OS-conditional ItemGroup in the .csproj excludes the real
// sources on those platforms.
namespace LiveSplit.Video
{
    internal static class LinuxStub
    {
    }
}
