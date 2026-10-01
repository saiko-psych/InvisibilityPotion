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

        public void Apply(Player p, int tier, bool isLocal)
        {
            if (p == null) return;
            if (_snapshots.TryGetValue(p, out var existing) && existing.Tier == tier) { ApplyCutoff(p, existing, tier, isLocal); return; }
            Remove(p);
            var snap = new Snapshot { Tier = tier };
            _snapshots[p] = snap;
            ApplyCutoff(p, snap, tier, isLocal);
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(FogPrefabName) : null;
            if (prefab != null)
            {
                snap.Fog = Object.Instantiate(prefab, p.transform);
                snap.Fog.transform.localPosition = new Vector3(0f, 1f, 0f);
                var ps = snap.Fog.GetComponentInChildren<ParticleSystem>();
                if (ps != null) { var main = ps.main; main.loop = true; }
            }
        }

        private static void ApplyCutoff(Player p, Snapshot snap, int tier, bool isLocal)
        {
            var g = PluginConfig.Global;
            var cutoff = isLocal && g.ShowSelfFaintly ? g.FogCutoffSelf : (tier == 1 ? g.FogCutoffLight : g.FogCutoffDense);
            foreach (var r in p.GetComponentsInChildren<Renderer>(true))
            {
                // r.materials instantiates per-renderer copies (vanilla players share sharedMaterials).
                // Remove restores the values but does not destroy the instances; acceptable for plan 2, plan 4 revisits with a proper shader.
                foreach (var m in r.materials)
                {
                    if (m == null || !m.HasProperty(CutoffId)) continue;
                    if (!snap.Cutoffs.ContainsKey(m)) snap.Cutoffs[m] = m.GetFloat(CutoffId);
                    m.SetFloat(CutoffId, cutoff);
                }
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
