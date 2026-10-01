using System;
using System.Collections.Generic;
using InvisibilityPotion.Config;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace InvisibilityPotion.Visuals
{
    /// <summary>How a veiled player's body is drawn. Switchable at runtime (Debug: ip_veil) to compare looks in-game.</summary>
    public enum BodyVeilMode { Off, Cutoff, Hide, Tint, Ghost, Distortion }

    /// <summary>
    /// Wraps a veiled player in a code-built fog particle system (child of the player, local simulation space) and changes the
    /// body according to <see cref="CurrentMode"/>. Every change is recorded in a per-player snapshot; Remove restores it exactly,
    /// whichever mode applied it.
    /// </summary>
    public sealed class FogVeil : IVeil
    {
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color TintTarget = new Color(0.3f, 0.35f, 0.4f);
        private const float TintAmount = 0.7f;
        // Only the material of this prefab is borrowed (soft mist sprite + particle shader); its emitter is a world-space ground mist.
        private const string FogMaterialSource = "vfx_swamp_mist";
        private const string GhostPrefabName = "Ghost";
        private const string DistortionShaderName = "Custom/Distortion";
        private const string FogObjectName = "ip_fog";

        /// <summary>Mode used for newly applied veils. Initialised from the BodyVeilMode config key; ip_veil overrides it in memory.</summary>
        public static BodyVeilMode CurrentMode = BodyVeilMode.Hide;

        public static bool TryParseMode(string text, out BodyVeilMode mode)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse(text.Trim(), true, out mode) && Enum.IsDefined(typeof(BodyVeilMode), mode)) return true;
            mode = BodyVeilMode.Hide;
            return false;
        }

        private sealed class Snapshot
        {
            public int Tier;
            public bool IsLocal;
            public BodyVeilMode Requested;
            public BodyVeilMode Effective;
            public GameObject Fog;
            public readonly Dictionary<Material, float> Cutoffs = new Dictionary<Material, float>();
            public readonly Dictionary<Material, Color> Colors = new Dictionary<Material, Color>();
            public readonly Dictionary<Renderer, Material[]> SharedMaterials = new Dictionary<Renderer, Material[]>();
            public readonly HashSet<Renderer> Hidden = new HashSet<Renderer>();
        }
        private readonly Dictionary<Player, Snapshot> _snapshots = new Dictionary<Player, Snapshot>();

        private static readonly List<Material> MatBuf = new List<Material>();

        public void Apply(Player p, int tier, bool isLocal)
        {
            if (p == null) return;
            if (_snapshots.TryGetValue(p, out var existing) && existing.Tier == tier && existing.IsLocal == isLocal && existing.Requested == CurrentMode)
            {
                if (existing.Fog == null) existing.Fog = SpawnFog(p, tier);
                ApplyBody(p, existing);   // idempotent: originals are recorded once, values derived from them
                return;
            }
            Remove(p);
            var snap = new Snapshot { Tier = tier, IsLocal = isLocal, Requested = CurrentMode, Effective = Resolve(CurrentMode, isLocal) };
            _snapshots[p] = snap;
            snap.Fog = SpawnFog(p, tier);
            ApplyBody(p, snap);
            Plugin.Log.LogDebug($"veil T{tier} on {p.GetPlayerName()}: mode {snap.Requested} (effective {snap.Effective}), fog {(snap.Fog != null ? "on" : "off")}, " +
                                $"{snap.Hidden.Count} hidden, {snap.SharedMaterials.Count} swapped, {snap.Colors.Count} tinted, {snap.Cutoffs.Count} cut");
        }

        /// <summary>Maps the requested mode to the one actually applied (self view and missing materials fall back to Tint).</summary>
        private static BodyVeilMode Resolve(BodyVeilMode mode, bool isLocal)
        {
            switch (mode)
            {
                case BodyVeilMode.Hide:
                    return isLocal && PluginConfig.Global.ShowSelfFaintly ? BodyVeilMode.Tint : BodyVeilMode.Hide;
                case BodyVeilMode.Ghost:
                    if (GhostMaterial() != null) return BodyVeilMode.Ghost;
                    Plugin.Log.LogWarning($"veil mode Ghost: no material found on prefab '{GhostPrefabName}'; using Tint");
                    return BodyVeilMode.Tint;
                case BodyVeilMode.Distortion:
                    if (DistortionMaterial() != null) return BodyVeilMode.Distortion;
                    Plugin.Log.LogWarning($"veil mode Distortion: shader '{DistortionShaderName}' not found; using Tint");
                    return BodyVeilMode.Tint;
                default:
                    return mode;
            }
        }

        private static void ApplyBody(Player p, Snapshot snap)
        {
            var g = PluginConfig.Global;
            var cutoff = snap.IsLocal && g.ShowSelfFaintly ? g.FogCutoffSelf : (snap.Tier == 1 ? g.FogCutoffLight : g.FogCutoffDense);
            var swap = snap.Effective == BodyVeilMode.Ghost ? GhostMaterial()
                     : snap.Effective == BodyVeilMode.Distortion ? DistortionMaterial() : null;

            foreach (var r in p.GetComponentsInChildren<Renderer>(true))
            {
                // Skips the current fog and a previous one whose deferred Destroy has not run yet (ForceRefresh re-applies in the same frame).
                if (r == null || r.gameObject.name == FogObjectName) continue;
                switch (snap.Effective)
                {
                    case BodyVeilMode.Cutoff:
                        // r.GetMaterials instantiates per-renderer copies (vanilla players share sharedMaterials). The values are restored,
                        // the instances are not destroyed; acceptable for plan 2, plan 4 revisits with a proper shader.
                        r.GetMaterials(MatBuf);
                        foreach (var m in MatBuf)
                        {
                            if (m == null || !m.HasProperty(CutoffId)) continue;
                            if (!snap.Cutoffs.ContainsKey(m)) snap.Cutoffs[m] = m.GetFloat(CutoffId);
                            m.SetFloat(CutoffId, cutoff);
                        }
                        break;
                    case BodyVeilMode.Hide:
                        if (r.enabled) { snap.Hidden.Add(r); r.enabled = false; }
                        break;
                    case BodyVeilMode.Tint:
                        if (!IsBodyRenderer(r)) break;
                        r.GetMaterials(MatBuf);
                        foreach (var m in MatBuf)
                        {
                            if (m == null || !m.HasProperty(ColorId)) continue;
                            if (!snap.Colors.TryGetValue(m, out var original)) snap.Colors[m] = original = m.GetColor(ColorId);
                            var c = Color.Lerp(original, TintTarget, TintAmount);
                            c.a = original.a;
                            m.SetColor(ColorId, c);
                        }
                        break;
                    case BodyVeilMode.Ghost:
                    case BodyVeilMode.Distortion:
                        if (swap == null || !IsBodyRenderer(r) || snap.SharedMaterials.ContainsKey(r)) break;
                        var originals = r.sharedMaterials;
                        snap.SharedMaterials[r] = originals;
                        var replaced = new Material[originals.Length];
                        for (var i = 0; i < replaced.Length; i++) replaced[i] = swap;
                        r.sharedMaterials = replaced;
                        break;
                }
            }
            MatBuf.Clear();
        }

        /// <summary>Material swaps and tints only touch meshes; particle and line renderers (equipment effects) keep their shaders.</summary>
        private static bool IsBodyRenderer(Renderer r) => r is SkinnedMeshRenderer || r is MeshRenderer;

        // ---------- fog ----------

        private static bool _fogWarned;

        private static GameObject SpawnFog(Player p, int tier)
        {
            var mat = FogMaterial();
            if (mat == null)
            {
                if (!_fogWarned) { _fogWarned = true; Plugin.Log.LogWarning("veil fog: no particle material available; fog skipped"); }
                return null;
            }
            var g = PluginConfig.Global;
            var dense = tier >= 2;
            var go = new GameObject(FogObjectName);
            go.SetActive(false);   // configure before the system starts playing
            go.transform.SetParent(p.transform, false);
            go.transform.localPosition = new Vector3(0f, 1f, 0f);
            go.transform.localRotation = Quaternion.identity;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;   // particles move with the player
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startSpeed = 0.1f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 1f, 1f, dense ? 0.4f : 0.25f);
            main.gravityModifier = 0f;
            main.maxParticles = 256;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = Mathf.Max(0f, dense ? g.FogRateDense : g.FogRateLight);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = mat;
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.shadowCastingMode = ShadowCastingMode.Off;
            psr.receiveShadows = false;

            go.SetActive(true);
            ps.Play();
            return go;
        }

        private static Material _fogMaterial;

        /// <summary>Borrowed vanilla soft-particle material; falls back to Particles/Standard Unlit with a generated soft sprite.</summary>
        private static Material FogMaterial()
        {
            if (_fogMaterial != null) return _fogMaterial;
            var scene = ZNetScene.instance;
            var prefab = scene != null ? scene.GetPrefab(FogMaterialSource) : null;
            if (prefab != null)
            {
                foreach (var psr in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    if (psr.sharedMaterial == null) continue;
                    _fogMaterial = psr.sharedMaterial;
                    Plugin.Log.LogInfo($"veil fog: borrowed material '{_fogMaterial.name}' (shader '{_fogMaterial.shader.name}') from {FogMaterialSource}");
                    return _fogMaterial;
                }
            }
            if (scene == null) return null;   // too early; retry once the scene exists
            var shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) return null;
            var m = new Material(shader) { name = "ip_fog_fallback", mainTexture = SoftSprite(), renderQueue = (int)RenderQueue.Transparent };
            // Fade mode of the Standard Particle shader.
            m.SetFloat("_Mode", 2f);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_ALPHABLEND_ON");
            _fogMaterial = m;
            Plugin.Log.LogWarning($"veil fog: no material on {FogMaterialSource}; using fallback shader 'Particles/Standard Unlit'");
            return _fogMaterial;
        }

        private static Texture2D SoftSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "ip_fog_sprite" };
            var px = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = (x + 0.5f) / size * 2f - 1f;
                var dy = (y + 0.5f) / size * 2f - 1f;
                var a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * size + x] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // ---------- body materials ----------

        private static Material _ghostMaterial;

        private static Material GhostMaterial()
        {
            if (_ghostMaterial != null) return _ghostMaterial;
            var scene = ZNetScene.instance;
            var prefab = scene != null ? scene.GetPrefab(GhostPrefabName) : null;
            if (prefab == null) return null;
            // The Ghost prefab also has a ParticleSystemRenderer; take the body mesh.
            foreach (var r in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.sharedMaterial != null) { _ghostMaterial = r.sharedMaterial; break; }
            if (_ghostMaterial == null)
                foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.sharedMaterial != null) { _ghostMaterial = r.sharedMaterial; break; }
            if (_ghostMaterial != null)
                Plugin.Log.LogInfo($"veil ghost: material '{_ghostMaterial.name}' (shader '{_ghostMaterial.shader.name}')");
            return _ghostMaterial;
        }

        private static Material _distortionMaterial;
        private static bool _distortionSearched;

        /// <summary>Prefers a vanilla material that already uses the shader (configured textures); otherwise a bare material from it.</summary>
        private static Material DistortionMaterial()
        {
            if (_distortionMaterial != null) return _distortionMaterial;
            var shader = Shader.Find(DistortionShaderName);
            if (!_distortionSearched)
            {
                _distortionSearched = true;
                foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (m == null || m.shader == null || m.shader.name != DistortionShaderName) continue;
                    _distortionMaterial = m;
                    Plugin.Log.LogInfo($"veil distortion: borrowed material '{m.name}'");
                    return _distortionMaterial;
                }
            }
            if (shader == null) return null;
            _distortionMaterial = new Material(shader) { name = "ip_distortion" };
            Plugin.Log.LogInfo("veil distortion: no vanilla material uses the shader; created a bare one");
            return _distortionMaterial;
        }

        // ---------- cleanup ----------

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
            foreach (var kv in snap.Colors) if (kv.Key != null) kv.Key.SetColor(ColorId, kv.Value);
            foreach (var kv in snap.SharedMaterials) if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
            foreach (var r in snap.Hidden) if (r != null) r.enabled = true;
            if (snap.Fog != null)
            {
                snap.Fog.SetActive(false);   // Destroy is deferred to the end of the frame; hide it now
                Object.Destroy(snap.Fog);
            }
            _snapshots.Remove(p);
        }
    }
}
