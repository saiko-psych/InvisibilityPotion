using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;

namespace InvisibilityPotion.Plants
{
    /// <summary>
    /// Huldra's Hair on vanilla Black Forest trees (spec §3.2). This component sits on the tree prefab root (added in
    /// OnVanillaPrefabsAvailable, so every later instantiation by ZNetScene.CreateObject or ZoneSystem.PlaceVegetation carries it,
    /// existing worlds included). In <see cref="Start"/> (never reached by ghost-spawned trees, ZoneSystem.cs:1547-1566) it decides:
    /// IP_LichenForce on the tree ZDO (ip_lichen), then the biome (WorldGenerator.GetBiome, WorldGenerator.cs:746) must be Black
    /// Forest, then <see cref="LichenRoll"/> over (world seed, ZDO position). Only a winning tree instantiates the patch child
    /// (<see cref="PlantPrefabs.LichenTemplate"/>, no ZNetView: it uses the tree's view); every other tree stays exactly vanilla.
    /// Deviation from the spec text (child on every tree, destroyed on a lost roll): the child is created only on a win, which
    /// avoids instantiating three hidden meshes on every fir and keeps the prefab hierarchy untouched.
    /// </summary>
    public sealed class TreeLichen : MonoBehaviour
    {
        /// <summary>Tree prefabs that may carry lichen (S1: verified by the "plants: tree" startup lines; missing names are skipped).</summary>
        public static readonly string[] EligibleTrees = TreeSaplingRule.EligibleTrees;
        /// <summary>ZDO prefab hashes of <see cref="EligibleTrees"/> (ZNetScene.GetPrefabHash = name.GetStableHashCode, ZNetScene.cs:151).</summary>
        public static readonly HashSet<int> EligibleHashes = new HashSet<int>(EligibleTrees.Select(n => n.GetStableHashCode()));
        /// <summary>RPC to the tree's ZDO owner: plant Huldra's Hair here (sent by <see cref="TreeSapling"/>).</summary>
        public const string RpcPlant = "IP_LichenPlant";
        /// <summary>Seconds between the checks for an IP_LichenForce change made by another peer.</summary>
        public const float PollInterval = 5f;
        /// <summary>Dumped at startup for the eligibility decision, never given the component.</summary>
        public static readonly string[] InspectOnly = { "FirTree_small", "FirTree_small_dead", "PineTree_01_dead", "Pine_tree_normal_small" };

        public static readonly int ForceHash = "IP_LichenForce".GetStableHashCode();
        /// <summary>Height of the patch centre above the tree root, metres (world, before the tree's scale).</summary>
        public const float PatchHeight = 1.3f;
        public const float FallbackRadius = 0.35f;
        private const float RayStart = 4f;

        public static readonly HashSet<TreeLichen> All = new HashSet<TreeLichen>();
        private static readonly HashSet<string> Logged = new HashSet<string>();
        private static readonly Dictionary<string, int> PlacementLogs = new Dictionary<string, int>();
        private static bool _loggedWorld;

        private ZNetView _nview;
        private GameObject _lichen;

        public GameObject Lichen => _lichen;
        public bool HasLichen => _lichen != null;
        public ZNetView View => _nview;
        /// <summary>ZDO prefab hash of this tree, 0 without a valid view.</summary>
        public int PrefabHash => _nview != null && _nview.IsValid() ? _nview.GetZDO().GetPrefab() : 0;
        public string Decision { get; private set; } = "pending";
        public double LastRoll { get; private set; } = double.NaN;
        public Heightmap.Biome LastBiome { get; private set; }
        public string PrefabName => name.Replace("(Clone)", "");

        private void Start()
        {
            _nview = GetComponent<ZNetView>();
            All.Add(this);
            try { Evaluate(); }
            catch (Exception e) { LogOnce("eval" + e.GetType().Name, $"lichen: {PrefabName}: evaluation failed: {e}"); }
            if (_nview == null || !_nview.IsValid()) return;
            try { _nview.Register(RpcPlant, RPC_Plant); }
            catch (ArgumentException e) { LogOnce("rpc", $"lichen: {PrefabName}: {RpcPlant} already registered ({e.Message})"); }
            InvokeRepeating(nameof(Poll), UnityEngine.Random.Range(0.5f, PollInterval), PollInterval);
        }

        /// <summary>
        /// Round N ruling 2: a planted sapling forces lichen through the tree's ZDO, written by the tree's owner; every other peer
        /// notices the key here within <see cref="PollInterval"/> s. Cheap: one GetInt, Evaluate only on a mismatch.
        /// </summary>
        private void Poll()
        {
            try
            {
                if (_nview == null || !_nview.IsValid()) return;
                var force = _nview.GetZDO().GetInt(ForceHash, 0);
                if ((force > 0 && _lichen == null) || (force < 0 && _lichen != null)) Evaluate();
            }
            catch (Exception e) { LogOnce("poll" + e.GetType().Name, $"lichen: {PrefabName}: poll failed: {e}"); }
        }

        /// <summary>Asks the tree's ZDO owner to plant Huldra's Hair (InvokeRPC routes to the owner, ZNetView.cs:331).</summary>
        public void RequestPlant()
        {
            if (_nview != null && _nview.IsValid()) _nview.InvokeRPC(RpcPlant);
        }

        /// <summary>Owner side: IP_LichenForce = 1, IP_LichenStage = 1 (S1), IP_LichenTime = now; the patch appears at once here.</summary>
        private void RPC_Plant(long sender)
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;
            var zdo = _nview.GetZDO();
            zdo.Set(ForceHash, 1);
            zdo.Set(VeilHarvest.LichenStageHash, 1);
            zdo.Set(VeilHarvest.LichenTimeHash, ZNet.instance.GetTime().Ticks);
            Evaluate();
            Plugin.Log.LogInfo($"lichen: planted on {PrefabName} at {transform.position:F1} for peer {sender}: force 1, stage 1 (S1), patch {(_lichen != null ? "created" : "missing")}");
        }

        private void OnDestroy() => All.Remove(this);

        /// <summary>Applies the decision: creates or removes the patch child.</summary>
        public void Evaluate()
        {
            var want = Decide();
            if (want && _lichen == null) Spawn();
            else if (!want && _lichen != null)
            {
                Destroy(_lichen);
                _lichen = null;
            }
        }

        private bool Decide()
        {
            if (_nview == null || !_nview.IsValid()) { Decision = "no valid tree view"; return false; }
            if (PlantPrefabs.LichenTemplate == null) { Decision = "no lichen template (bundle?)"; return false; }
            var zdo = _nview.GetZDO();
            var force = zdo.GetInt(ForceHash, 0);
            if (force > 0) { Decision = "forced on"; return true; }
            if (force < 0) { Decision = "forced off"; return false; }
            var wg = WorldGenerator.instance;
            if (wg == null)
            {
                Decision = "no WorldGenerator";
                LogOnce("nowg", "lichen: WorldGenerator.instance is null at tree Start; no lichen (fail closed)");
                return false;
            }
            var pos = zdo.GetPosition();
            LastBiome = wg.GetBiome(pos);
            var seed = wg.GetSeed();
            LastRoll = LichenRoll.Value(seed, pos.x, pos.z);
            if (!_loggedWorld)
            {
                _loggedWorld = true;
                // S3: proves seed and biome are available where trees start (client and server).
                Plugin.Log.LogInfo($"lichen: first tree start: world seed {seed}, {PrefabName} at {pos:F1} biome {LastBiome}, " +
                                   $"server {(ZNet.instance != null && ZNet.instance.IsServer())}, roll {LastRoll:F4} vs chance {Config.PluginConfig.LichenTreeChance}");
            }
            if (LastBiome != Heightmap.Biome.BlackForest) { Decision = $"biome {LastBiome}"; return false; }
            var chance = Config.PluginConfig.LichenTreeChance;
            var has = LastRoll < chance;
            Decision = has ? $"roll {LastRoll:F4} < {chance}" : $"roll {LastRoll:F4} >= {chance}";
            return has;
        }

        private void Spawn()
        {
            _lichen = Instantiate(PlantPrefabs.LichenTemplate, transform, false);
            _lichen.name = PlantPrefabs.LichenName;
            Place(_lichen.transform);
        }

        /// <summary>
        /// Puts the patch on the trunk: a horizontal ray at <see cref="PatchHeight"/> from outside toward the trunk axis, at a
        /// per-tree angle (<see cref="LichenRoll.AngleDegrees"/>), against the tree's own colliders (Collider.Raycast, any layer);
        /// the innermost hit is taken (the trunk, not a crown volume). The patch's +Z faces outward. Without a hit: a fixed radius.
        /// The child keeps a world scale of 1 whatever the tree's random scale.
        /// </summary>
        private void Place(Transform patch)
        {
            var root = transform.position;
            var wg = WorldGenerator.instance;
            var angle = LichenRoll.AngleDegrees(wg != null ? wg.GetSeed() : 0, root.x, root.z);
            var outward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            var origin = root + Vector3.up * PatchHeight + outward * RayStart;
            var ray = new Ray(origin, -outward);
            RaycastHit best = default;
            Collider bestCol = null;
            foreach (var c in GetComponentsInChildren<Collider>(true))
            {
                if (c == null || c.transform.IsChildOf(patch)) continue;
                if (c.Raycast(ray, out var hit, RayStart) && (bestCol == null || hit.distance > best.distance))
                {
                    best = hit;
                    bestCol = c;
                }
            }
            Vector3 pos, normal;
            if (bestCol != null)
            {
                normal = Vector3.ProjectOnPlane(best.normal, Vector3.up);
                if (normal.sqrMagnitude < 0.01f) normal = outward;
                normal.Normalize();
                pos = best.point + normal * 0.01f;
            }
            else
            {
                normal = outward;
                pos = root + Vector3.up * PatchHeight + outward * FallbackRadius * transform.lossyScale.x;
            }
            patch.SetPositionAndRotation(pos, Quaternion.LookRotation(normal, Vector3.up));
            var s = transform.lossyScale;
            patch.localScale = new Vector3(1f / Mathf.Max(0.01f, s.x), 1f / Mathf.Max(0.01f, s.y), 1f / Mathf.Max(0.01f, s.z));
            var key = PrefabName;
            PlacementLogs.TryGetValue(key, out var n);
            if (n < 5)
            {
                PlacementLogs[key] = n + 1;
                // S1/S4: proves which collider carries the trunk and how far out the patch sits.
                var radius = Vector3.ProjectOnPlane(pos - root, Vector3.up).magnitude;
                Plugin.Log.LogInfo($"lichen: {key} at {root:F1} ({Decision}): patch at r={radius:F2} m, h={PatchHeight} m, angle {angle:F0}, " +
                                   (bestCol != null
                                       ? $"via {bestCol.GetType().Name} '{bestCol.name}' layer {LayerMask.LayerToName(bestCol.gameObject.layer)} trigger {bestCol.isTrigger}"
                                       : "no collider hit, fallback radius") + $", tree scale {s.x:F2}");
            }
        }

#if DEBUG
        /// <summary>Debug (ip_lichen): writes IP_LichenForce (1 on, -1 off, 0 roll) as the ZDO owner and re-evaluates at once.</summary>
        public void DevForce(int value)
        {
            if (_nview == null || !_nview.IsValid()) return;
            if (!_nview.IsOwner()) _nview.ClaimOwnership();
            _nview.GetZDO().Set(ForceHash, value);
            if (value == 0 && _lichen != null) { Destroy(_lichen); _lichen = null; }
            Evaluate();
        }
#endif

        private static void LogOnce(string key, string message)
        {
            if (Logged.Add(key)) Plugin.Log.LogWarning(message);
        }

        // ---------- registration ----------

        private static bool _dumped;

        /// <summary>
        /// Adds the component to the eligible tree prefabs (idempotent). Called at OnVanillaPrefabsAvailable and again on every
        /// PrefabManager.OnPrefabsRegistered (ZNetScene.Awake, every world load), looking the prefab up in ZNetScene first, so a
        /// world entered after a logout still gets lichen. The hierarchy dump (S1) runs once.
        /// </summary>
        public static void AttachToTrees(string context)
        {
            if (PlantPrefabs.LichenTemplate == null) { Plugin.Log.LogWarning($"lichen ({context}): no template; trees stay vanilla"); return; }
            var attached = new List<string>();
            var added = 0;
            foreach (var name in EligibleTrees)
            {
                var prefab = (ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null) ?? PrefabManager.Instance.GetPrefab(name);
                if (prefab == null) { if (!_dumped) Plugin.Log.LogWarning($"lichen: tree prefab {name} not found; skipped"); continue; }
                if (!_dumped) Dump(prefab, true);
                if (prefab.GetComponent<ZNetView>() == null) { if (!_dumped) Plugin.Log.LogWarning($"lichen: {name} has no ZNetView; skipped"); continue; }
                if (prefab.GetComponent<TreeLichen>() == null) { prefab.AddComponent<TreeLichen>(); added++; }
                attached.Add(name);
            }
            if (!_dumped)
                foreach (var name in InspectOnly)
                {
                    var prefab = PrefabManager.Instance.GetPrefab(name);
                    if (prefab != null) Dump(prefab, false);
                    else Plugin.Log.LogInfo($"plants: tree {name}: not found");
                }
            _dumped = true;
            var check = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("FirTree") : null;
            Plugin.Log.LogInfo($"lichen ({context}): TreeLichen on {attached.Count} tree prefabs ({added} newly added): {string.Join(", ", attached)}; " +
                               $"ZNetScene FirTree has TreeLichen: {(check == null ? "no ZNetScene/FirTree" : (check.GetComponent<TreeLichen>() != null).ToString())}");
        }

        private static void Dump(GameObject prefab, bool eligible)
        {
            var lod = prefab.GetComponent<LODGroup>();
            var lods = lod != null ? lod.GetLODs() : null;
            var cols = prefab.GetComponentsInChildren<Collider>(true).Select(c =>
                $"{c.GetType().Name} '{c.name}' layer {LayerMask.LayerToName(c.gameObject.layer)} trigger {c.isTrigger} bounds {LocalBounds(prefab.transform, c)}");
            var children = prefab.transform.Cast<Transform>().Select(t => $"{t.name}[{t.childCount}]");
            var tb = prefab.GetComponent<TreeBase>();
            Plugin.Log.LogInfo($"plants: tree {prefab.name} ({(eligible ? "eligible" : "inspect only")}): TreeBase {tb != null}" +
                               $"{(tb != null && tb.m_trunk != null ? $" (trunk '{tb.m_trunk.name}')" : "")}, Destructible {prefab.GetComponent<Destructible>() != null}, " +
                               $"LODGroup {(lods == null ? "none" : $"{lods.Length} LODs ({string.Join("/", lods.Select(l => l.renderers?.Length ?? 0))} renderers)")}, " +
                               $"children {string.Join(", ", children)}; colliders: {string.Join("; ", cols)}");
        }

        private static string LocalBounds(Transform root, Collider c)
        {
            switch (c)
            {
                case CapsuleCollider cap: return $"capsule r {cap.radius:F2} h {cap.height:F2} at {root.InverseTransformPoint(cap.transform.TransformPoint(cap.center)):F2} scale {cap.transform.lossyScale:F2}";
                case BoxCollider box: return $"box {box.size:F2} at {root.InverseTransformPoint(box.transform.TransformPoint(box.center)):F2}";
                case SphereCollider sph: return $"sphere r {sph.radius:F2}";
                case MeshCollider mc: return $"mesh {(mc.sharedMesh != null ? mc.sharedMesh.bounds.size.ToString("F2") : "none")} convex {mc.convex}";
                default: return "?";
            }
        }
    }
}
