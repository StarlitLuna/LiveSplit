// Empty stub used as the only source file when LiveSplit.Register is built on non-Windows
// hosts. The real Program.cs / FiletypeRegistryHelper.cs / InternetExplorerBrowserEmulation.cs
// are excluded by the OS-conditional ItemGroup in LiveSplit.Register.csproj. Linux file
// associations are handled via .desktop + xdg-mime in the AppImage / tarball packaging.
namespace LiveSplit.Register
{
    internal static class LinuxNoOp
    {
    }
}
