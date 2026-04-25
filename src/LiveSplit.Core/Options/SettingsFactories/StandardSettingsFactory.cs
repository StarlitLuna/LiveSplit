using System.Collections.Generic;
using System.Runtime.InteropServices;

using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.Input;

namespace LiveSplit.Options.SettingsFactories;

public class StandardSettingsFactory : ISettingsFactory
{
    public ISettings Create()
    {
        return new Settings()
        {
            HotkeyProfiles = new Dictionary<string, HotkeyProfile>()
            {
                {HotkeyProfile.DefaultHotkeyProfileName, new HotkeyProfile()
                    {
                        SplitKey = new KeyOrButton(Key.NumPad1),
                        ResetKey = new KeyOrButton(Key.NumPad3),
                        UndoKey = new KeyOrButton(Key.NumPad8),
                        SkipKey = new KeyOrButton(Key.NumPad2),
                        SwitchComparisonPrevious = new KeyOrButton(Key.NumPad4),
                        SwitchComparisonNext = new KeyOrButton(Key.NumPad6),
                        PauseKey = null,
                        ToggleGlobalHotkeys = null,
                        GlobalHotkeysEnabled = false,
                        DeactivateHotkeysForOtherPrograms = false,
                        DoubleTapPrevention = true,
                        AllowGamepadsAsHotkeys = false,
                        HotkeyDelay = 0f
                    }
                }
            },
            WarnOnReset = true,
            LastComparison = Run.PersonalBestComparisonName,
            UpdateCheckEnabled = RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
            SimpleSumOfBest = false,
            RefreshRate = 40,
            ServerPort = 16834,
            ServerStartup = ServerStartupType.Off,
            ServerState = ServerStateType.Off,
            EnableDPIAwareness = false,
            UILanguage = string.Empty,
            ComparisonGeneratorStates = new Dictionary<string, bool>()
            {
                { BestSegmentsComparisonGenerator.ComparisonName, true },
                { BestSplitTimesComparisonGenerator.ComparisonName, false },
                { AverageSegmentsComparisonGenerator.ComparisonName, true },
                { MedianSegmentsComparisonGenerator.ComparisonName, false },
                { WorstSegmentsComparisonGenerator.ComparisonName, false},
                { PercentileComparisonGenerator.ComparisonName, false },
                { LatestRunComparisonGenerator.ComparisonName, false },
                { HCPComparisonGenerator.ComparisonName, false },
                { NoneComparisonGenerator.ComparisonName, false }
            },
            HcpHistorySize = 20,
            HcpNBestRuns = 8
        };
    }
}
