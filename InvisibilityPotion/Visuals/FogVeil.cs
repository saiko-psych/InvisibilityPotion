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
    /// Wraps a veiled player in flattened fog emitters that follow body bones (head, chest, hips, shoulders, hands, legs, feet;
    /// optionally a second, wider and fainter layer on the trunk) and changes the body according to the tier's mode.
    /// Every change is recorded in a per-player snapshot; Remove restores it exactly, whichever mode applied it.
    /// Look values are static, per tier and live-tunable (Debug: ip_fog, ip_veil, ip_fogui); any change bumps
    /// <see cref="Version"/>, which makes the next Apply rebuild the veil. Apply is only called by VeilController.Refresh.
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
        /// <summary>Every GameObject this veil creates starts with this name; ApplyBody and the particle hiding skip them.</summary>
        public const string FogObjectName = "ip_fog";
        private const string OuterObjectName = "ip_fog_outer";
        private const float DynamicColorBlend = 0.5f;

        // ---------- live look state ----------

        /// <summary>Current per-tier look (index 1..3; copies of PluginConfig.Fog(t), mutated by ip_fog / ip_fogui).</summary>
        public static readonly FogSettings[] Fog = { null, FogSettings.Defaults(1), FogSettings.Defaults(2), FogSettings.Defaults(3) };
        public static FogAlphaMode AlphaMode = FogAlphaMode.Both;
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
            for (var t = 1; t <= 3; t++)
                if (_distortionInstances[t] != null) ApplyDistortionLook(_distortionInstances[t], t);
        }

        /// <summary>Re-reads all look values from PluginConfig (startup, ip_reload_config, server sync, ip_fog reset).</summary>
        public static void LoadFromConfig()
        {
            var g = PluginConfig.Global;
            for (var t = 1; t <= 3; t++) Fog[t] = PluginConfig.Fog(t).Clone();
            AlphaMode = PluginConfig.FogAlphaMode;
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

        private static readonly BodyVeilMode[] _configuredModes = { BodyVeilMode.Off, BodyVeilMode.Off, BodyVeilMode.Distortion, BodyVeilMode.Distortion };

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
            public readonly List<FogEmitter> Fog = new List<FogEmitter>();
            public readonly List<Material> FogMaterials = new List<Material>();   // per-veil instances (inner, outer), destroyed on Remove
            public bool MaterialHasColor;
            public Color AppliedColor;     // rgb currently on the fog (dynamic colour follows the environment)
            public readonly Dictionary<Material, float> Cutoffs = new Dictionary<Material, float>();
            public readonly Dictionary<Material, Color> Colors = new Dictionary<Material, Color>();
            public readonly Dictionary<Renderer, Material[]> SharedMaterials = new Dictionary<Renderer, Material[]>();
            public readonly HashSet<Renderer> Hidden = new HashSet<Renderer>();   // renderers disabled by Hide, and vanilla particles in Ghost/Distortion

            public bool FogIntact()
            {
                if (!FogWanted) return true;
                if (Fog.Count == 0) return false;
                foreach (var e in Fog) if (e.Go == null) return false;
                return true;
            }
        }

        private sealed class FogEmitter
        {
            public GameObject Go;
            public ParticleSystem Ps;
            public string Anchor;
            public bool Outer;
            public float VertexAlpha;
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
                UpdateFogColor(existing);
                return;
            }
            Remove(p);
            var fog = Fog[tier];
            var snap = new Snapshot
            {
                Tier = tier, IsLocal = isLocal, ForceHide = forceHide, Version = Version,
                Requested = requested, Effective = Resolve(requested, isLocal, tier),
                FogWanted = !forceHide && fog.Enabled && fog.Alpha > 0f && fog.Rate > 0f,
            };
            _snapshots[p] = snap;
            if (snap.FogWanted) SpawnFog(p, snap);
            if (snap.Fog.Count == 0) snap.FogWanted = false;   // nothing spawned (no material/bones): do not retry every tick
            ApplyBody(p, snap);
            Plugin.Log.LogDebug($"veil T{tier} on {p.GetPlayerName()}: mode {snap.Requested} (effective {snap.Effective}){(forceHide ? " [hidden from players]" : "")}, " +
                                $"fog {snap.Fog.Count} emitters, {snap.Hidden.Count} hidden, {snap.SharedMaterials.Count} swapped, {snap.Colors.Count} tinted, {snap.Cutoffs.Count} cut");
        }

        /// <summary>Maps the requested mode to the one actually applied (self view and missing materials fall back to Tint).</summary>
        private static BodyVeilMode Resolve(BodyVeilMode mode, bool isLocal, int tier)
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
                    if (DistortionInstance(tier) != null) return BodyVeilMode.Distortion;
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
                     : snap.Effective == BodyVeilMode.Distortion ? DistortionInstance(snap.Tier) : null;
            // Vanilla particles parented to the player (rain splashes, wet/tarred status effects, torch flames) would float in
            // the air around an invisible body: hide them while a body-hiding mode is active, restore on Remove.
            var hideParticles = snap.Effective == BodyVeilMode.Ghost || snap.Effective == BodyVeilMode.Distortion;

            foreach (var r in p.GetComponentsInChildren<Renderer>(true))
            {
                // Skips the current fog and a previous one whose deferred Destroy has not run yet (ForceRefresh re-applies in the same frame).
                if (r == null || IsOurs(r.gameObject)) continue;
                if (hideParticles && r is ParticleSystemRenderer)
                {
                    if (r.enabled) { snap.Hidden.Add(r); r.enabled = false; }
                    continue;
                }
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

        private static bool IsOurs(GameObject go) => go.name.StartsWith(FogObjectName, StringComparison.Ordinal);

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
            ["LeftShoulder"] = new[] { "LeftArm", "LeftShoulder" },
            ["RightShoulder"] = new[] { "RightArm", "RightShoulder" },
            ["LeftUpperLeg"] = new[] { "LeftUpLeg" },
            ["RightUpperLeg"] = new[] { "RightUpLeg" },
            ["LeftLowerLeg"] = new[] { "LeftLeg" },
            ["RightLowerLeg"] = new[] { "RightLeg" },
        };

        /// <summary>Anchor names that are not HumanBodyBones names: the shoulder anchors sit on the upper arm bones.</summary>
        private static readonly Dictionary<string, HumanBodyBones> AnchorBones = new Dictionary<string, HumanBodyBones>(StringComparer.OrdinalIgnoreCase)
        {
            ["LeftShoulder"] = HumanBodyBones.LeftUpperArm,
            ["RightShoulder"] = HumanBodyBones.RightUpperArm,
        };

        private static Transform FindBone(Animator animator, string anchorName)
        {
            if (AnchorBones.TryGetValue(anchorName, out var mapped))
            {
                var t = Utils.GetBoneTransform(animator, mapped);
                if (t != null) return t;
            }
            else if (Enum.TryParse(anchorName, true, out HumanBodyBones bone) && Enum.IsDefined(typeof(HumanBodyBones), bone))
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

        private static void SpawnFog(Player p, Snapshot snap)
        {
            var source = FogSourceMaterial();
            if (source == null)
            {
                if (!_fogWarned) { _fogWarned = true; Plugin.Log.LogWarning("veil fog: no particle material available; fog skipped"); }
                return;
            }
            var s = Fog[snap.Tier];
            var color = FogColor(s);
            snap.AppliedColor = color;
            var inner = MakeFogMaterial(source, snap, color, s.Alpha, out var innerVertexAlpha);
            Material outer = null;
            var outerVertexAlpha = 0f;
            if (s.OuterEnabled && s.OuterAlphaFactor > 0f && s.OuterRateFactor > 0f)
                outer = MakeFogMaterial(source, snap, color, Mathf.Clamp01(s.Alpha * s.OuterAlphaFactor), out outerVertexAlpha);

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
                var offset = new Vector3(a.X, a.Y, a.Z);
                snap.Fog.Add(SpawnEmitter(p.transform, bone, a.Name, false, a.Radius, offset, s.Rate, s.Size, color, innerVertexAlpha, inner, s, snap.MaterialHasColor));
                if (outer != null && Array.IndexOf(FogSettings.OuterAnchorNames, a.Name) >= 0)
                    snap.Fog.Add(SpawnEmitter(p.transform, bone, a.Name, true, a.Radius * s.OuterRadiusMultiplier, offset, s.Rate * s.OuterRateFactor,
                                              s.Size * s.OuterSizeFactor, color, outerVertexAlpha, outer, s, snap.MaterialHasColor));
            }
            // No animator at all: one emitter at chest height so the fog does not silently disappear.
            if (animator == null && anyEnabled)
                snap.Fog.Add(SpawnEmitter(p.transform, p.transform, "Root", false, 0.4f, new Vector3(0f, 1f, 0f), s.Rate, s.Size, color, innerVertexAlpha, inner, s, snap.MaterialHasColor));
        }

        /// <summary>Per-veil copy of the borrowed material with the layer's alpha split between material and particle colour.</summary>
        private static Material MakeFogMaterial(Material source, Snapshot snap, Color color, float alpha, out float vertexAlpha)
        {
            var mat = new Material(source) { name = "ip_fog_mat" };   // never mutate the vanilla shared material
            var materialHasColor = mat.HasProperty(ColorId);
            float matAlpha;
            if (!materialHasColor || AlphaMode == FogAlphaMode.Vertex) { matAlpha = 1f; vertexAlpha = alpha; }
            else if (AlphaMode == FogAlphaMode.Material) { matAlpha = alpha; vertexAlpha = 1f; }
            else { matAlpha = vertexAlpha = Mathf.Sqrt(alpha); }   // Both: the product of the two is the target alpha
            if (materialHasColor) mat.SetColor(ColorId, new Color(color.r, color.g, color.b, matAlpha));
            if (mat.HasProperty(EmissionColorId)) mat.SetColor(EmissionColorId, Color.black);
            snap.MaterialHasColor = materialHasColor;
            snap.FogMaterials.Add(mat);
            return mat;
        }

        /// <summary>The tier's fog colour; with DynamicColor blended 50/50 with the environment fog colour (EnvMan sets RenderSettings.fogColor).</summary>
        private static Color FogColor(FogSettings s)
        {
            if (!s.DynamicColor) return new Color(s.R, s.G, s.B);
            var env = RenderSettings.fogColor;
            var c = FogRgb.Blend(s.Color, new FogRgb(env.r, env.g, env.b), DynamicColorBlend);
            return new Color(c.R, c.G, c.B);
        }

        /// <summary>Follows the environment colour on the idempotent Apply path (every refresh) without rebuilding the emitters.</summary>
        private static void UpdateFogColor(Snapshot snap)
        {
            if (snap.Fog.Count == 0) return;
            var s = Fog[snap.Tier];
            if (!s.DynamicColor) return;
            var c = FogColor(s);
            var d = snap.AppliedColor;
            if (Mathf.Abs(c.r - d.r) + Mathf.Abs(c.g - d.g) + Mathf.Abs(c.b - d.b) < 0.01f) return;
            snap.AppliedColor = c;
            if (snap.MaterialHasColor)
            {
                foreach (var m in snap.FogMaterials)
                {
                    if (m == null) continue;
                    var a = m.GetColor(ColorId).a;
                    m.SetColor(ColorId, new Color(c.r, c.g, c.b, a));
                }
                return;
            }
            foreach (var e in snap.Fog)
            {
                if (e.Ps == null) continue;
                var main = e.Ps.main;
                main.startColor = new Color(c.r, c.g, c.b, e.VertexAlpha);
            }
        }

        private static FogEmitter SpawnEmitter(Transform root, Transform bone, string anchor, bool outerLayer, float radius, Vector3 offset, float rate, float size,
                                               Color color, float vertexAlpha, Material mat, FogSettings s, bool materialHasColor)
        {
            var go = new GameObject(outerLayer ? OuterObjectName : FogObjectName);
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
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.25f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            // With a material colour the tint lives there (it can follow the environment); else in the particle colour.
            main.startColor = materialHasColor ? new Color(1f, 1f, 1f, vertexAlpha) : new Color(color.r, color.g, color.b, vertexAlpha);
            main.gravityModifier = 0f;
            main.maxParticles = s.MaxParticles(rate);

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = Mathf.Max(0f, rate);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(0.001f, radius);
            shape.scale = new Vector3(s.SpreadX, s.SpreadY, s.SpreadZ);   // follower rotation = player rotation, so y is up

            var vel = ps.velocityOverLifetime;
            vel.enabled = Mathf.Abs(s.Drift) > 0.0001f;
            vel.space = ParticleSystemSimulationSpace.World;   // drift is vertical regardless of bone rotation
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
            return new FogEmitter { Go = go, Ps = ps, Anchor = anchor, Outer = outerLayer, VertexAlpha = vertexAlpha };
        }

        /// <summary>Live particle count per emitter of a player's veil (tuning window readout). Empty when the player has no veil.</summary>
        public void CollectParticleCounts(Player p, List<KeyValuePair<string, int>> into)
        {
            into.Clear();
            if (p == null || !_snapshots.TryGetValue(p, out var snap)) return;
            foreach (var e in snap.Fog)
                if (e.Ps != null) into.Add(new KeyValuePair<string, int>(e.Outer ? e.Anchor + " (outer)" : e.Anchor, e.Ps.particleCount));
        }

        public bool HasVeil(Player p) => p != null && _snapshots.ContainsKey(p);

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

        private static readonly Material[] _distortionInstances = new Material[4];
        private static Material _distortionSource;
        private static bool _distortionSearched;
        private static bool _distortionLogged;

        /// <summary>
        /// Per-tier copy of a vanilla Custom/Distortion material (prefers one with a normal map) tuned with the tier's
        /// DistortionStrength/DistortionColor; a bare one when no vanilla material uses the shader.
        /// </summary>
        private static Material DistortionInstance(int tier)
        {
            if (tier < 1 || tier > 3) return null;
            if (_distortionInstances[tier] != null) return _distortionInstances[tier];
            if (_distortionSource == null && !_distortionSearched)
            {
                _distortionSearched = true;
                foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (m == null || m.shader == null || m.shader.name != DistortionShaderName) continue;
                    if (_distortionSource == null || m.name == PreferredDistortionMaterial) _distortionSource = m;
                    if (m.name == PreferredDistortionMaterial) break;
                }
                if (_distortionSource != null) Plugin.Log.LogInfo($"veil distortion: borrowed material '{_distortionSource.name}'");
            }
            Material instance;
            if (_distortionSource != null) instance = new Material(_distortionSource) { name = $"ip_distortion_t{tier}" };
            else
            {
                var shader = Shader.Find(DistortionShaderName);
                if (shader == null) return null;
                instance = new Material(shader) { name = $"ip_distortion_t{tier}" };
                Plugin.Log.LogInfo("veil distortion: no vanilla material uses the shader; created a bare one (no normal map, refraction may be invisible)");
            }
            if (!_distortionLogged) { _distortionLogged = true; LogProperties("distortion (before tuning)", instance); }
            ApplyDistortionLook(instance, tier);
            _distortionInstances[tier] = instance;
            return instance;
        }

        private static void ApplyDistortionLook(Material m, int tier)
        {
            var s = Fog[tier];
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, new Color(s.DR, s.DG, s.DB, s.DA));
            if (m.HasProperty(RefractionId)) m.SetFloat(RefractionId, s.DistortionStrength);
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

        /// <summary>Restores the body and destroys the fog. Each restore step is isolated so one failure cannot leave the fog behind.</summary>
        public void Remove(Player p)
        {
            if (p == null || !_snapshots.TryGetValue(p, out var snap)) return;
            _snapshots.Remove(p);
            try
            {
                foreach (var kv in snap.Cutoffs) if (kv.Key != null) kv.Key.SetFloat(CutoffId, kv.Value);
                foreach (var kv in snap.Colors) if (kv.Key != null) kv.Key.SetColor(ColorId, kv.Value);
                foreach (var kv in snap.SharedMaterials) if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
                foreach (var r in snap.Hidden) if (r != null) r.enabled = true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"veil remove: restoring the body failed: {e}");
            }
            finally
            {
                DestroyFog(snap);
            }
        }

        /// <summary>Removes every veil (look change from the console or the tuning window); the next Refresh re-applies from state.</summary>
        public void RemoveAll()
        {
            foreach (var p in new List<Player>(_snapshots.Keys))
            {
                if (p == null) continue;
                Remove(p);
            }
            PruneDead();
        }

        /// <summary>
        /// Destroys any ip_fog* child of an unveiled player (safety net: fog must never outlive the veil, whatever path left it).
        /// Only direct children are checked; the fog emitters are parented to the player root.
        /// </summary>
        public void SweepStrayFog(Player p)
        {
            if (p == null || _snapshots.ContainsKey(p)) return;
            var root = p.transform;
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                if (!IsOurs(child) || !child.activeSelf) continue;
                child.SetActive(false);
                Object.Destroy(child);
                Plugin.Log.LogWarning($"veil: destroyed stray fog object '{child.name}' on {p.GetPlayerName()} (no veil recorded)");
            }
        }

        private static void DestroyFog(Snapshot snap)
        {
            foreach (var e in snap.Fog)
            {
                if (e.Go == null) continue;
                e.Go.SetActive(false);   // Destroy is deferred to the end of the frame; hide it now
                Object.Destroy(e.Go);
            }
            snap.Fog.Clear();
            foreach (var m in snap.FogMaterials) if (m != null) Object.Destroy(m);
            snap.FogMaterials.Clear();
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
