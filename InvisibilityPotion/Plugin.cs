using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Utils;
using Cfg = InvisibilityPotion.Config;

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
            PatchHealth.TargetKey("BaseAI", "CanHearTarget"),
            PatchHealth.TargetKey("BaseAI", "CanSeeTarget"),
            PatchHealth.TargetKey("BaseAI", "FindEnemy"),
        };

        public static IReadOnlyList<string> MissingPatches { get; private set; } = new List<string>();

        private void Awake()
        {
            Log = Logger;
            Config.SaveOnConfigSet = true;
            try
            {
                Cfg.PluginConfig.Bind(Config);
            }
            catch (Exception e)
            {
                Log.LogError($"Config binding failed: {e}");
            }
            Effects.StatusEffects.Register();
            // Server-synced values arrive after join; rebuild the snapshots so they take effect.
            Jotunn.Managers.SynchronizationManager.OnConfigurationSynchronized += (s, e) => Cfg.PluginConfig.Refresh();
            HarmonyInstance = new Harmony(PluginGuid);
            // Per-class patching: one unresolvable target must not abort Awake before the health check runs.
            foreach (var type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                try
                {
                    HarmonyInstance.CreateClassProcessor(type).Patch();
                }
                catch (Exception e)
                {
                    Log.LogError($"Failed to apply patches in {type.FullName}: {e.Message}");
                }
            }
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
