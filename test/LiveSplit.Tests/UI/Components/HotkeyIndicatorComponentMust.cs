using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI.Components;

using Xunit;

namespace LiveSplit.Tests.UI.Components;

public class HotkeyIndicatorComponentMust
{
    [Fact]
    public void FollowCurrentHotkeyProfileGlobalState()
    {
        ISettings settings = new StandardSettingsFactory().Create();
        var state = new LiveSplitState(null, null, null, new StandardLayoutSettingsFactory().Create(), settings)
        {
            CurrentHotkeyProfile = HotkeyProfile.DefaultHotkeyProfileName
        };

        Assert.False(HotkeyIndicatorComponent.AreGlobalHotkeysEnabled(state));

        settings.HotkeyProfiles[HotkeyProfile.DefaultHotkeyProfileName].GlobalHotkeysEnabled = true;

        Assert.True(HotkeyIndicatorComponent.AreGlobalHotkeysEnabled(state));
    }
}
