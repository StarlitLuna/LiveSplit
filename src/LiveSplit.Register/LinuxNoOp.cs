// Placeholder so the project compiles on non-Windows hosts. The OS-conditional ItemGroup
// in LiveSplit.Register.csproj excludes the real Program.cs / FiletypeRegistryHelper.cs /
// InternetExplorerBrowserEmulation.cs sources on those platforms. File associations on
// non-Windows hosts are handled by .desktop + xdg-mime at packaging time.
namespace LiveSplit.Register
{
    internal static class LinuxNoOp
    {
    }
}
