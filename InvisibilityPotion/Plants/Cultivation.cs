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
    /// Huldra's Hair on cultivated ground (spec §3.4). The sapling IP_HuldraSapling is a clone of a vanilla crop sapling (Piece with
    /// m_cultivatedGroundOnly, Plant, Destructible, ZNetView, place effects) whose four visual children are replaced by the flat
    /// lichen at S1/S2 size; Plant swaps them with SetActive at 50 % (Plant.cs:152-159) and Grow instantiates one of
    /// IP_HuldraGround_a/b/c and destroys the sapling (Plant.cs:181-213). The grown plant is a VeilHarvest with three stages and the
    /// IP_PlantStage/IP_PlantTime keys: no keys = S3 ripe; after a pick it replays S1 -> S2 -> S3. Both carry VeilSight(1). The
    /// piece goes into the Cultivator table (PieceTables.Cultivator = "_CultivatorPieceTable") and costs 1 VeilIngredient_T1.
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
            if (grown.Count == 0) { Plugin.Log.LogError("cultivation: no grown prefab; sapling not registered"); return; }
            BuildSapling(grown.ToArray());
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
            var destructible = root.AddComponent<Destructible>();   // Destructible.cs:5-45; effects copied from the sapling source in BuildSapling
            destructible.m_health = 1f;
            destructible.m_minToolTier = 0;
            destructible.m_spawnWhenDestroyed = null;
            AssetBundles.Track(root);
            Plugin.Log.LogInfo($"cultivation: {root.name} built (S1..S3 flat, VeilSight 1, keys IP_PlantStage/IP_PlantTime)");
            return root;
        }

        private static void BuildSapling(GameObject[] grown)
        {
            string source = null;
            GameObject go = null;
            foreach (var n in SaplingSources)
            {
                var src = PrefabManager.Instance.GetPrefab(n);
                if (src == null || src.GetComponent<Plant>() == null || src.GetComponent<Piece>() == null) continue;
                Plugin.Log.LogInfo($"cultivation: source {n}: {ModelPrefabs.ComponentList(src)}; children {string.Join(", ", src.transform.Cast<Transform>().Select(t => t.name))}; " +
                                   $"colliders {string.Join(", ", src.GetComponentsInChildren<Collider>(true).Select(c => $"{c.GetType().Name} '{c.name}' layer {LayerMask.LayerToName(c.gameObject.layer)}"))}");
                go = PrefabManager.Instance.CreateClonedPrefab(SaplingName, src);
                source = n;
                break;
            }
            if (go == null) { Plugin.Log.LogError($"cultivation: none of {string.Join(", ", SaplingSources)} found as a Plant piece; no sapling"); return; }

            var plant = go.GetComponent<Plant>();
            // Replace the vanilla visuals (the four Plant children) with the lichen; colliders elsewhere stay for placement and hover.
            var old = new[] { plant.m_healthy, plant.m_unhealthy, plant.m_healthyGrown, plant.m_unhealthyGrown }
                .Where(o => o != null && o != go).Distinct().ToList();
            var layer = old.Count > 0 ? old[0].layer : go.layer;
            foreach (var o in old) UnityEngine.Object.DestroyImmediate(o);
            var v = PlantPrefabs.Variants[0];
            plant.m_healthy = FlatStage(v, go.transform, "healthy", StageSizes[0]);
            plant.m_unhealthy = FlatStage(v, go.transform, "unhealthy", StageSizes[0]);       // separate objects: Plant SetActive's each one
            plant.m_healthyGrown = FlatStage(v, go.transform, "healthyGrown", StageSizes[1]);
            plant.m_unhealthyGrown = FlatStage(v, go.transform, "unhealthyGrown", StageSizes[1]);
            foreach (var stage in new[] { plant.m_healthy, plant.m_unhealthy, plant.m_healthyGrown, plant.m_unhealthyGrown })
                foreach (var t in stage.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            plant.m_unhealthy.SetActive(false);
            plant.m_healthyGrown.SetActive(false);
            plant.m_unhealthyGrown.SetActive(false);
            var remaining = go.GetComponentsInChildren<Collider>(true);
            var fallback = "none needed";
            if (remaining.Length == 0)
            {
                // On the root itself: Plant.HaveGrowSpace takes GetComponent<Plant>() of every collider's own GameObject in its space
                // mask (Plant.cs:389-401); a collider on a child would count as an obstacle and the sapling would never grow.
                var b = ModelPrefabs.LocalMeshBounds(go);
                var box = go.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = Vector3.Max(b.size, new Vector3(0.3f, 0.1f, 0.3f));
                fallback = $"BoxCollider added on the root (layer {LayerMask.LayerToName(go.layer)}, size {box.size:F2})";
            }
            Plugin.Log.LogInfo($"cultivation: sapling visuals replaced ({old.Count} vanilla children removed, layer {LayerMask.LayerToName(layer)}); " +
                               $"colliders kept {remaining.Length} ({string.Join(", ", remaining.Select(c => $"{c.GetType().Name} on {(c.gameObject == go ? "root" : c.name)}"))}); fallback: {fallback}");
            var srcDestructible = PrefabManager.Instance.GetPrefab(source)?.GetComponent<Destructible>();
            foreach (var g in grown)
            {
                var d = g.GetComponent<Destructible>();
                if (d == null || srcDestructible == null) continue;
                d.m_destroyedEffect = srcDestructible.m_destroyedEffect;
                d.m_hitEffect = srcDestructible.m_hitEffect;
            }

            plant.m_name = "$piece_ip_huldrasapling";
            var growSeconds = 2f * Config.PluginConfig.LichenStageMinutes * 60f;
            plant.m_growTime = growSeconds;
            plant.m_growTimeMax = growSeconds;
            plant.m_grownPrefabs = grown;
            plant.m_minScale = 1f;
            plant.m_maxScale = 1f;
            plant.m_growRadius = 0.5f;
            plant.m_needCultivatedGround = true;
            plant.m_destroyIfCantGrow = false;
            plant.m_tolerateCold = true;
            plant.m_tolerateHeat = true;
            plant.m_biome = Heightmap.Biome.All;

            var piece = go.GetComponent<Piece>();
            piece.m_cultivatedGroundOnly = true;
            piece.m_groundOnly = true;
            piece.m_onlyInBiome = Heightmap.Biome.None;

            var sight = go.AddComponent<VeilSight>();
            sight.RequiredLevel = 1;
            AssetBundles.Track(go);

            var ingredient = PrefabManager.Instance.GetPrefab(IngredientItems.ItemName(1)) ?? ItemManager.Instance.GetItem(IngredientItems.ItemName(1))?.ItemPrefab;
            var icon = ingredient != null ? ingredient.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons?.FirstOrDefault() : null;
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
            Plugin.Log.LogInfo($"cultivation: {SaplingName} cloned from {source}, piece table {PieceTables.Cultivator}, added {added}, enabled {Config.PluginConfig.HuldraCultivable}, " +
                               $"grow {growSeconds / 60f:F0} min to {string.Join("/", grown.Select(g => g.name))}, icon {(icon != null ? "ingredient" : "none")}, " +
                               $"cultivated only {piece.m_cultivatedGroundOnly}, needs cultivated {plant.m_needCultivatedGround}");
        }
    }
}
