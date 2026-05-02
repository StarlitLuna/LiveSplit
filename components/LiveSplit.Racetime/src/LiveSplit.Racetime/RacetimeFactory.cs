using System;

using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.UI.Components;

[assembly: ComponentFactory(typeof(LiveSplit.Racetime.RacetimeFactory))]

namespace LiveSplit.Racetime;

public class RacetimeFactory : IRaceProviderFactory
{
    public string UpdateName => "Racetime Integration";
    public string XMLURL => "http://livesplit.org/update/Components/update.LiveSplit.Racetime.v2.xml";
    public string UpdateURL => "http://livesplit.org/update/";
    public Version Version => Version.Parse("1.8.37");

    public RaceProviderAPI Create(ITimerModel model, RaceProviderSettings settings)
    {
        var api = new RacetimeAPI
        {
            Settings = settings
        };
        return api;
    }

    public RaceProviderSettings CreateSettings()
    {
        return new RacetimeSettings();
    }
}
