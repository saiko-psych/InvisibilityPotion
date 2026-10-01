using System;
using System.Collections.Generic;
using InvisibilityPotion.Config;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace InvisibilityPotion.Visuals
{
    /// <summary>How a veiled player's body is drawn. Per tier in the config (TierN.BodyVeilMode); ip_veil overrides it at runtime.</summary>
    public enum BodyVeilMode { Off, Cutoff, Hide, Tint, Ghost, Distortion }

    /// <summary>
    /// Wraps a veiled player in small fog emitters that follow body bones (head, chest, hips, hands, feet) and changes the body
    /// according to the tier's mode. Every change is recorded in a per-player snapshot; Remove restores it exactly,
    /// whichever mode applied it. Look values are static and live-tunable (Debug: ip_fog, ip_veil); any change bumps
    /// <see cref="Version"/>, which makes the next Apply rebuild the veil.
    /// </summary>
    public sealed class FogVeil : IVeil
    {
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int RefractionId = Shader.PropertyToID("_RefractionIntensity");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly Color TintTarget = new Color(0.3f, 0.35f, 0.4f);
        private const float TintAmount = 0.7f;
        // Only the material of this prefab is borrowed (soft mist sprite + Custom/LitParticles); its emitter is a world-space ground mist.
        private const string FogMaterialSource = "vfx_swamp_mist";
        private const string GhostPrefabName = "Ghost";
        private const string DistortionShaderName = "Custom/Distortion";
        // Vanilla Custom/Distortion material with a normal map; its bluish _Color is overridden by DistortionColor.
        private const string PreferredDistortionMaterial = "Aspect_mat";
        private const string FogObjectName = "ip_fog";

        // ---------- live look state ----------

        /// <summary>Current fog look (a copy of PluginConfig.Fog, mutated by ip_fog).</summary>
        public static FogSettings Fog = FogSettings.Defaults();
        public static float DistortionStrength = 0.2f;
        public static Color DistortionColor = new Color(1f, 1f, 1f, 0.15f);
        public static Color GhostColor = new Color(0.75f, 0.8f, 0.9f, 1f);
        public static float GhostEmission = 0.25f;
        /// <summary>Runtime body-mode override per tier (index 1..3), null = use the tier's config. Set by ip_veil.</summary>
        public static readonly BodyVeilMode?[] ModeOverride = new BodyVeilMode?[4];
        /// <summary>Bumped on every look change; a snapshot of another version is rebuilt on the next Apply.</summary>
        public static int Version { get; private set; }

        public static void Bump()
        {
            Version++;
            if (_ghostInstance != null) ApplyGhostLook(_ghostInstance);
            if (_distortionInstance != null) ApplyDistortionLook(_distortionInstance);
        }

        /// <summary>Re-reads all look values from PluginConfig (startup, ip_reload_config, server sync, ip_fog reset).</summary>
        public static void LoadFromConfig()
        {
            var g = PluginConfig.Global;
            Fog = PluginConfig.Fog.Clone();
            DistortionStrength = g.DistortionStrength;
            DistortionColor = ParseColor(g.DistortionColor, 4, new Color(1f, 1f, 1f, 0.15f), "DistortionColor");
            GhostColor = ParseColor(g.GhostColor, 4, new Color(0.75f, 0.8f, 0.9f, 1f), "GhostColor");
            GhostEmission = Mathf.Max(0f, g.GhostEmission);
            ParseConfiguredModes();
            Bump();
        }

        private static Color ParseColor(string text, int count, Color fallback, string key)
        {
            if (FloatList.TryParse(text, count, out var v)) return new Color(v[0], v[1], v[2], count > 3 ? v[3] : 1f);
            Plugin.Log.LogWarning($"[Veil] {key} '{text}' is not {(count > 3 ? "r,g,b,a" : "r,g,b")}; using default");
            return fallback;
        }

        public static string FormatColor(Color c) => FloatList.Format(c.r, c.g, c.b, c.a);

        public static bool TryParseMode(string text, out BodyVeilMode mode)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse(text.Trim(), true, out mode) && Enum.IsDefined(typeof(BodyVeilMode), mode)) return true;
            mode = BodyVeilMode.Off;
            return false;
        }

        private static readonly BodyVeilMode[] _configuredModes = { BodyVeilMode.Off, BodyVeilMode.Off, BodyVeilMode.Ghost, BodyVeilMode.Distortion };

        /// <summary>The tier's configured body mode, parsed once per LoadFromConfig.</summary>
        public static BodyVeilMode ConfiguredMode(int tier) => tier >= 1 && tier <= 3 ? _configuredModes[tier] : BodyVeilMode.Off;

        /// <summary>Parses TierN.BodyVeilMode; a malformed value falls back to the tier's default with a warning.</summary>
        private static void ParseConfiguredModes()
        {
            for (var t = 1; t <= 3; t++)
            {
                var text = PluginConfig.Tier(t).BodyVeilMode;
                if (TryParseMode(text, out var mode)) { _configuredModes[t] = mode; continue; }
                var fallback = PluginConfig.DefaultBodyVeilMode(t);
                TryParseMode(fallback, out mode);
                Plugin.Log.LogWarning($"[Tier{t}] BodyVeilMode '{text}' is not one of Off, Cutoff, Hide, Tint, Ghost, Distortion; using the default {mode}");
                _configuredModes[t] = mode;
            }
        }

        public static BodyVeilMode ModeFor(int tier) => tier >= 1 && tier <= 3 ? ModeOverride[tier] ?? ConfiguredMode(tier) : BodyVeilMode.Off;

        // ---------- snapshots ----------

        private sealed class Snapshot
        {
            public int Tier;
            public bool IsLocal;
            public bool ForceHide;
            public int Version;
            public BodyVeilMode Requested;
            public BodyVeilMode Effective;
            public bool FogWanted;
            public readonly List<GameObject> Fog = new List<GameObject>();
            public Material FogMaterial;   // per-veil instance, destroyed on Remove
            public readonly Dictionary<Material, float> Cutoffs = new Dictionary<Material, float>();
            public readonly Dictionary<Material, Color> Colors = new Dictionary<Material, Color>();
            public readonly Dictionary<Renderer, Material[]> SharedMaterials = new Dictionary<Renderer, Material[]>();
            public readonly HashSet<Renderer> Hidden = new HashSet<Renderer>();

            public bool FogIntact()
            {
                if (!FogWanted) return true;
                if (Fog.Count == 0) return false;
                foreach (var go in Fog) if (go == null) return false;
                return true;
            }
        }
        private readonly Dictionary<Player, Snapshot> _snapshots = new Dictionary<Player, Snapshot>();

        private static readonly List<Material> MatBuf = new List<Material>();

        /// <param name="forceHide">Remote player hidden from players (tier III): draw nothing, no fog, whatever the tier's look.</param>
        public void Apply(Player p, int tier, bool isLocal, bool forceHide)
        {
            if (p == null) return;
            var requested = forceHide ? BodyVeilMode.Hide : ModeFor(tier);
            if (_snapshots.TryGetValue(p, out var existing) && existing.Tier == tier && existing.IsLocal == isLocal && existing.ForceHide == forceHide
                && existing.Version == Version && existing.Requested == requested && existing.FogIntact())
            {
                ApplyBody(p, existing);   // idempotent: originals are recorded once, values derived from them
                return;
            }
            Remove(p);
            var cfg = PluginConfig.Tier(tier);
            var snap = new Snapshot
            {
                Tier = tier, IsLocal = isLocal, ForceHide = forceHide, Version = Version,
                Requested = requested, Effective = Resolve(requested, isLocal),
                FogWanted = !forceHide && cfg.FogEnabled && cfg.FogDensity > 0f,
            };
            _snapshots[p] = snap;
            if (snap.FogWanted) SpawnFog(p, snap, cfg.FogDensity);
            if (snap.Fog.Count == 0) snap.FogWanted = false;   // nothing spawned (no material/bones): do not retry every tick
            ApplyBody(p, snap);
            Plugin.Log.LogDebug($"veil T{tier} on {p.GetPlayerName()}: mode {snap.Requested} (effective {snap.Effective}){(forceHide ? " [hidden from players]" : "")}, " +
                                $"fog {snap.Fog.Count} emitters, {snap.Hidden.Count} hidden, {snap.SharedMaterials.Count} swapped, {snap.Colors.Count} tinted, {snap.Cutoffs.Count} cut");
        }

        /// <summary>Maps the requested mode to the one actually applied (self view and missing materials fall back to Tint).</summary>
        private static BodyVeilMode Resolve(BodyVeilMode mode, bool isLocal)
        {
            switch (mode)
            {
                case BodyVeilMode.Hide:
                    return isLocal && PluginConfig.Global.ShowSelfFaintly ? BodyVeilMode.Tint : BodyVeilMode.Hide;
                case BodyVeilMode.Ghost:
                    if (GhostInstance() != null) return BodyVeilMode.Ghost;
                    Plugin.Log.LogWarning($"veil mode Ghost: no material found on prefab '{GhostPrefabName}'; using Tint");
                    return BodyVeilMode.Tint;
                case BodyVeilMode.Distortion:
                    if (DistortionInstance() != null) return BodyVeilMode.Distortion;
                    Plugin.Log.LogWarning($"veil mode Distortion: shader '{DistortionShaderName}' not found; using Tint");
                    return BodyVeilMode.Tint;
                default:
                    return mode;
            }
        }

        private static void ApplyBody(Player p, Snapshot snap)
        {
            if (snap.Effective == BodyVeilMode.Off) return;
            var g = PluginConfig.Global;
            var cutoff = snap.IsLocal && g.ShowSelfFaintly ? g.FogCutoffSelf : (snap.Tier == 1 ? g.FogCutoffLight : g.FogCutoffDense);
            var swap = snap.Effective == BodyVeilMode.Ghost ? GhostInstance()
                     : snap.Effective == BodyVeilMode.Distortion ? DistortionInstance() : null;

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
        private static bool _noAnimatorWarned;
        private static readonly HashSet<string> _missingBoneWarned = new HashSet<string>();

        // Valheim's player rig names bones like Mixamo without prefix (Hips, Spine, Spine1, Spine2, Head, LeftHand ...);
        // Utils.GetBoneTransform searches the animator hierarchy for the HumanBodyBones name, these are the fallbacks.
        private static readonly Dictionary<string, string[]> BoneFallbacks = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Chest"] = new[] { "Spine2", "Spine1", "Spine", "UpperChest" },
            ["Hips"] = new[] { "Pelvis" },
        };

        private static Transform FindBone(Animator animator, string anchorName)
        {
            if (Enum.TryParse(anchorName, true, out HumanBodyBones bone) && Enum.IsDefined(typeof(HumanBodyBones), bone))
            {
                var t = Utils.GetBoneTransform(animator, bone);
                if (t != null) return t;
            }
            if (BoneFallbacks.TryGetValue(anchorName, out var names))
                foreach (var n in names)
                {
                    var t = Utils.FindChild(animator.transform, n);
                    if (t != null) return t;
                }
            return null;
        }

        private static void SpawnFog(Player p, Snapshot snap, float density)
        {
            var source = FogSourceMaterial();
            if (source == null)
            {
                if (!_fogWarned) { _fogWarned = true; Plugin.Log.LogWarning("veil fog: no particle material available; fog skipped"); }
                return;
            }
            var s = Fog;
            var alpha = s.EffectiveAlpha(density);
            var mat = new Material(source) { name = "ip_fog_mat" };   // never mutate the vanilla shared material
            var materialHasColor = mat.HasProperty(ColorId);
            float matAlpha, vertexAlpha;
            if (!materialHasColor || s.AlphaMode == FogAlphaMode.Vertex) { matAlpha = 1f; vertexAlpha = alpha; }
            else if (s.AlphaMode == FogAlphaMode.Material) { matAlpha = alpha; vertexAlpha = 1f; }
            else { matAlpha = vertexAlpha = Mathf.Sqrt(alpha); }   // Both: the product of the two is the target alpha
            if (materialHasColor) mat.SetColor(ColorId, new Color(s.R, s.G, s.B, matAlpha));
            if (mat.HasProperty(EmissionColorId)) mat.SetColor(EmissionColorId, Color.black);
            snap.FogMaterial = mat;
            var startColor = materialHasColor ? new Color(1f, 1f, 1f, vertexAlpha) : new Color(s.R, s.G, s.B, vertexAlpha);

            var animator = p.m_animator;
            if (animator == null && !_noAnimatorWarned)
            {
                _noAnimatorWarned = true;
                Plugin.Log.LogWarning("veil fog: player has no animator; using one emitter at the player root");
            }
            var anyEnabled = false;
            foreach (var a in s.Anchors)
            {
                if (!a.Enabled) continue;
                anyEnabled = true;
                if (animator == null) break;
                var bone = FindBone(animator, a.Name);
                if (bone == null)
                {
                    if (_missingBoneWarned.Add(a.Name)) Plugin.Log.LogWarning($"veil fog: bone for anchor '{a.Name}' not found; anchor skipped");
                    continue;
                }
                snap.Fog.Add(SpawnEmitter(p.transform, bone, a.Radius, new Vector3(a.X, a.Y, a.Z), density, startColor, mat));
            }
            // No animator at all: one emitter at chest height so the fog does not silently disappear.
            if (animator == null && anyEnabled)
                snap.Fog.Add(SpawnEmitter(p.transform, p.transform, 0.4f, new Vector3(0f, 1f, 0f), density, startColor, mat));
        }

        private static GameObject SpawnEmitter(Transform root, Transform bone, float radius, Vector3 offset, float density, Color startColor, Material mat)
        {
            var s = Fog;
            var go = new GameObject(FogObjectName);
            go.SetActive(false);   // configure before the system starts playing
            go.transform.SetParent(root, false);   // destroyed with the player; position driven by the follower
            var follower = go.AddComponent<FogAnchorFollower>();
            follower.Bone = bone;
            follower.Root = root;
            follower.Offset = offset;
            follower.Snap();

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;   // particles stay with the bone
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(s.Lifetime * 0.8f, s.Lifetime * 1.2f);
            main.startSpeed = s.Speed;
            main.startSize = new ParticleSystem.MinMaxCurve(s.Size * 0.6f, s.Size * 1.25f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = startColor;
            main.gravityModifier = 0f;
            main.maxParticles = 64;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = Mathf.Max(0f, s.Rate * density);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(0.001f, radius);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;   // drift is "up" regardless of bone rotation
            vel.x = new ParticleSystem.MinMaxCurve(0f);
            vel.y = new ParticleSystem.MinMaxCurve(s.Drift);
            vel.z = new ParticleSystem.MinMaxCurve(0f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = mat;
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.shadowCastingMode = ShadowCastingMode.Off;
            psr.receiveShadows = false;
            psr.lightProbeUsage = LightProbeUsage.Off;

            go.SetActive(true);
            ps.Play();
            return go;
        }

        private static Material _fogMaterial;

        /// <summary>Borrowed vanilla soft-particle material (shared, never mutated); falls back to Particles/Standard Unlit with a generated soft sprite.</summary>
        private static Material FogSourceMaterial()
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
                    LogProperties("fog", _fogMaterial);
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

        /// <summary>Logs every shader property of a material once (name, type, current value) so the user can report what exists.</summary>
        private static void LogProperties(string label, Material m)
        {
            var shader = m.shader;
            if (shader == null) return;
            var parts = new List<string>();
            for (var i = 0; i < shader.GetPropertyCount(); i++)
            {
                var name = shader.GetPropertyName(i);
                var type = shader.GetPropertyType(i);
                string value;
                switch (type)
                {
                    case ShaderPropertyType.Color: value = m.GetColor(name).ToString(); break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range: value = m.GetFloat(name).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); break;
                    case ShaderPropertyType.Texture: var t = m.GetTexture(name); value = t != null ? t.name : "-"; break;
                    default: value = type.ToString(); break;
                }
                parts.Add($"{name}={value}");
            }
            Plugin.Log.LogInfo($"veil {label}: material '{m.name}' shader '{shader.name}' queue {m.renderQueue} properties: {string.Join(", ", parts)}");
        }

        private static Material _ghostSource;
        private static Material _ghostInstance;
        private static Color _ghostSourceEmission = Color.black;

        /// <summary>Copy of the vanilla Ghost_mat (Custom/Creature) with GhostColor and a dimmed glow; created once, re-tuned on Bump.</summary>
        private static Material GhostInstance()
        {
            if (_ghostInstance != null) return _ghostInstance;
            if (_ghostSource == null)
            {
                var scene = ZNetScene.instance;
                var prefab = scene != null ? scene.GetPrefab(GhostPrefabName) : null;
                if (prefab == null) return null;
                // The Ghost prefab also has a ParticleSystemRenderer; take the body mesh.
                foreach (var r in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (r.sharedMaterial != null) { _ghostSource = r.sharedMaterial; break; }
                if (_ghostSource == null)
                    foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true))
                        if (r.sharedMaterial != null) { _ghostSource = r.sharedMaterial; break; }
                if (_ghostSource == null) return null;
                LogProperties("ghost", _ghostSource);
            }
            if (_ghostSource.HasProperty(EmissionColorId)) _ghostSourceEmission = _ghostSource.GetColor(EmissionColorId);
            _ghostInstance = new Material(_ghostSource) { name = "ip_ghost" };
            ApplyGhostLook(_ghostInstance);
            return _ghostInstance;
        }

        private static void ApplyGhostLook(Material m)
        {
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, GhostColor);
            if (m.HasProperty(EmissionColorId)) m.SetColor(EmissionColorId, _ghostSourceEmission * GhostEmission);
        }

        private static Material _distortionInstance;
        private static bool _distortionSearched;

        /// <summary>Copy of a vanilla Custom/Distortion material (prefers one with a normal map) with DistortionColor/Strength; else a bare one.</summary>
        private static Material DistortionInstance()
        {
            if (_distortionInstance != null) return _distortionInstance;
            if (_distortionSearched && Shader.Find(DistortionShaderName) == null) return null;
            _distortionSearched = true;
            Material source = null;
            foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (m == null || m.shader == null || m.shader.name != DistortionShaderName) continue;
                if (source == null || m.name == PreferredDistortionMaterial) source = m;
                if (m.name == PreferredDistortionMaterial) break;
            }
            if (source != null)
            {
                Plugin.Log.LogInfo($"veil distortion: borrowed material '{source.name}'");
                _distortionInstance = new Material(source) { name = "ip_distortion" };
            }
            else
            {
                var shader = Shader.Find(DistortionShaderName);
                if (shader == null) return null;
                _distortionInstance = new Material(shader) { name = "ip_distortion" };
                Plugin.Log.LogInfo("veil distortion: no vanilla material uses the shader; created a bare one (no normal map, refraction may be invisible)");
            }
            LogProperties("distortion (before tuning)", _distortionInstance);
            ApplyDistortionLook(_distortionInstance);
            return _distortionInstance;
        }

        private static void ApplyDistortionLook(Material m)
        {
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, DistortionColor);
            if (m.HasProperty(RefractionId)) m.SetFloat(RefractionId, DistortionStrength);
            // Specular reflections of the sky made the body read as blue glass.
            if (m.HasProperty(GlossinessId)) m.SetFloat(GlossinessId, 0f);
            if (m.HasProperty(MetallicId)) m.SetFloat(MetallicId, 0f);
        }

        // ---------- cleanup ----------

        /// <summary>Drops snapshots of destroyed players (Unity-null keys); nothing to restore, only fog objects and the fog material may remain.</summary>
        public void PruneDead()
        {
            List<Player> dead = null;
            foreach (var kv in _snapshots)
                if (kv.Key == null) (dead ?? (dead = new List<Player>())).Add(kv.Key);
            if (dead == null) return;
            foreach (var k in dead)
            {
                if (_snapshots.TryGetValue(k, out var snap)) DestroyFog(snap);
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
            DestroyFog(snap);
            _snapshots.Remove(p);
        }

        private static void DestroyFog(Snapshot snap)
        {
            foreach (var go in snap.Fog)
            {
                if (go == null) continue;
                go.SetActive(false);   // Destroy is deferred to the end of the frame; hide it now
                Object.Destroy(go);
            }
            snap.Fog.Clear();
            if (snap.FogMaterial != null) Object.Destroy(snap.FogMaterial);
            snap.FogMaterial = null;
        }
    }

    /// <summary>Keeps a fog emitter on its bone: bone position plus an offset in the player's frame, rotation of the player.</summary>
    internal sealed class FogAnchorFollower : MonoBehaviour
    {
        public Transform Bone;
        public Transform Root;
        public Vector3 Offset;

        public void Snap()
        {
            if (Bone == null || Root == null) return;
            transform.SetPositionAndRotation(Bone.position + Root.rotation * Offset, Root.rotation);
        }

        private void LateUpdate() => Snap();
    }
}
