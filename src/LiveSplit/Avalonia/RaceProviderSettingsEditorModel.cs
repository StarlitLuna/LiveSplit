using System.Collections.Generic;
using System.Linq;

using LiveSplit.Options;

namespace LiveSplit.Avalonia;

public sealed class RaceProviderSettingsEditorModel
{
    private readonly IList<RaceProviderSettings> _originalSettings;

    public RaceProviderSettingsEditorModel(IList<RaceProviderSettings> settings)
    {
        _originalSettings = settings;
        WorkingSettings = settings.Select(x => (RaceProviderSettings)x.Clone()).ToList();
    }

    public IList<RaceProviderSettings> WorkingSettings { get; }

    public void SetEnabled(int index, bool enabled)
    {
        if (index < 0 || index >= WorkingSettings.Count)
        {
            return;
        }

        WorkingSettings[index].Enabled = enabled;
    }

    public void Apply()
    {
        _originalSettings.Clear();
        foreach (RaceProviderSettings settings in WorkingSettings)
        {
            _originalSettings.Add((RaceProviderSettings)settings.Clone());
        }
    }
}
