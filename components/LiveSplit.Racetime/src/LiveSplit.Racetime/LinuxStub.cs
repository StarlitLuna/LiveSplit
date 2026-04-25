// Placeholder so the project compiles on non-Windows hosts where Microsoft.Web.WebView2,
// DarkUI, and Ookii.Dialogs.WinForms (and therefore the real Racetime sources) are
// unavailable. The OS-conditional ItemGroup in the .csproj excludes the real sources on
// those platforms.
namespace LiveSplit.Racetime
{
    internal static class LinuxStub
    {
    }
}
