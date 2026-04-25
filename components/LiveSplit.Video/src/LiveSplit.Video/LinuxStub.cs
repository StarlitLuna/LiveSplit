// Stub source used when LiveSplit.Video is built on non-Windows hosts. The real
// VideoComponent + VideoFactory + VideoSettings are excluded by the OS-conditional
// ItemGroup in LiveSplit.Video.csproj (VLC ActiveX COM interop is Windows-only). A
// Linux-friendly libvlcsharp port is out of scope for the current Linux migration.
namespace LiveSplit.Video
{
    internal static class LinuxStub
    {
    }
}
