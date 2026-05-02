using System;

using LiveSplit.Model;
using LiveSplit.UI.Components;

[assembly: ComponentFactory(typeof(HotkeyIndicatorFactory))]

namespace LiveSplit.UI.Components;

public class HotkeyIndicatorFactory : IComponentFactory
{
    public string ComponentName => "Hotkey Indicator";

    public string Description => "Displays whether global hotkeys are enabled.";

    public ComponentCategory Category => ComponentCategory.Other;

    public IComponent Create(LiveSplitState state)
    {
        return new HotkeyIndicatorComponent();
    }

    public string UpdateName => ComponentName;

    public string XMLURL => "http://livesplit.org/update/Components/update.LiveSplit.HotkeyIndicator.xml";

    public string UpdateURL => "http://livesplit.org/update/";

    public Version Version => Version.Parse("1.8.31");
}
