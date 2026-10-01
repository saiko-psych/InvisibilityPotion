using System.Collections.Generic;
using InvisibilityPotion.Config;
using UnityEngine;

namespace InvisibilityPotion.Visuals
{
    /// <summary>Thins the player's materials via the Custom/Player _Cutoff property and attaches a fog particle. Restores the exact snapshot on Remove.</summary>
    public sealed class FogVeil : IVeil
    {
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        // Picked from Jotunn's prefab list without in-game verification; round B checks the look (browse with ip_prefabs).
        // Alternatives: "vfx_mistlands_mist", "vfx_darkland_groundfog". Replaced by an asset bundle in plan 4.
        private const string FogPrefabName = "vfx_swamp_mist";

        private sealed class Snapshot
        {
            public readonly Dictionary<Material, float> Cutoffs = new Dictionary<Material, float>();
            public GameObject Fog;
            public int Tier;
        }
        private readonly Dictionary<Player, Snapshot> _snapshots = new Dictionary<Player, Snapshot>();

        private static readonly List<Material> MatBuf = new List<Material>();

        public void Apply(Player p, int tier, bool isLocal)
        {
            if (p == null) return;
            if (_snapshots.TryGetValue(p, out var existing) && existing.Tier == tier)
            {
                if (existing.Fog == null) existing.Fog = SpawnFog(p);
                ApplyCutoff(p, existing, tier, isLocal);
                return;
            }
            Remove(p);
            var snap = new Snapshot { Tier = tier };
            _snapshots[p] = snap;
            snap.Fog = SpawnFog(p);
            ApplyCutoff(p, snap, tier, isLocal);
        }

        /// <summary>Purely local effect: the prefab comes from ZNetScene and has a ZNetView, so init is disabled while instantiating (as vanilla does in Player.cs) and leftover sync components are stripped.</summary>
        private static GameObject SpawnFog(Player p)
        {
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(FogPrefabName) : null;
            if (prefab == null) return null;
            GameObject fog;
            ZNetView.m_forceDisableInit = true;
            try { fog = Object.Instantiate(prefab, p.transform); }
            finally { ZNetView.m_forceDisableInit = false; }
            fog.transform.localPosition = new Vector3(0f, 1f, 0f);
            foreach (var c in fog.GetComponentsInChildren<TimedDestruction>(true)) Object.DestroyImmediate(c);
            foreach (var c in fog.GetComponentsInChildren<ZSyncTransform>(true)) Object.DestroyImmediate(c);
            foreach (var c in fog.GetComponentsInChildren<ZNetView>(true)) Object.DestroyImmediate(c);
            foreach (var ps in fog.GetComponentsInChildren<ParticleSystem>(true)) { var main = ps.main; main.loop = true; }
            return fog;
        }

        private static void ApplyCutoff(Player p, Snapshot snap, int tier, bool isLocal)
        {
            var g = PluginConfig.Global;
            var cutoff = isLocal && g.ShowSelfFaintly ? g.FogCutoffSelf : (tier == 1 ? g.FogCutoffLight : g.FogCutoffDense);
            var fogT = snap.Fog != null ? snap.Fog.transform : null;
            foreach (var r in p.GetComponentsInChildren<Renderer>(true))
            {
                if (fogT != null && r.transform.IsChildOf(fogT)) continue;
                // r.materials instantiates per-renderer copies (vanilla players share sharedMaterials).
                // Remove restores the values but does not destroy the instances; acceptable for plan 2, plan 4 revisits with a proper shader.
                r.GetMaterials(MatBuf);
                foreach (var m in MatBuf)
                {
                    if (m == null || !m.HasProperty(CutoffId)) continue;
                    if (!snap.Cutoffs.ContainsKey(m)) snap.Cutoffs[m] = m.GetFloat(CutoffId);
                    m.SetFloat(CutoffId, cutoff);
                }
            }
            MatBuf.Clear();
        }

        /// <summary>Drops snapshots of destroyed players (Unity-null keys); nothing to restore, only the fog may remain.</summary>
        public void PruneDead()
        {
            List<Player> dead = null;
            foreach (var kv in _snapshots)
                if (kv.Key == null) (dead ?? (dead = new List<Player>())).Add(kv.Key);
            if (dead == null) return;
            foreach (var k in dead)
            {
                if (_snapshots.TryGetValue(k, out var snap) && snap.Fog != null) Object.Destroy(snap.Fog);
                _snapshots.Remove(k);
            }
        }

        public void Remove(Player p)
        {
            if (p == null || !_snapshots.TryGetValue(p, out var snap)) return;
            foreach (var kv in snap.Cutoffs) if (kv.Key != null) kv.Key.SetFloat(CutoffId, kv.Value);
            if (snap.Fog != null) Object.Destroy(snap.Fog);
            _snapshots.Remove(p);
        }
    }
}
