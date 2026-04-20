using BepInEx.Configuration;
using CSync.Extensions;
using CSync.Lib;
using System.Runtime.Serialization;
using WeatherProbe.Misc.Util;

namespace WeatherProbe.Misc
{
    [DataContract]
    public class PluginConfig : SyncedConfig2<PluginConfig>
    {
        [field: SyncedEntryField] public SyncedEntry<int> RANDOM_PRICE { get; set; }
        [field: SyncedEntryField] public SyncedEntry<int> SPECIFIED_PRICE { get; set; }
        [field: SyncedEntryField] public SyncedEntry<bool> RANDOM_ALWAYS_CLEAR { get; set; }
        [field: SyncedEntryField] public SyncedEntry<string> BlacklistWeathers { get; set; }
		public const char BlacklistWeatherDelimiter = ',';
		[field: SyncedEntryField] public SyncedEntry<int> InitialPurchasePrice { get; set; }
		[field: SyncedEntryField] public SyncedEntry<string> WeathersPrice { get; set; }
        public const char IndividualWeatherPriceDelimiter = ':';
        public const char IndividualWeatherDelimiter = ',';
        public PluginConfig(ConfigFile cfg) : base(Metadata.GUID)
        {
            string topSection = "General";
            RANDOM_PRICE = cfg.BindSyncedEntry(topSection, Constants.WEATHER_PROBE_PRICE_KEY, Constants.WEATHER_PROBE_PRICE_DEFAULT, Constants.WEATHER_PROBE_PRICE_DESCRIPTION);
            SPECIFIED_PRICE = cfg.BindSyncedEntry(topSection, Constants.WEATHER_PROBE_PICKED_WEATHER_PRICE_KEY, Constants.WEATHER_PROBE_PICKED_WEATHER_PRICE_DEFAULT, Constants.WEATHER_PROBE_PICKED_WEATHER_PRICE_DESCRIPTION);
            RANDOM_ALWAYS_CLEAR = cfg.BindSyncedEntry(topSection, Constants.WEATHER_PROBE_ALWAYS_CLEAR_KEY, Constants.WEATHER_PROBE_ALWAYS_CLEAR_DEFAULT, Constants.WEATHER_PROBE_ALWAYS_CLEAR_DESCRIPTION);
            BlacklistWeathers = cfg.BindSyncedEntry(topSection, "Blacklisted Weathers", "weather1" + BlacklistWeatherDelimiter + "weather2", "Any weathers listed here, separated by \"" + BlacklistWeatherDelimiter + "\" , will be blacklisted from selection when probing a moon.");
            InitialPurchasePrice = cfg.BindSyncedEntry(topSection, "Unlock Purchase Price", 0, "If greater than zero, the probe feature will be locked behind the price configurated here.");
            WeathersPrice = cfg.BindSyncedEntry(topSection, "Individual Weather Prices", $"weather1{IndividualWeatherPriceDelimiter}100{IndividualWeatherDelimiter}weather2{IndividualWeatherPriceDelimiter}200", "Dictionary in which the key is the name of the weather and the value is the probing price for the respective weather.");

            ConfigManager.Register(this);
        }
    }
}
