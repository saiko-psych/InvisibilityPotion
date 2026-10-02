using System.Collections.Generic;
using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace InvisibilityPotion.Plants
{
    /// <summary>
    /// Wild spawning of the ground plants (spec §3.3) through Jötunn's ZoneManager.AddCustomVegetation (which also registers the
    /// prefab). ZoneSystem.PlaceVegetation treats m_max &lt; 1 as the chance per zone for one placement batch (ZoneSystem.cs:1399-1405);
    /// the three variants share the zone chance, so each gets Max = chance / 3. Only zones generated after installing the mod get
    /// plants (ZoneSystem.cs:1337). Zone chances are read here, once (restart to change).
    /// </summary>
    public static class PlantVegetation
    {
        public static readonly List<CustomVegetation> Registered = new List<CustomVegetation>();
        private static bool _hooked;

        public static void Add(GameObject prefab, int tier)
        {
            var chance = tier == 2 ? Config.PluginConfig.BaldrZoneChance : Config.PluginConfig.HelFernZoneChance;
            var max = chance;
            if (max <= 0f)
            {
                PrefabManager.Instance.AddPrefab(new CustomPrefab(prefab, true));
                Plugin.Log.LogInfo($"plants: {prefab.name}: zone chance 0, registered without wild spawning");
                return;
            }
            var cfg = new VegetationConfig
            {
                Biome = tier == 2 ? Heightmap.Biome.Mountain : Heightmap.Biome.AshLands,
                BiomeArea = Heightmap.BiomeArea.Everything,
                BlockCheck = true,
                Min = 0f,
                Max = max,
                GroupSizeMin = tier == 2 ? 1 : 3,
                GroupSizeMax = tier == 2 ? 2 : 6,
                GroupRadius = tier == 2 ? 4f : 6f,
                MinTilt = 0f,
                MaxTilt = tier == 2 ? 25f : 30f,
                ScaleMin = PlantYield.ScaleRanges[tier].min,
                ScaleMax = PlantYield.ScaleRanges[tier].max,
            };
            var veg = new CustomVegetation(prefab, true, cfg);
            if (!ZoneManager.Instance.AddCustomVegetation(veg))
            {
                Plugin.Log.LogError($"plants: AddCustomVegetation({prefab.name}) refused; registering the prefab without wild spawning");
                PrefabManager.Instance.AddPrefab(new CustomPrefab(prefab, true));
                return;
            }
            Registered.Add(veg);
            if (tier == 3)
            {
                // Ashlands lava is painted into the vegetation mask; PlaceVegetation checks the mask only when min != max
                // (ZoneSystem.cs:1453-1459, fields :70/:72). CustomVegetation.Vegetation is the ZoneVegetation Jötunn injects.
                veg.Vegetation.m_minVegetation = 0f;
                veg.Vegetation.m_maxVegetation = 0.5f;
                Plugin.Log.LogInfo($"plants: vegetation {prefab.name}: vegetation mask {veg.Vegetation.m_minVegetation}-{veg.Vegetation.m_maxVegetation} (keeps ferns off lava)");
            }
            Plugin.Log.LogInfo($"plants: vegetation {prefab.name}: biome {cfg.Biome}, max {cfg.Max:0.###} per zone, " +
                               $"group {cfg.GroupSizeMin}-{cfg.GroupSizeMax} r {cfg.GroupRadius} m, tilt {cfg.MinTilt}-{cfg.MaxTilt}, scale {cfg.ScaleMin}-{cfg.ScaleMax}");
            if (!_hooked)
            {
                _hooked = true;
                ZoneManager.OnVegetationRegistered += LogInjected;
            }
        }

        /// <summary>Proof that the entries reached ZoneSystem.m_vegetation (ZoneSystem.cs:500) after Jötunn's injection.</summary>
        private static void LogInjected()
        {
            var zs = ZoneSystem.instance;
            if (zs == null) return;
            var ours = zs.m_vegetation.Where(v => v?.m_prefab != null && (v.m_prefab.name.StartsWith(PlantPrefabs.BaldrName, System.StringComparison.Ordinal) || v.m_prefab.name.StartsWith(PlantPrefabs.FernName, System.StringComparison.Ordinal))).ToList();
            Plugin.Log.LogInfo($"plants: {ours.Count} of {zs.m_vegetation.Count} ZoneSystem vegetation entries are ours: " +
                               string.Join(", ", ours.Select(v => $"{v.m_prefab.name} (biome {v.m_biome}, max {v.m_max:0.###}, enable {v.m_enable})")));
        }

        /// <summary>Lines for ip_veg: what ZoneSystem holds for our prefabs right now.</summary>
        public static IEnumerable<string> Describe()
        {
            foreach (var cv in Registered)
            {
                var zv = ZoneManager.Instance.GetZoneVegetation(cv.Name);
                if (zv == null) { yield return $"{cv.Name}: not in ZoneSystem"; continue; }
                var inSystem = ZoneSystem.instance != null && ZoneSystem.instance.m_vegetation.Contains(zv);
                yield return $"{cv.Name}: biome {zv.m_biome}, area {zv.m_biomeArea}, max {zv.m_max:0.###}/zone, group {zv.m_groupSizeMin}-{zv.m_groupSizeMax} r {zv.m_groupRadius}, " +
                             $"tilt {zv.m_minTilt}-{zv.m_maxTilt}, vegetation mask {zv.m_minVegetation}-{zv.m_maxVegetation}, scale {zv.m_scaleMin}-{zv.m_scaleMax}, altitude {zv.m_minAltitude}..{zv.m_maxAltitude}, block {zv.m_blockCheck}, enable {zv.m_enable}, in ZoneSystem {inSystem}";
            }
        }
    }
}
