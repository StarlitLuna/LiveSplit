// Stub source used when LiveSplit.Racetime is built on non-Windows hosts. The real Racetime
// integration is excluded by the OS-conditional ItemGroup in LiveSplit.Racetime.csproj
// (Microsoft.Web.WebView2 + DarkUI + Ookii.Dialogs.WinForms are all Windows-only). Linux
// users who want racetime.gg integration would need a WebView2 alternative, out of scope
// for the current Linux port.
namespace LiveSplit.Racetime
{
    internal static class LinuxStub
    {
    }
}
