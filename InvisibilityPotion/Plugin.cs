using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Utils;

namespace InvisibilityPotion
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "saikopsych.InvisibilityPotion";
        public const string PluginName = "InvisibilityPotion";
        public const string PluginVersion = "0.1.0";

        public static ManualLogSource Log { get; private set; }
        public static Harmony HarmonyInstance { get; private set; }

        private void Awake()
        {
            Log = Logger;
            HarmonyInstance = new Harmony(PluginGuid);
            HarmonyInstance.PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void OnDestroy()
        {
            HarmonyInstance?.UnpatchSelf();
        }
    }
}
