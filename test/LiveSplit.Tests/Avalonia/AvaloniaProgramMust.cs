using LiveSplit.Avalonia;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class AvaloniaProgramMust
{
    [Fact]
    public void KeepWindowsDpiUnawareByDefaultLikeMaster()
    {
        Assert.Equal("Unaware", AvaloniaProgram.GetWin32DpiAwarenessName(enableDpiAwareness: false));
    }

    [Fact]
    public void UseSystemDpiAwarenessOnlyWhenSettingIsEnabledLikeMaster()
    {
        Assert.Equal("SystemDpiAware", AvaloniaProgram.GetWin32DpiAwarenessName(enableDpiAwareness: true));
    }
}
