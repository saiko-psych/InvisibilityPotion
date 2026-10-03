using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace InvisibilityPotion.Plants
{
    /// <summary>
    /// 0.4.0: wild Baldr's Tear / Hel's Ember Fern for zones that were generated before the mod (or before the plant existed).
    /// Vanilla places vegetation only when a zone is generated for the first time (ZoneSystem.SpawnZone, ZoneSystem.cs:1337), so a
    /// long-played world never gets our plants. The server (host or dedicated) scans the zones around every peer each tick
    /// (ZoneSystem.CreateGhostZones, :1222, called every 0.1 s per peer from Update :1185-1188) and only generates missing ones; the
    /// Harmony postfix (<see cref="Patches.ZoneRetrofitPatch"/>) lets <see cref="Run"/> look at one already generated, unchecked
    /// zone per call. A zone that holds one of our wild plants (ZDO prefab hash, ZDOMan.m_objectsBySector :106 by
    /// ZoneSystem.SectorToIndex :2991) is done. Otherwise vanilla's own PlaceVegetation (:1385) runs with ZoneSystem.m_vegetation
    /// temporarily reduced to our entries, in Ghost mode (ZDOs only, instances destroyed like SpawnZone does :1346-1354), on the
    /// loaded zone root when there is one (host) or on a throw-away instance of m_zonePrefab (:466) when the terrain is ready
    /// (HeightmapBuilder.IsTerrainReady, :1327). PlaceVegetation seeds Random from world seed + zone + prefab name (:1397), so a
    /// zone rolls the same way on every load and the same way a fresh zone would have: a zone generated with the mod that missed
    /// its roll misses again, nothing accumulates. Location clear areas are unknown here (empty list): a plant can stand next to a
    /// location entrance; m_blockCheck still keeps it out of buildings.
    /// </summary>
    public static class ZoneRetrofit
    {
        public static readonly ZoneRetrofitPlanner Planner = new ZoneRetrofitPlanner();
        private static ZoneSystem _plannedFor;   // the planner is per world: a new ZoneSystem resets it
        private static readonly HashSet<int> WildHashes = new HashSet<int> { PlantPrefabs.BaldrName.GetStableHashCode(), PlantPrefabs.FernName.GetStableHashCode() };
        private static int _placedZones, _placedPlants, _loggedErrors;
        private static readonly List<GameObject> Spawned = new List<GameObject>();

        private static readonly MethodInfo IsZoneGeneratedMethod = AccessTools.Method(typeof(ZoneSystem), "IsZoneGenerated");
        private static readonly MethodInfo PlaceVegetationMethod = AccessTools.Method(typeof(ZoneSystem), "PlaceVegetation");
        private static readonly Type ClearAreaType = AccessTools.Inner(typeof(ZoneSystem), "ClearArea");
        private static readonly FieldInfo SimulationDistanceField = AccessTools.Field(typeof(ZoneSystem), "m_simulationDistance");
        private static readonly FieldInfo ZonesField = AccessTools.Field(typeof(ZoneSystem), "m_zones");
        private static readonly FieldInfo ObjectsBySectorField = AccessTools.Field(typeof(ZDOMan), "m_objectsBySector");

        public static bool Available => IsZoneGeneratedMethod != null && PlaceVegetationMethod != null && ClearAreaType != null && SimulationDistanceField != null && ObjectsBySectorField != null;

        /// <summary>Called from the CreateGhostZones postfix with the peer's reference position. At most one zone per call.</summary>
        public static void Run(Vector3 refPoint)
        {
            try
            {
                var zs = ZoneSystem.instance;
                if (zs == null || !Config.PluginConfig.RetrofitExistingZones || !Available) return;
                if (ZNet.instance == null || !ZNet.instance.IsServer() || PlantVegetation.Registered.Count == 0 || WorldGenerator.instance == null) return;
                if (_plannedFor != zs) { Planner.Reset(); _plannedFor = zs; _placedZones = _placedPlants = 0; }
                var centre = ZoneSystem.GetZone(refPoint);
                var simObj = SimulationDistanceField.GetValue(zs);
                var radius = simObj is SimulationDistance sim ? sim.NearSimulationDistance : 2;   // struct, boxed by reflection
                if (!Planner.Next(centre.x, centre.y, radius, (x, y) => IsGenerated(zs, x, y), out var zx, out var zy)) return;
                var outcome = Check(zs, zx, zy, out var placed);
                if (outcome == RetrofitOutcome.TerrainNotReady) Planner.Unmark(zx, zy);
                if (outcome == RetrofitOutcome.Placed || outcome == RetrofitOutcome.NoSpot)
                    Plugin.Log.LogInfo($"plants: retrofit zone ({zx},{zy}): {outcome}, {placed} placed");
            }
            catch (Exception e)
            {
                if (_loggedErrors++ < 3) Plugin.Log.LogWarning($"plants: zone retrofit failed: {e}");
            }
        }

        /// <summary>ip_retrofit here: check the given zone now, whatever the planner remembers.</summary>
        public static string RunNow(Vector2s zone)
        {
            if (ZoneSystem.instance == null) return "no ZoneSystem";
            if (!Available) return "retrofit unavailable: a game member was not found (see patch health)";
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return "not the server: the server runs the retrofit";
            if (!IsGenerated(ZoneSystem.instance, zone.x, zone.y)) return $"zone ({zone.x},{zone.y}) is not generated yet: vanilla places the plants itself";
            var outcome = Check(ZoneSystem.instance, zone.x, zone.y, out var placed);
            return $"zone ({zone.x},{zone.y}): {outcome}, {placed} placed (RetrofitExistingZones={Config.PluginConfig.RetrofitExistingZones})";
        }

        public static void Reset() => Planner.Reset();

        public static IEnumerable<string> Describe()
        {
            yield return $"retrofit: enabled {Config.PluginConfig.RetrofitExistingZones}, available {Available}, server {(ZNet.instance != null && ZNet.instance.IsServer())}, " +
                         $"zones checked this session {Planner.CheckedCount}, zones with new plants {_placedZones}, plants placed {_placedPlants}";
            var p = Player.m_localPlayer;
            if (p != null && ZoneSystem.instance != null)
            {
                var z = ZoneSystem.GetZone(p.transform.position);
                yield return $"  current zone ({z.x},{z.y}): generated {IsGenerated(ZoneSystem.instance, z.x, z.y)}, checked {Planner.IsChecked(z.x, z.y)}, wild plants {CountWild(z)}";
            }
        }

        private static bool IsGenerated(ZoneSystem zs, int x, int y) =>
            (bool)IsZoneGeneratedMethod.Invoke(zs, new object[] { new Vector2s((short)x, (short)y) });

        /// <summary>Our wild plants (mixed prefab, not the cultivated variants) whose ZDO sits in the zone's sector.</summary>
        private static int CountWild(Vector2s zone)
        {
            var zdoMan = ZDOMan.instance;
            if (zdoMan == null) return 0;
            var sectors = ObjectsBySectorField.GetValue(zdoMan) as List<ZDO>[];
            if (sectors == null) return 0;
            var index = ZoneSystem.SectorToIndex(zone).Sector;
            if (index >= sectors.Length) return 0;
            var list = sectors[index];
            if (list == null) return 0;
            var n = 0;
            foreach (var zdo in list)
                if (zdo != null && WildHashes.Contains(zdo.GetPrefab())) n++;
            return n;
        }

        private static RetrofitOutcome Check(ZoneSystem zs, int x, int y, out int placed)
        {
            placed = 0;
            var zone = new Vector2s((short)x, (short)y);
            if (CountWild(zone) > 0) return RetrofitOutcome.HasPlants;
            var zonePos = ZoneSystem.GetZonePos(zone);
            var ours = PlantVegetation.Registered.Select(v => v.Vegetation).Where(v => v != null).ToList();
            if (ours.Count == 0) return RetrofitOutcome.Skipped;
            // Will the roll hit at all? Same seeding as PlaceVegetation (ZoneSystem.cs:1397-1405), evaluated without touching the world.
            var seed = WorldGenerator.instance.GetSeed();
            var anyRoll = false;
            var state = UnityEngine.Random.state;
            foreach (var veg in ours)
            {
                UnityEngine.Random.InitState(seed + zone.x * 4271 + zone.y * 9187 + veg.m_prefab.name.GetStableHashCode());
                if (veg.m_max >= 1f || UnityEngine.Random.value <= veg.m_max) anyRoll = true;
            }
            UnityEngine.Random.state = state;
            if (!anyRoll) return RetrofitOutcome.NoRoll;

            // A root with a heightmap: the loaded zone (host) or a throw-away zone prefab instance (dedicated server, ghost zones).
            GameObject root = null, temp = null;
            var zones = ZonesField?.GetValue(zs) as IDictionary;
            if (zones != null && zones.Contains(zone))
                root = Traverse.Create(zones[zone]).Field("m_root").GetValue<GameObject>();
            if (root == null)
            {
                var hm = zs.m_zonePrefab.GetComponentInChildren<Heightmap>();
                if (!HeightmapBuilder.instance.IsTerrainReady(zonePos, hm.m_width, hm.m_scale, hm.IsDistantLod, WorldGenerator.instance))
                    return RetrofitOutcome.TerrainNotReady;
                temp = UnityEngine.Object.Instantiate(zs.m_zonePrefab, zonePos, Quaternion.identity);
                root = temp;
            }
            var hmap = root.GetComponentInChildren<Heightmap>();
            if (hmap == null) { if (temp != null) UnityEngine.Object.Destroy(temp); return RetrofitOutcome.TerrainNotReady; }
            if (!hmap.HaveBiome(Heightmap.Biome.Mountain) && !hmap.HaveBiome(Heightmap.Biome.AshLands))
            {
                if (temp != null) UnityEngine.Object.Destroy(temp);
                return RetrofitOutcome.NoRoll;   // neither of our biomes: PlaceVegetation would skip every entry
            }

            var all = zs.m_vegetation;
            Spawned.Clear();
            try
            {
                zs.m_vegetation = ours;
                var clear = Activator.CreateInstance(typeof(List<>).MakeGenericType(ClearAreaType));
                PlaceVegetationMethod.Invoke(zs, new object[] { zone, zonePos, root.transform, hmap, clear, ZoneSystem.SpawnMode.Ghost, Spawned });
            }
            finally
            {
                zs.m_vegetation = all;
                placed = Spawned.Count;
                foreach (var go in Spawned) if (go != null) UnityEngine.Object.Destroy(go);   // ghost instances: the ZDOs stay (SpawnZone does the same, ZoneSystem.cs:1346-1352)
                Spawned.Clear();
                if (temp != null) UnityEngine.Object.Destroy(temp);
            }
            if (placed > 0) { _placedZones++; _placedPlants += placed; return RetrofitOutcome.Placed; }
            return RetrofitOutcome.NoSpot;
        }
    }
}
