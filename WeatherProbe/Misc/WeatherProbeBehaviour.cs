using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;

namespace WeatherProbe.Misc
{
    public class WeatherProbeBehaviour : NetworkBehaviour
    {
        internal const string COMMAND_NAME = "Weather Probe";
        internal static WeatherProbeBehaviour Instance { get; set; }
        internal readonly static Dictionary<string, LevelWeatherType> probedWeathers = [];
        static void SetInstance(WeatherProbeBehaviour instance )
        {
            Instance = instance;
        }
        void Start()
        {
            SetInstance(this);
            DontDestroyOnLoad(gameObject);
        }
        [ServerRpc(RequireOwnership = false)]
        internal void SyncWeatherServerRpc(string level, LevelWeatherType selectedWeather)
        {
            SyncWeatherClientRpc(level, selectedWeather);
        }

        [ClientRpc]
        internal void SyncWeatherClientRpc(string level, LevelWeatherType selectedWeather)
        {
            SyncWeather(level, selectedWeather);
        }

        internal void SyncWeather(string level, LevelWeatherType selectedWeather)
        {
            SelectableLevel[] availableLevels = StartOfRound.Instance.levels;
            SelectableLevel selectedLevel = availableLevels.First(x => x.PlanetName.Contains(level));

            probedWeathers[selectedLevel.PlanetName] = selectedWeather;
            if(WeatherRegistryPatches.IsModPresent){
                // if weatherregistry is present, don't use probe's syncing
                WeatherRegistryPatches.SetWeatherOnHost(selectedLevel, selectedWeather);
                return;
            }

            if (selectedLevel.overrideWeather) selectedLevel.overrideWeatherType = selectedWeather;
            else selectedLevel.currentWeather = selectedWeather;
            if (selectedLevel == StartOfRound.Instance.currentLevel) StartOfRound.Instance.SetMapScreenInfoToCurrentLevel();
        }
        [ServerRpc(RequireOwnership = false)]
        internal void SyncProbeWeathersServerRpc()
        {
            foreach (string level in probedWeathers.Keys.ToList())
            {
                SyncWeatherClientRpc(level, probedWeathers[level]);
            }
        }

        internal static (string, LevelWeatherType) RandomizeWeather(ref SelectableLevel level)
        {
            if (Plugin.Config.RANDOM_ALWAYS_CLEAR) return (level.PlanetName, LevelWeatherType.None);

            LevelWeatherType selectedWeather = level.overrideWeather ? level.overrideWeatherType : level.currentWeather;
            LevelWeatherType[] allowedWeathers = level.randomWeathers.Select(x => x.weatherType).Where(x => x != selectedWeather).ToArray();
            int selectedWeatherValue = UnityEngine.Random.Range(0, allowedWeathers.Length + 1);
            if (selectedWeatherValue == allowedWeathers.Length)
            {
                if (selectedWeather == LevelWeatherType.None)
                {
                    LevelWeatherType newSelectedWeather = allowedWeathers[UnityEngine.Random.Range(0, allowedWeathers.Length)];
                    return (level.PlanetName, newSelectedWeather);
                }
                else
                {
                    return (level.PlanetName, LevelWeatherType.None);
                }
            }
            else
            {
                LevelWeatherType newSelectedWeather = allowedWeathers[selectedWeatherValue];
                return (level.PlanetName, newSelectedWeather);
            }
        }
    }
}
