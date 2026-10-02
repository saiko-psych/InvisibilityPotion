using System;
using System.Collections.Generic;
using System.Linq;
using InvisibilityPotion.Items;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace InvisibilityPotion.Plants
{
    /// <summary>
    /// Builds the plan 5 runtime prefabs from the bundle plant models (spec §3.2-§3.4), after ModelPrefabs.RegisterPlants applied the
    /// size factors to the bundle prefabs' "model" children and before AssetBundles.Unload:
    /// ground plants IP_BaldrsTear_a/b/c (T2) and IP_HelsEmberFern_a/b/c (T3) with a fixed model variant (ip_spawn), plus the mixed
    /// IP_BaldrsTear / IP_HelsEmberFern used for wild spawning (one prefab per plant, so a vegetation group of 3-6 mixes variants:
    /// each instance picks its variant in VeilHarvest.Awake from its position). Root ZNetView (persistent, m_syncInitialScale) +
    /// VeilHarvest + VeilSight, children "picked" and "ripe" (hover collider; T3 an ember light); the Huldra's Hair trunk patch
    /// template (no ZNetView, instantiated by <see cref="TreeLichen"/>); the cultivated Huldra prefabs (<see cref="Cultivation"/>).
    /// Also gates the plain Plant_* look-check props (ip_spawn Plant_T2) with a VeilSight of their tier.
    /// </summary>
    public static class PlantPrefabs
    {
        public const string BaldrName = "IP_BaldrsTear";
        public const string FernName = "IP_HelsEmberFern";
        public const string LichenName = "IP_Lichen";
        public const string HoverLayerName = "item";
        public static readonly string[] Variants = { "a", "b", "c" };
        public static readonly string[] PickEffectSources = { "Pickable_Thistle", "Pickable_Dandelion", "Pickable_Mushroom" };

        /// <summary>Pick sound/particles borrowed from a vanilla pickable's m_pickEffector (Pickable.cs:36).</summary>
        public static EffectList PickEffects { get; private set; }
        /// <summary>The trunk patch (inactive holder parent, active itself): Instantiate(LichenTemplate, tree) gives a live child.</summary>
        public static GameObject LichenTemplate { get; private set; }
        /// <summary>Every ground plant prefab built here (ip_veg, ip_plants).</summary>
        public static readonly List<GameObject> GroundPrefabs = new List<GameObject>();

        private static GameObject _holder;

        /// <summary>Inactive, persistent parent: objects built under it never run Awake until instantiated elsewhere.</summary>
        public static Transform Holder
        {
            get
            {
                if (_holder == null)
                {
                    _holder = new GameObject("IP_PlantPrefabs");
                    _holder.SetActive(false);
                    UnityEngine.Object.DontDestroyOnLoad(_holder);
                }
                return _holder.transform;
            }
        }

        public static string NameToken(int tier) => tier == 1 ? "$ip_plant_huldrashair" : tier == 2 ? "$ip_plant_baldrstear" : "$ip_plant_helsemberfern";

        /// <summary>Call from ModelItems.Register after ModelPrefabs.RegisterPlants (size factors applied) and before AssetBundles.Unload.</summary>
        public static void Register()
        {
            if (!AssetBundles.Loaded) { Plugin.Log.LogWarning("plants: no bundle; no hidden plants, no lichen, no cultivation"); return; }
            Step("pick effects", ResolvePickEffects);
            Step("lichen template", BuildLichenTemplate);
            foreach (var tier in new[] { 2, 3 })
            {
                foreach (var v in Variants)
                    Step($"ground plant T{tier}{v}", () => PrefabManager.Instance.AddPrefab(new CustomPrefab(BuildGround(tier, v), true)));
                Step($"ground plant T{tier} mixed", () => PlantVegetation.Add(BuildGround(tier, null), tier));
            }
            Step("gate Plant_* props", GateProps);
            Step("trees", () => TreeLichen.AttachToTrees("OnVanillaPrefabsAvailable"));
            PrefabManager.OnPrefabsRegistered += () => Step("trees", () => TreeLichen.AttachToTrees("OnPrefabsRegistered"));
            Step("cultivation", Cultivation.Register);
        }

        private static void Step(string what, Action a)
        {
            try { a(); }
            catch (Exception e) { Plugin.Log.LogError($"plants: {what} failed: {e}"); }
        }

        private static void ResolvePickEffects()
        {
            foreach (var n in PickEffectSources)
            {
                var pick = PrefabManager.Instance.GetPrefab(n)?.GetComponent<Pickable>();
                if (pick == null || pick.m_pickEffector == null) continue;
                PickEffects = pick.m_pickEffector;
                Plugin.Log.LogInfo($"plants: pick effects from {n} ({pick.m_pickEffector.m_effectPrefabs?.Length ?? 0} prefabs: " +
                                   $"{string.Join(", ", (pick.m_pickEffector.m_effectPrefabs ?? new EffectList.EffectData[0]).Where(e => e?.m_prefab != null).Select(e => e.m_prefab.name))})");
                return;
            }
            Plugin.Log.LogWarning($"plants: none of {string.Join(", ", PickEffectSources)} found with a Pickable; picks are silent");
        }

        /// <summary>A child named <paramref name="name"/> under <paramref name="parent"/> holding copies of the bundle prefab's children (model, anchors); no root components.</summary>
        public static GameObject StageFrom(string bundlePrefab, Transform parent, string name)
        {
            var src = AssetBundles.Prefab(bundlePrefab) ?? throw new InvalidOperationException($"bundle prefab {bundlePrefab} missing");
            var stage = new GameObject(name);
            stage.layer = src.layer;
            stage.transform.SetParent(parent, false);
            foreach (Transform child in src.transform)
            {
                var copy = UnityEngine.Object.Instantiate(child.gameObject, stage.transform, false);
                copy.name = child.name;
            }
            return stage;
        }

        /// <summary>
        /// Rotates a flat patch stage so its thinnest axis is the wanted one (+Z for a trunk patch, +Y for the ground); the Blender
        /// script builds the patch facing -Y (Unity +Z after the FBX export, make_plants.py:114). Logs the decision (asset fact).
        /// </summary>
        public static void Orient(GameObject stage, bool vertical, string context)
        {
            var b = ModelPrefabs.LocalMeshBounds(stage);
            var s = b.size;
            var thinIsZ = s.z <= s.y && s.z <= s.x;
            var thinIsY = s.y < s.z && s.y <= s.x;
            var rot = Quaternion.identity;
            if (vertical && thinIsY) rot = Quaternion.Euler(90f, 0f, 0f);         // +Y -> +Z
            else if (!vertical && thinIsZ) rot = Quaternion.Euler(-90f, 0f, 0f);  // +Z -> +Y
            stage.transform.localRotation = rot * stage.transform.localRotation;
            Plugin.Log.LogInfo($"plants: {context}: mesh bounds {s:F3} (thin axis {(thinIsZ ? "z" : thinIsY ? "y" : "x")}), want {(vertical ? "vertical (+Z out)" : "flat (+Y up)")}, " +
                               $"rotation {(rot == Quaternion.identity ? "none" : rot.eulerAngles.ToString("F0"))}");
        }

        /// <summary>Box collider on its own child "hover" (layer item: in Player.m_interactMask, Player.cs:671, but not in the camera, placement or attack masks).</summary>
        public static BoxCollider AddHoverBox(GameObject parent, Vector3 minSize, string layer = HoverLayerName)
        {
            var b = ModelPrefabs.LocalMeshBounds(parent);
            var go = new GameObject("hover");
            go.transform.SetParent(parent.transform, false);
            go.layer = LayerMask.NameToLayer(layer);
            var box = go.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = Vector3.Max(b.size, minSize);
            return box;
        }

        private static GameObject NewRoot(string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(Holder, false);
            return root;
        }

        // ---------- ground plants (T2, T3) ----------

        /// <summary>Ground plant prefab; <paramref name="v"/> null = mixed (all three variants under "ripe", one kept per instance).</summary>
        private static GameObject BuildGround(int tier, string v)
        {
            var baseName = tier == 2 ? BaldrName : FernName;
            var name = v == null ? baseName : $"{baseName}_{v}";
            var root = NewRoot(name);
            var picked = StageFrom($"Plant_T{tier}_picked", root.transform, "picked");
            GameObject ripe;
            var variants = new List<GameObject>();
            if (v != null)
            {
                ripe = StageFrom($"Plant_T{tier}_{v}", root.transform, "ripe");
                AddHoverBox(ripe, new Vector3(0.3f, 0.3f, 0.3f));
                if (tier == 3) AddEmberLight(ripe);
            }
            else
            {
                ripe = new GameObject("ripe");
                ripe.transform.SetParent(root.transform, false);
                foreach (var each in Variants)
                {
                    var sub = StageFrom($"Plant_T{tier}_{each}", ripe.transform, each);
                    AddHoverBox(sub, new Vector3(0.3f, 0.3f, 0.3f));
                    if (tier == 3) AddEmberLight(sub);
                    variants.Add(sub);
                }
            }
            var nv = root.AddComponent<ZNetView>();
            nv.m_persistent = true;
            nv.m_syncInitialScale = true;
            var sight = root.AddComponent<VeilSight>();
            sight.RequiredLevel = tier;
            sight.Stages = new[] { picked, ripe };
            var harvest = root.AddComponent<VeilHarvest>();
            harvest.Tier = tier;
            harvest.MaxStage = 2;
            harvest.OnTree = false;
            harvest.NameToken = NameToken(tier);
            harvest.Variants = variants.ToArray();
            AssetBundles.Track(root);
            GroundPrefabs.Add(root);
            Plugin.Log.LogInfo($"plants: {name} built (T{tier}, stages picked/ripe, {(v == null ? "variants a/b/c mixed by position" : "variant " + v)}, " +
                               $"hover layer {HoverLayerName}{(tier == 3 ? ", ember light" : "")}, size {PlantYield.ScaleRanges[tier].min}-{PlantYield.ScaleRanges[tier].max})");
            return root;
        }

        private static void AddEmberLight(GameObject ripe)
        {
            var anchor = ripe.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "EmberAnchor") ?? ripe.transform;
            var go = new GameObject("ember_light");
            go.transform.SetParent(anchor, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.42f, 0.08f);
            light.range = 1.6f;
            light.intensity = 0.9f;
            light.shadows = LightShadows.None;
        }

        // ---------- Huldra's Hair on trunks ----------

        private static void BuildLichenTemplate()
        {
            var root = NewRoot(LichenName);
            var stages = new GameObject[3];
            for (var s = 1; s <= 3; s++)
            {
                stages[s - 1] = StageFrom($"Plant_T1_S{s}", root.transform, $"S{s}");
                Orient(stages[s - 1], true, $"lichen S{s}");
            }
            var box = AddHoverBox(root, new Vector3(0.25f, 0.25f, 0.15f));
            var sight = root.AddComponent<VeilSight>();
            sight.RequiredLevel = 1;
            sight.UseLichenDistance = true;
            sight.Stages = stages;
            var harvest = root.AddComponent<VeilHarvest>();
            harvest.Tier = 1;
            harvest.MaxStage = 3;
            harvest.OnTree = true;
            harvest.DropOutward = true;
            harvest.NameToken = NameToken(1);
            AssetBundles.Track(root);
            LichenTemplate = root;
            Plugin.Log.LogInfo($"plants: {LichenName} template built (S1..S3, hover box {box.size:F2} at {box.center:F2})");
        }

        // ---------- ip_spawn look-check props ----------

        /// <summary>The bundle Plant_* props (ModelPrefabs.RegisterPlants) get a VeilSight of their tier, so ip_spawn Plant_T2 is gated too.</summary>
        private static void GateProps()
        {
            var gated = new List<string>();
            foreach (var go in AssetBundles.Prefabs.Where(p => p != null && p.name.StartsWith("Plant_T", StringComparison.Ordinal)).ToList())
            {
                if (go.GetComponent<VeilSight>() != null) continue;
                var tier = go.name.Length > 7 ? go.name[7] - '0' : 1;
                var sight = go.AddComponent<VeilSight>();
                sight.RequiredLevel = Mathf.Clamp(tier, 1, 3);
                gated.Add($"{go.name}({sight.RequiredLevel})");
            }
            Plugin.Log.LogInfo($"plants: {gated.Count} Plant_* props gated: {string.Join(", ", gated)}");
        }
    }
}
