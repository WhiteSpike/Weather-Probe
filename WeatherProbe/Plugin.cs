using BepInEx;
using BepInEx.Logging;
using WeatherProbe.Misc.UI.Application;
using HarmonyLib;
using InteractiveTerminalAPI.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using WeatherProbe.Misc;
using WeatherProbe.Patches.RoundComponents;
using WeatherProbe.Patches.TerminalComponents;
using WeatherProbe.Patches;
namespace WeatherProbe
{
    [BepInPlugin(Metadata.GUID,Metadata.NAME,Metadata.VERSION)]
    [BepInDependency("com.sigurd.csync")]
    [BepInDependency("evaisa.lethallib")]
    [BepInDependency("WhiteSpike.InteractiveTerminalAPI")]
    [BepInDependency("mrov.WeatherRegistry", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal static readonly Harmony harmony = new(Metadata.GUID);
        internal static readonly ManualLogSource mls = BepInEx.Logging.Logger.CreateLogSource(Metadata.NAME);

        public new static PluginConfig Config;
        internal static GameObject networkPrefab;

        void Awake()
        {
            Config = new PluginConfig(base.Config);

            // netcode patching stuff
            IEnumerable<Type> types;
            try
            {
                types = Assembly.GetExecutingAssembly().GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null);
            }
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }

            PatchMainVersion();
            networkPrefab = LethalLib.Modules.NetworkPrefabs.CreateNetworkPrefab("Weather Probe");
            networkPrefab.AddComponent<WeatherProbeBehaviour>();
            InteractiveTerminalManager.RegisterApplication<WeatherProbeApplication>("probe", caseSensitive: false);

            mls.LogInfo($"{Metadata.NAME} {Metadata.VERSION} has been loaded successfully.");
        }
        internal static void PatchMainVersion()
        {
            PatchVitalComponents();
        }
        static void PatchVitalComponents()
        {
            harmony.PatchAll(typeof(StartOfRoundPatcher));
            harmony.PatchAll(typeof(StartMatchLeverPatcher));
            harmony.PatchAll(typeof(TerminalPatcher));
            mls.LogInfo("Game managers have been patched");
        }
    }   
}
