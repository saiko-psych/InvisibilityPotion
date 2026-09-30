using System.Collections.Generic;
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

        /// <summary>Game methods this mod must have patched for its features to work. Later plans add entries.</summary>
        public static readonly List<string> ExpectedPatchTargets = new List<string>
        {
#if DEBUG
            PatchHealth.TargetKey("FejdStartup", "Start"),
#endif
        };

        public static IReadOnlyList<string> MissingPatches { get; private set; } = new List<string>();

        private void Awake()
        {
            Log = Logger;
            HarmonyInstance = new Harmony(PluginGuid);
            HarmonyInstance.PatchAll(typeof(Plugin).Assembly);
            MissingPatches = PatchHealth.Report(HarmonyInstance, ExpectedPatchTargets);
#if DEBUG
            Dev.DevCommands.Register();
#endif
            Log.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void OnDestroy()
        {
            HarmonyInstance?.UnpatchSelf();
        }
    }
}
