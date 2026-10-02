using System;
using System.Collections.Generic;
using System.Linq;
using InvisibilityPotion.Items;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace InvisibilityPotion.Plants
{
    /// <summary>
    /// Cultivator pieces (spec §3.4, round N rulings 2 and 3). IP_HuldraSapling is planted on a fir/pine trunk and turns into lichen
    /// on that tree (<see cref="TreeSapling"/>); IP_BaldrSapling / IP_FernSapling (<see cref="Crops"/>) are vanilla-style crop
    /// saplings on cultivated ground in their own biome that grow into the wild ground plants. All are clones of a vanilla crop
    /// sapling and carry a VeilSight of their tier. IP_HuldraGround_a/b/c (the former flat ground lichen) stay registered so
    /// objects in older worlds still load, but no sapling grows into them.
    /// </summary>
    public static class Cultivation
    {
        public const string SaplingName = "IP_HuldraSapling";
        public const string GroundPrefix = "IP_HuldraGround_";
        /// <summary>Vanilla crop saplings tried as the clone source, first found wins (S7 logs the choice).</summary>
        public static readonly string[] SaplingSources = { "sapling_carrot", "sapling_turnip", "sapling_onion", "sapling_barley" };
        /// <summary>Size of the flat model per stage relative to the full patch.</summary>
        public static readonly float[] StageSizes = { 0.45f, 0.7f, 1f };

        public static GameObject Sapling { get; private set; }

        public static void Register()
        {
            var grown = new List<GameObject>();
            foreach (var v in PlantPrefabs.Variants)
            {
                try
                {
                    var go = BuildGround(v);
                    PrefabManager.Instance.AddPrefab(new CustomPrefab(go, true));
                    grown.Add(go);
                }
                catch (Exception e) { Plugin.Log.LogError($"cultivation: {GroundPrefix}{v} failed: {e}"); }
            }
            // Round N ruling 2: no ground lichen any more; IP_HuldraGround_* stay registered (old worlds) but nothing grows into them.
            BuildTreeSapling(grown.ToArray());
            foreach (var c in Crops)
            {
                try { BuildCropSapling(c); }
                catch (Exception e) { Plugin.Log.LogError($"cultivation: {c.Name} failed: {e}"); }
            }
        }

        /// <summary>
        /// Round N ruling 3: Baldr's Tear and Hel's Ember Fern on cultivated ground in their own biome. The grown prefab is the wild
        /// mixed one (variant by position, size rolled by Plant.Grow from the wild range, Plant.cs:206-207), so a cultivated plant
        /// behaves exactly like a wild one (VeilHarvest, regrowth, VeilSight level 2/3).
        /// </summary>
        public sealed class Crop
        {
            public string Name;
            public int Tier;
            public string Grown;
            public Heightmap.Biome Biome;
            public string Token;
        }

        public static readonly Crop[] Crops =
        {
            new Crop { Name = "IP_BaldrSapling", Tier = 2, Grown = PlantPrefabs.BaldrName, Biome = Heightmap.Biome.Mountain, Token = "$piece_ip_baldrsapling" },
            new Crop { Name = "IP_FernSapling", Tier = 3, Grown = PlantPrefabs.FernName, Biome = Heightmap.Biome.AshLands, Token = "$piece_ip_fernsapling" },
        };

        /// <summary>Every Plant sapling of this mod (ip_grow, ip_plants).</summary>
        public static bool IsOurSapling(string name) =>
            name.StartsWith(SaplingName, StringComparison.Ordinal) || Crops.Any(c => name.StartsWith(c.Name, StringComparison.Ordinal));

        /// <summary>Clone of the first vanilla crop sapling found (Piece, Plant, Destructible, ZNetView, place effects).</summary>
        private static (GameObject go, string source) CloneSource(string name)
        {
            foreach (var n in SaplingSources)
            {
                var src = PrefabManager.Instance.GetPrefab(n);
                if (src == null || src.GetComponent<Plant>() == null || src.GetComponent<Piece>() == null) continue;
                return (PrefabManager.Instance.CreateClonedPrefab(name, src), n);
            }
            return (null, null);
        }

        /// <summary>Replaces the four Plant visuals with copies of <paramref name="bundlePrefab"/> at the given sizes; returns the removed count.</summary>
        private static int ReplaceVisuals(GameObject go, Plant plant, Func<string, float, GameObject> make, float small, float grown)
        {
            var old = new[] { plant.m_healthy, plant.m_unhealthy, plant.m_healthyGrown, plant.m_unhealthyGrown }
                .Where(o => o != null && o != go).Distinct().ToList();
            var layer = old.Count > 0 ? old[0].layer : go.layer;
            foreach (var o in old) UnityEngine.Object.DestroyImmediate(o);
            plant.m_healthy = make("healthy", small);
            plant.m_unhealthy = make("unhealthy", small);       // separate objects: Plant SetActive's each one
            plant.m_healthyGrown = make("healthyGrown", grown);
            plant.m_unhealthyGrown = make("unhealthyGrown", grown);
            foreach (var stage in new[] { plant.m_healthy, plant.m_unhealthy, plant.m_healthyGrown, plant.m_unhealthyGrown })
                foreach (var t in stage.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            plant.m_unhealthy.SetActive(false);
            plant.m_healthyGrown.SetActive(false);
            plant.m_unhealthyGrown.SetActive(false);
            return old.Count;
        }

        /// <summary>Plant.HaveGrowSpace needs every collider in its mask on the Plant's own GameObject (Plant.cs:389-401).</summary>
        private static string EnsureRootCollider(GameObject go)
        {
            if (go.GetComponentsInChildren<Collider>(true).Length > 0) return "none needed";
            var b = ModelPrefabs.LocalMeshBounds(go);
            var box = go.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = Vector3.Max(b.size, new Vector3(0.3f, 0.1f, 0.3f));
            return $"BoxCollider added on the root (layer {LayerMask.LayerToName(go.layer)}, size {box.size:F2})";
        }

        private static Sprite IngredientIcon(int tier)
        {
            var ingredient = PrefabManager.Instance.GetPrefab(IngredientItems.ItemName(tier)) ?? ItemManager.Instance.GetItem(IngredientItems.ItemName(tier))?.ItemPrefab;
            return ingredient != null ? ingredient.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons?.FirstOrDefault() : null;
        }

        private static void BuildCropSapling(Crop c)
        {
            var grown = PlantPrefabs.GroundPrefabs.FirstOrDefault(g => g != null && g.name == c.Grown);
            if (grown == null) { Plugin.Log.LogError($"cultivation: grown prefab {c.Grown} missing; {c.Name} not registered"); return; }
            var (go, source) = CloneSource(c.Name);
            if (go == null) { Plugin.Log.LogError($"cultivation: none of {string.Join(", ", SaplingSources)} found as a Plant piece; no {c.Name}"); return; }
            var plant = go.GetComponent<Plant>();
            var removed = ReplaceVisuals(go, plant, (n, size) =>
            {
                var stage = PlantPrefabs.StageFrom($"Plant_T{c.Tier}_a", go.transform, n);
                stage.transform.localScale *= size;
                return stage;
            }, 0.35f, 0.6f);
            var fallback = EnsureRootCollider(go);
            var growSeconds = Config.PluginConfig.CultivateMinutes * 60f;
            var (sMin, sMax) = PlantYield.ScaleRanges[c.Tier];
            plant.m_name = c.Token;
            plant.m_growTime = growSeconds;
            plant.m_growTimeMax = growSeconds;
            plant.m_grownPrefabs = new[] { grown };
            plant.m_minScale = sMin;
            plant.m_maxScale = sMax;
            plant.m_growRadius = 0.5f;
            plant.m_needCultivatedGround = true;
            plant.m_destroyIfCantGrow = false;
            plant.m_biome = c.Biome;                 // Plant.UpdateHealth: WrongBiome status (Plant.cs:225-229)
            plant.m_tolerateCold = c.Biome == Heightmap.Biome.Mountain;   // else TooCold in the Mountains (Plant.cs:241)
            plant.m_tolerateHeat = c.Biome == Heightmap.Biome.AshLands;   // else TooHot in the Ashlands (Plant.cs:236)
            var piece = go.GetComponent<Piece>();
            piece.m_cultivatedGroundOnly = true;
            piece.m_groundOnly = true;
            piece.m_onlyInBiome = c.Biome;           // placement refused with $msg_wrongbiome (Player.cs:4085-4088)
            go.AddComponent<VeilSight>().RequiredLevel = c.Tier;
            AssetBundles.Track(go);
            var icon = IngredientIcon(c.Tier);
            var added = PieceManager.Instance.AddPiece(new CustomPiece(go, true, new PieceConfig
            {
                Name = c.Token,
                Description = c.Token + "_description",
                PieceTable = PieceTables.Cultivator,
                Category = "Misc",
                Icon = icon,
                Requirements = new[] { new RequirementConfig(IngredientItems.ItemName(c.Tier), 1, 0, true) },
            }));
            Plugin.Log.LogInfo($"cultivation: {c.Name} cloned from {source} ({removed} vanilla visuals replaced by Plant_T{c.Tier}_a; collider fallback: {fallback}), added {added}, " +
                               $"biome {c.Biome}, grow {growSeconds / 60f:F0} min to {grown.name} (scale {sMin}-{sMax}), cost 1 {IngredientItems.ItemName(c.Tier)}, " +
                               $"VeilSight {c.Tier}, icon {(icon != null ? "ingredient" : "none")}");
        }

        /// <summary>Stage root with the flat lichen of variant <paramref name="v"/>, laid flat on the ground and scaled to the stage size.</summary>
        private static GameObject FlatStage(string v, Transform parent, string name, float size)
        {
            var stage = PlantPrefabs.StageFrom($"Plant_T1_Flat_{v}", parent, name);
            PlantPrefabs.Orient(stage, false, $"{name} flat {v}");
            stage.transform.localScale *= size;
            stage.transform.localPosition += Vector3.up * 0.02f;
            return stage;
        }

        private static GameObject BuildGround(string v)
        {
            var root = new GameObject(GroundPrefix + v);
            root.transform.SetParent(PlantPrefabs.Holder, false);
            var stages = new GameObject[3];
            for (var i = 0; i < 3; i++) stages[i] = FlatStage(v, root.transform, $"S{i + 1}", StageSizes[i]);
            // piece_nonsolid: in the interact mask (hover, pick) and in the attack mask (Player.cs:671, 679), so any tool hit removes
            // the plant through its Destructible, like a vanilla crop; drops nothing.
            PlantPrefabs.AddHoverBox(root, new Vector3(0.4f, 0.15f, 0.4f), "piece_nonsolid");
            var nv = root.AddComponent<ZNetView>();
            nv.m_persistent = true;
            nv.m_syncInitialScale = true;
            var sight = root.AddComponent<VeilSight>();
            sight.RequiredLevel = 1;
            sight.Stages = stages;
            var harvest = root.AddComponent<VeilHarvest>();
            harvest.Tier = 1;
            harvest.MaxStage = 3;
            harvest.OnTree = false;
            harvest.NameToken = PlantPrefabs.NameToken(1);
            var destructible = root.AddComponent<Destructible>();   // Destructible.cs:5-45; effects copied from the sapling source in BuildTreeSapling
            destructible.m_health = 1f;
            destructible.m_minToolTier = 0;
            destructible.m_spawnWhenDestroyed = null;
            AssetBundles.Track(root);
            Plugin.Log.LogInfo($"cultivation: {root.name} built (S1..S3 flat, VeilSight 1, keys IP_PlantStage/IP_PlantTime)");
            return root;
        }

        /// <summary>
        /// Round N ruling 2: IP_HuldraSapling is planted on a fir/pine trunk. A clone of a vanilla crop sapling (Piece, ZNetView,
        /// Destructible, place effects) without its Plant: no ground or cultivated-ground rule (the ray may hit the trunk), a
        /// <see cref="TreeSapling"/> that converts it into lichen on the tree, the flat lichen S1 as the ghost's look.
        /// </summary>
        private static void BuildTreeSapling(GameObject[] grownForDestructible)
        {
            var (go, source) = CloneSource(SaplingName);
            if (go == null) { Plugin.Log.LogError($"cultivation: none of {string.Join(", ", SaplingSources)} found as a Plant piece; no {SaplingName}"); return; }
            var plant = go.GetComponent<Plant>();
            var old = new[] { plant.m_healthy, plant.m_unhealthy, plant.m_healthyGrown, plant.m_unhealthyGrown }
                .Where(o => o != null && o != go).Distinct().ToList();
            var layer = old.Count > 0 ? old[0].layer : go.layer;
            foreach (var o in old) UnityEngine.Object.DestroyImmediate(o);
            UnityEngine.Object.DestroyImmediate(plant);
            var look = FlatStage(PlantPrefabs.Variants[0], go.transform, "look", StageSizes[0]);
            foreach (var t in look.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            var colliders = go.GetComponentsInChildren<Collider>(true);
            var srcDestructible = PrefabManager.Instance.GetPrefab(source)?.GetComponent<Destructible>();
            foreach (var g in grownForDestructible)
            {
                var d = g.GetComponent<Destructible>();
                if (d == null || srcDestructible == null) continue;
                d.m_destroyedEffect = srcDestructible.m_destroyedEffect;
                d.m_hitEffect = srcDestructible.m_hitEffect;
            }

            var piece = go.GetComponent<Piece>();
            piece.m_cultivatedGroundOnly = false;
            piece.m_groundOnly = false;
            piece.m_vegetationGroundOnly = false;
            piece.m_onlyInBiome = Heightmap.Biome.None;
            go.AddComponent<TreeSapling>();
            go.AddComponent<VeilSight>().RequiredLevel = 1;
            AssetBundles.Track(go);

            var icon = IngredientIcon(1);
            var added = PieceManager.Instance.AddPiece(new CustomPiece(go, true, new PieceConfig
            {
                Name = "$piece_ip_huldrasapling",
                Description = "$piece_ip_huldrasapling_description",
                PieceTable = PieceTables.Cultivator,
                Category = "Misc",
                Icon = icon,
                Enabled = Config.PluginConfig.HuldraCultivable,
                Requirements = new[] { new RequirementConfig(IngredientItems.ItemName(1), 1, 0, true) },
            }));
            Sapling = go;
            Plugin.Log.LogInfo($"cultivation: {SaplingName} cloned from {source} (Plant removed, {old.Count} vanilla visuals replaced by the flat lichen; colliders {colliders.Length}: " +
                               $"{string.Join(", ", colliders.Select(c => $"{c.GetType().Name} on {(c.gameObject == go ? "root" : c.name)} layer {LayerMask.LayerToName(c.gameObject.layer)}"))}), " +
                               $"piece table {PieceTables.Cultivator}, added {added}, enabled {Config.PluginConfig.HuldraCultivable}, tree sapling: within {TreeSaplingRule.TrunkRadius} m of " +
                               $"{string.Join("/", TreeLichen.EligibleTrees)} (hashes {string.Join("/", TreeLichen.EligibleHashes)}), icon {(icon != null ? "ingredient" : "none")}");
        }
    }
}
