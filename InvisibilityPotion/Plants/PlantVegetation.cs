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
    /// one mixed prefab per plant carries the zone chance, each group member picks its model variant from its own position
    /// (VeilHarvest). Only zones generated after installing the mod get plants (ZoneSystem.cs:1337). Zone chances are read here,
    /// once (restart to change). Group members sit <see cref="GroundOffset"/> in the ground and follow the slope
    /// (<see cref="GroundTiltChance"/>), so a group looks embedded rather than stamped (ZoneSystem.cs:1532-1541).
    /// </summary>
    public static class PlantVegetation
    {
        /// <summary>ZoneVegetation.m_groundOffset (ZoneSystem.cs:104, added to the ground height at :1532): slightly sunk.</summary>
        public const float GroundOffset = -0.03f;
        /// <summary>
        /// ZoneVegetation.m_chanceToUseGroundTilt (ZoneSystem.cs:54, :1534-1537): 1 = every plant is rotated onto the ground normal.
        /// Jötunn 2.30.0's (and 2.30.2's) VegetationConfig has no property for it (ilspy), so it is set on CustomVegetation.Vegetation.
        /// </summary>
        public const float GroundTiltChance = 1f;
        /// <summary>
        /// ZoneVegetation.m_minAltitude (ZoneSystem.cs:66): PlaceVegetation rejects a point whose ground height minus 30 (hard-coded
        /// sea level, ZoneSystem.cs:1448-1452; m_waterLevel :472 is also 30; p.y is the terrain raycast hit, :2903-2911) is outside
        /// [m_minAltitude, m_maxAltitude]. 0.5 m keeps every group member on dry land (each member is checked at its own position).
        /// Vanilla Ashlands bushes use 1 (bundle data).
        /// </summary>
        public const float MinAltitude = 0.5f;
        /// <summary>
        /// ZoneVegetation.m_maxVegetation for the fern (ZoneSystem.cs:72, checked at :1453-1460 only when min != max). Ashlands lava is
        /// the paint-mask alpha, the same channel Heightmap.GetVegetationMask reads (Heightmap.cs:925-930; IsLava/GetLava read it
        /// too, :958-978, damage from 0.6). 0.5 still allowed glowing lava edges; vanilla Ashlands bushes use 0.15, trees and smoke
        /// puffs 0.2 (bundle data), so the fern uses 0.15.
        /// </summary>
        public const float FernMaxLavaMask = 0.15f;

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
                GroundOffset = GroundOffset,
                MinAltitude = MinAltitude,
            };
            var veg = new CustomVegetation(prefab, true, cfg);
            if (!ZoneManager.Instance.AddCustomVegetation(veg))
            {
                Plugin.Log.LogError($"plants: AddCustomVegetation({prefab.name}) refused; registering the prefab without wild spawning");
                PrefabManager.Instance.AddPrefab(new CustomPrefab(prefab, true));
                return;
            }
            Registered.Add(veg);
            veg.Vegetation.m_chanceToUseGroundTilt = GroundTiltChance;
            // Dry, solid land only. CustomVegetation.Vegetation is the ZoneVegetation Jötunn injects; set again here so the values do
            // not depend on how Jötunn maps its config. Ocean depth: equal min and max switch the check off (ZoneSystem.cs:1461-1468);
            // Heightmap.GetOceanDepth interpolates 30 minus the corner heights of the whole heightmap (Heightmap.cs:366-378, :385-393), too
            // coarse for a plant, and no vanilla Ashlands/Mountains entry uses it. The altitude check covers water per point.
            veg.Vegetation.m_minAltitude = MinAltitude;
            veg.Vegetation.m_minOceanDepth = 0f;
            veg.Vegetation.m_maxOceanDepth = 0f;
            veg.Vegetation.m_blockCheck = true;   // IsBlocked: Default/static_solid/Default_small/piece above the point (ZoneSystem.cs:2732, mask :677)
            if (tier == 3)
            {
                veg.Vegetation.m_minVegetation = 0f;
                veg.Vegetation.m_maxVegetation = FernMaxLavaMask;
                Plugin.Log.LogInfo($"plants: vegetation {prefab.name}: lava mask {veg.Vegetation.m_minVegetation}-{veg.Vegetation.m_maxVegetation} (keeps ferns off lava)");
            }
            Plugin.Log.LogInfo($"plants: vegetation {prefab.name}: biome {cfg.Biome}, max {cfg.Max:0.###} per zone, " +
                               $"group {cfg.GroupSizeMin}-{cfg.GroupSizeMax} r {cfg.GroupRadius} m, tilt {cfg.MinTilt}-{cfg.MaxTilt}, scale {cfg.ScaleMin}-{cfg.ScaleMax}, " +
                               $"ground offset {veg.Vegetation.m_groundOffset} m, ground tilt chance {veg.Vegetation.m_chanceToUseGroundTilt}, " +
                               $"altitude {veg.Vegetation.m_minAltitude}..{veg.Vegetation.m_maxAltitude} m above sea, block {veg.Vegetation.m_blockCheck}");
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
                             $"tilt {zv.m_minTilt}-{zv.m_maxTilt}, ground offset {zv.m_groundOffset}, ground tilt chance {zv.m_chanceToUseGroundTilt}, vegetation mask {zv.m_minVegetation}-{zv.m_maxVegetation}, scale {zv.m_scaleMin}-{zv.m_scaleMax}, altitude {zv.m_minAltitude}..{zv.m_maxAltitude} m above sea, ocean depth {zv.m_minOceanDepth}-{zv.m_maxOceanDepth} (equal = off), block {zv.m_blockCheck}, enable {zv.m_enable}, in ZoneSystem {inSystem}";
            }
        }
    }
}
