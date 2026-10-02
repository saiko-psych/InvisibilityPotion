using System;
using System.Collections.Generic;
using UnityEngine;

namespace InvisibilityPotion.Plants
{
    /// <summary>
    /// Huldra's Hair is planted on a tree, not on the ground (round N ruling 2). The Cultivator piece IP_HuldraSapling carries this
    /// component and no Plant. Placement: the ghost is valid only within <see cref="TreeSaplingRule.TrunkRadius"/> of the trunk of
    /// an eligible fir/pine (ZDO prefab hash) that has no lichen yet; the check runs in the Harmony postfix on
    /// Player.UpdatePlacementGhost (Patches/PlacementPatches.cs), because vanilla Piece.m_mustConnectTo matches a single name by
    /// substring (Player.cs:3851-3877) and has no message of its own. On the placed instance (ZDO owner = the placer, Start runs
    /// after PlacePiece's SetCreator, Player.cs:3092-3102) it asks the tree's owner to force lichen at S1
    /// (<see cref="TreeLichen.RequestPlant"/>), marks itself used and removes itself after the place effect played. A sapling
    /// left from an older version (ground lichen) or one that finds no tree refunds its cost (Piece.DropResources) and goes.
    /// </summary>
    public sealed class TreeSapling : MonoBehaviour
    {
        public static readonly int UsedHash = "IP_SaplingUsed".GetStableHashCode();
        /// <summary>Seconds the placed sapling stays (place sound and particles are parented to it, Player.cs:3126).</summary>
        public const float RemoveDelay = 1.5f;
        public const string NeedsTreeToken = "$ip_sapling_needs_tree";
        public const string TakenToken = "$ip_sapling_tree_taken";

        private static readonly Collider[] Hits = new Collider[64];
        private static readonly List<TreeLichen> Trees = new List<TreeLichen>();
        private static readonly List<TreeSaplingRule.Candidate> Candidates = new List<TreeSaplingRule.Candidate>();
        private static string _pendingMessage;
        private ZNetView _nview;

        private void Start()
        {
            _nview = GetComponent<ZNetView>();
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;   // ghosts have no ZDO (ZNetView.cs:45)
            try
            {
                var zdo = _nview.GetZDO();
                if (zdo.GetInt(UsedHash, 0) == 0)
                {
                    zdo.Set(UsedHash, 1);
                    var verdict = Find(transform.position, out var tree);
                    if (verdict == TreeSaplingRule.Verdict.Ok)
                    {
                        tree.RequestPlant();
                        Plugin.Log.LogInfo($"cultivation: {Cultivation.SaplingName} at {transform.position:F1} -> {tree.PrefabName} at {tree.transform.position:F1}: plant requested");
                    }
                    else
                    {
                        GetComponent<Piece>()?.DropResources();
                        Plugin.Log.LogWarning($"cultivation: {Cultivation.SaplingName} at {transform.position:F1}: {verdict}; cost refunded, sapling removed");
                    }
                }
            }
            catch (Exception e) { Plugin.Log.LogError($"cultivation: tree sapling start failed: {e}"); }
            Invoke(nameof(Remove), RemoveDelay);
        }

        private void Remove()
        {
            if (_nview != null && _nview.IsValid() && _nview.IsOwner() && ZNetScene.instance != null) ZNetScene.instance.Destroy(gameObject);
        }

        /// <summary>
        /// Trees whose colliders overlap a <see cref="TreeSaplingRule.TrunkRadius"/> sphere at <paramref name="pos"/> (triggers
        /// ignored), each with its distance to the nearest of its colliders' bounds, decided by <see cref="TreeSaplingRule.Choose"/>.
        /// </summary>
        public static TreeSaplingRule.Verdict Find(Vector3 pos, out TreeLichen tree)
        {
            tree = null;
            Trees.Clear();
            Candidates.Clear();
            var distances = new Dictionary<TreeLichen, float>();
            var n = Physics.OverlapSphereNonAlloc(pos, TreeSaplingRule.TrunkRadius, Hits, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < n; i++)
            {
                var c = Hits[i];
                if (c == null) continue;
                var t = c.GetComponentInParent<TreeLichen>();
                if (t == null) continue;
                var d = Mathf.Min(TreeSaplingRule.TrunkRadius, Vector3.Distance(pos, c.bounds.ClosestPoint(pos)));
                if (!distances.TryGetValue(t, out var old) || d < old) distances[t] = d;
            }
            foreach (var kv in distances)
            {
                Trees.Add(kv.Key);
                Candidates.Add(new TreeSaplingRule.Candidate(kv.Key.PrefabHash, kv.Value, kv.Key.HasLichen));
            }
            var r = TreeSaplingRule.Choose(Candidates, TreeLichen.EligibleHashes);
            if (r.Index >= 0) tree = Trees[r.Index];
            return r.Verdict;
        }

        /// <summary>Queued by the placement patch when TryPlacePiece is refused; shown in LateUpdate so it replaces $msg_invalidplacement (Player.cs:3050-3052).</summary>
        public static void QueueMessage(TreeSaplingRule.Verdict verdict) =>
            _pendingMessage = verdict == TreeSaplingRule.Verdict.Taken ? TakenToken : NeedsTreeToken;

        /// <summary>Called from VeilSightDriver.LateUpdate.</summary>
        public static void FlushMessage()
        {
            if (_pendingMessage == null) return;
            var msg = _pendingMessage;
            _pendingMessage = null;
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, msg);
        }
    }
}
