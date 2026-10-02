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
        public const string PluginVersion = "0.2.0";

        public static ManualLogSource Log { get; private set; }
        public static Harmony HarmonyInstance { get; private set; }

        /// <summary>Game methods this mod must have patched for its features to work. Later plans add entries.</summary>
        public static readonly List<string> ExpectedPatchTargets = new List<string>
        {
#if DEBUG
            PatchHealth.TargetKey("FejdStartup", "Start"),
            PatchHealth.TargetKey("PlayerController", "TakeInput"),
#endif
            PatchHealth.TargetKey("BaseAI", "CanHearTarget"),
            PatchHealth.TargetKey("BaseAI", "CanSeeTarget"),
            PatchHealth.TargetKey("BaseAI", "FindEnemy"),
            PatchHealth.TargetKey("MonsterAI", "UpdateTarget"),
            PatchHealth.TargetKey("MonsterAI", "UpdateSleep"),
            PatchHealth.TargetKey("Player", "UpdateStealth"),
            PatchHealth.TargetKey("Character", "Damage"),
            PatchHealth.TargetKey("Attack", "StartDraw"),
            PatchHealth.TargetKey("Humanoid", "StartAttack"),
            PatchHealth.TargetKey("Humanoid", "BlockAttack"),
            PatchHealth.TargetKey("VisEquipment", "UpdateLodgroup"),
            PatchHealth.TargetKey("VisEquipment", "SetChestEquipped"),
            PatchHealth.TargetKey("VisEquipment", "SetLegEquipped"),
            PatchHealth.TargetKey("Player", "CanConsumeItem"),
            PatchHealth.TargetKey("EnemyHud", "TestShow"),
            PatchHealth.TargetKey("ZNet", "UpdatePlayerList"),
            PatchHealth.TargetKey("ZDOMan", "SendZDOs"),
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
            try
            {
                Items.Localization.Register();
            }
            catch (Exception e)
            {
                Log.LogError($"Localization registration failed: {e}");
            }
            try
            {
                Effects.StatusEffects.Register();
            }
            catch (Exception e)
            {
                Log.LogError($"Status effect registration failed: {e}");
            }
            try
            {
                Items.AssetBundles.Load();
            }
            catch (Exception e)
            {
                Log.LogError($"Asset bundle load failed: {e}");
            }
            // Server-synced values arrive after join; rebuild the snapshots so they take effect.
            Jotunn.Managers.SynchronizationManager.OnConfigurationSynchronized += (s, e) => Cfg.PluginConfig.Refresh();
            Jotunn.Managers.PrefabManager.OnVanillaPrefabsAvailable += RegisterItemsOnce;
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
            try
            {
                gameObject.AddComponent<Visuals.VeilController>();
            }
            catch (Exception e)
            {
                Log.LogError($"Veil controller setup failed: {e}");
            }
            try
            {
                gameObject.AddComponent<Plants.VeilSightDriver>();   // plan 5: goggle level poll and plant visibility
            }
            catch (Exception e)
            {
                Log.LogError($"Veil sight driver setup failed: {e}");
            }
#if DEBUG
            Dev.DevCommands.Register();
            gameObject.AddComponent<Dev.FogTuningWindow>();
#endif
            Log.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void RegisterItemsOnce()
        {
            Jotunn.Managers.PrefabManager.OnVanillaPrefabsAvailable -= RegisterItemsOnce;
            try
            {
                Items.PotionItems.Register();
            }
            catch (Exception e)
            {
                Log.LogError($"Item registration failed: {e}");
            }
            try
            {
                Items.ModelItems.Register();
            }
            catch (Exception e)
            {
                Log.LogError($"Model registration failed: {e}");
            }
        }

        private void OnDestroy()
        {
            HarmonyInstance?.UnpatchSelf();
        }
    }
}
