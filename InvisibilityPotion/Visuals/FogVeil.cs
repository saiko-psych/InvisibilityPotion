using System;
using System.Collections.Generic;
using InvisibilityPotion.Config;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace InvisibilityPotion.Visuals
{
    /// <summary>How a veiled player's body is drawn. Per tier in the config (TierN.BodyVeilMode); ip_veil overrides it at runtime.</summary>
    public enum BodyVeilMode { Off, Cutoff, Hide, Tint, Ghost, Distortion, Shadow, Spirit }

    /// <summary>
    /// Wraps a veiled player in fog: an inner layer on body bones (head, chest, hips, shoulders, hands, legs, feet) or on the body
    /// mesh surface, optionally a second, wide and flat outer layer on selected bones, and optionally a ground fog field at the feet
    /// (world space, ground-parallel particles that stay and spread); inner and outer either follow the body (local space) or leave
    /// a trail (world space). Changes the body according to the tier's mode.
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
        private static readonly int NormalTexId = Shader.PropertyToID("_NormalTex");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int NormalScaleId = Shader.PropertyToID("_NormalScale");
        private static readonly int WaveVelId = Shader.PropertyToID("_WaveVel");
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        private static readonly int ZFadeDistanceId = Shader.PropertyToID("_ZFadeDistance");
        private static readonly int CameraFadeMinId = Shader.PropertyToID("_CameraFadeDistanceMin");
        private static readonly int CameraFadeMaxId = Shader.PropertyToID("_CameraFadeDistanceMax");
        private static readonly int CameraYFadeId = Shader.PropertyToID("_CameraYFadeDistance");
        private static readonly int BillboardId = Shader.PropertyToID("_Billboard");
        private static readonly int LightNormalFactorId = Shader.PropertyToID("_LightNormalFactor");
        private static readonly int SkyMaskId = Shader.PropertyToID("_SkyMask");
        // swamp_mist (Custom/LitParticles) ships _ZFadeDistance 1 (soft-particle fade within 1 m of the surface behind) and
        // _CameraFadeDistanceMin/Max 1/5 (fades particles within 5 m of the camera): made for a ground mist the camera walks
        // through, but our fog always sits 3-5 m from the third-person camera and close to the ground/body, so most of it was faded
        // out. Our per-veil copy fades only right at the camera and at a hard intersection.
        private const float FogZFadeDistance = 0.25f;
        private const float FogCameraFadeMin = 0.3f;
        private const float FogCameraFadeMax = 1f;
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
        /// <summary>Round Q ruling 1b: the follow part of the outer volume (local space, moves with the player).</summary>
        private const string OuterFollowObjectName = "ip_fog_outer_follow";
        private const string GroundObjectName = "ip_fog_ground";
        private const string GroundAnchorName = "Ground";
        private const string GroundUpperAnchorName = "GroundUpper";
        private const float DynamicColorBlend = 0.5f;

        // ---------- live look state ----------

        /// <summary>Current per-tier look (index 1..3; copies of PluginConfig.Fog(t), mutated by ip_fog / ip_fogui).</summary>
        public static readonly FogSettings[] Fog = { null, FogSettings.Defaults(1), FogSettings.Defaults(2), FogSettings.Defaults(3) };
        public static FogAlphaMode AlphaMode = FogAlphaMode.Both;
        public static Color GhostColor = new Color(0.75f, 0.8f, 0.9f, 1f);
        public static float GhostEmission = 0.25f;
        public static Color ShadowColor = DefaultShadowColor;
        /// <summary>Spirit tint (rgb, alpha ignored); _TintColor = SpiritColor x SpiritStrength.</summary>
        public static Color SpiritColor = DefaultSpiritColor;
        public static float SpiritStrength = DefaultSpiritStrength;
        public static readonly Color DefaultShadowColor = new Color(0f, 0f, 0f, 0.12f);
        public static readonly Color DefaultSpiritColor = new Color(0.6f, 0.7f, 0.8f, 1f);
        public const float DefaultSpiritStrength = 0.25f;
        /// <summary>Runtime body-mode override per tier (index 1..3), null = use the tier's config. Set by ip_veil.</summary>
        public static readonly BodyVeilMode?[] ModeOverride = new BodyVeilMode?[4];
#if DEBUG
        /// <summary>Tuning window "Solo outer": spawn only the outer layer (inner layer and ground field off) without touching the settings. Debug only.</summary>
        public static bool SoloOuter;
#endif
        /// <summary>Bumped on every look change; a snapshot of another version is rebuilt on the next Apply.</summary>
        public static int Version { get; private set; }

        public static void Bump()
        {
            Version++;
            if (_ghostInstance != null) ApplyGhostLook(_ghostInstance);
            if (_shadowInstance != null) ApplyShadowLook(_shadowInstance);
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
            ShadowColor = ParseColor(g.ShadowColor, 4, DefaultShadowColor, "ShadowColor");
            SpiritColor = ParseColor(g.SpiritColor, 3, DefaultSpiritColor, "SpiritColor");
            SpiritStrength = Mathf.Max(0f, g.SpiritStrength);
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

        public static string FormatRgb(Color c) => FloatList.Format(c.r, c.g, c.b);

        /// <summary>Writes the global body looks (Ghost, Shadow, Spirit) to [Veil].</summary>
        public static void SaveGlobalLook() =>
            PluginConfig.SaveVeilLook(FormatColor(GhostColor), GhostEmission, FormatColor(ShadowColor), FormatRgb(SpiritColor), SpiritStrength);

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
                Plugin.Log.LogWarning($"[Tier{t}] BodyVeilMode '{text}' is not one of {string.Join(", ", Enum.GetNames(typeof(BodyVeilMode)))}; using the default {mode}");
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
            public float BuiltAt;   // Time.time of the (re)build; the Debug dump prints the veil's age to expose rebuild loops
            public readonly List<FogEmitter> Fog = new List<FogEmitter>();
            public readonly List<Material> FogMaterials = new List<Material>();   // per-veil instances (inner, outer), destroyed on Remove
            public bool MaterialHasColor;
            public Color AppliedColor;     // rgb currently on the fog (dynamic colour follows the environment)
            public readonly Dictionary<Material, float> Cutoffs = new Dictionary<Material, float>();
            public readonly Dictionary<Material, Color> Colors = new Dictionary<Material, Color>();
            public readonly Dictionary<Renderer, Material[]> SharedMaterials = new Dictionary<Renderer, Material[]>();
            public readonly HashSet<Renderer> Hidden = new HashSet<Renderer>();   // renderers disabled by Hide, and vanilla particles in the swap modes
            public readonly Dictionary<Material, Material> SpiritByOriginal = new Dictionary<Material, Material>();   // per-veil Spirit copies by original material
            public readonly List<Material> SpiritOwned = new List<Material>();   // every Spirit copy of this veil, destroyed on Remove

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
            public FogLayerKind Layer;
            public float VertexAlpha;
            public int Burst;   // particles emitted at once at spawn (outer volume), for the spawn log
        }
        private readonly Dictionary<Player, Snapshot> _snapshots = new Dictionary<Player, Snapshot>();

        private static readonly List<Material> MatBuf = new List<Material>();
        private static readonly List<Renderer> RendererBuf = new List<Renderer>();

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
                FogWanted = !forceHide && fog.Enabled && (fog.InnerActive || fog.OuterActive || fog.GroundActive),
                BuiltAt = Time.time,
            };
            _snapshots[p] = snap;
            if (snap.FogWanted) SpawnFog(p, snap);
            if (snap.Fog.Count == 0) snap.FogWanted = false;   // nothing spawned (no material/bones): do not retry every tick
#if DEBUG
            if (isLocal)
            {
                LogSpawn(snap);
                // auto fog dump: VeilController logs the full dump once, AutoDumpDelay after the latest local build.
                AutoDumpAt = Time.time + AutoDumpDelay;
                AutoDumpBuiltAt = snap.BuiltAt;
            }
#endif
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
                case BodyVeilMode.Shadow:
                    if (ShadowInstance() != null) return BodyVeilMode.Shadow;
                    Plugin.Log.LogWarning($"veil mode Shadow: material '{ShadowMaterialName}' not found (prefab {ShadowPrefabName}, SP_* items, loaded materials); using Tint");
                    return BodyVeilMode.Tint;
                case BodyVeilMode.Spirit:
                    if (SpiritShader() != null) return BodyVeilMode.Spirit;
                    Plugin.Log.LogWarning($"veil mode Spirit: shader '{SpiritShaderName}' not found; using Tint");
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
                     : snap.Effective == BodyVeilMode.Distortion ? DistortionInstance(snap.Tier)
                     : snap.Effective == BodyVeilMode.Shadow ? ShadowInstance() : null;
            // Vanilla particles parented to the player (rain splashes, wet/tarred status effects, torch flames) would float in
            // the air around an invisible body: hide them while a body-hiding mode is active, restore on Remove.
            var hideParticles = IsSwapMode(snap.Effective);
            // MaterialMan (decision, task 10e): VisEquipment puts only _SnowCover into the MaterialPropertyBlock that MaterialMan
            // sets on every player MeshRenderer/SkinnedMeshRenderer (VisEquipment.cs:237/1447, MaterialMan.cs:88). A block only
            // overrides the properties it contains; none of the swap shaders (Custom/Distortion, Standard, Custom/Fallen Warrior)
            // declares _SnowCover, and Custom/Creature (Ghost) uses it as intended. So the block is left alone: clearing it would
            // be undone by MaterialMan's next UpdateBlock anyway, and restoring it on Remove would race with MaterialMan.

            p.GetComponentsInChildren(true, RendererBuf);
            foreach (var r in RendererBuf)
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
                    case BodyVeilMode.Shadow:
                        if (swap == null || !IsBodyRenderer(r) || snap.SharedMaterials.ContainsKey(r)) break;
                        var originals = r.sharedMaterials;
                        snap.SharedMaterials[r] = originals;
                        var replaced = new Material[originals.Length];
                        for (var i = 0; i < replaced.Length; i++) replaced[i] = swap;
                        r.sharedMaterials = replaced;
                        break;
                    case BodyVeilMode.Spirit:
                        if (!IsBodyRenderer(r) || snap.SharedMaterials.ContainsKey(r)) break;
                        var spiritOriginals = r.sharedMaterials;
                        var spirit = new Material[spiritOriginals.Length];
                        for (var i = 0; i < spirit.Length; i++) spirit[i] = SpiritFor(snap, spiritOriginals[i]);
                        snap.SharedMaterials[r] = spiritOriginals;
                        r.sharedMaterials = spirit;
                        break;
                }
            }
            MatBuf.Clear();
        }

        private static bool IsSwapMode(BodyVeilMode m) =>
            m == BodyVeilMode.Ghost || m == BodyVeilMode.Distortion || m == BodyVeilMode.Shadow || m == BodyVeilMode.Spirit;

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
            var s = Fog[snap.Tier];
            var source = FogSourceMaterial(s.FogMaterial);
            if (source == null)
            {
                if (!_fogWarned) { _fogWarned = true; Plugin.Log.LogWarning("veil fog: no particle material available; fog skipped"); }
                return;
            }
            var color = FogColor(s);
            snap.AppliedColor = color;
            var innerActive = s.InnerActive;
            var groundActive = s.GroundActive;
#if DEBUG
            if (SoloOuter) innerActive = groundActive = false;
#endif
            var innerVertexAlpha = 0f;
            var inner = innerActive ? MakeFogMaterial(source, snap, color, s.Alpha, s.Emission, out innerVertexAlpha) : null;
            // The outer layer is independent of the inner one (rate, alpha): it can be tuned and tested alone.
            Material outer = null;
            var outerVertexAlpha = 0f;
            if (s.OuterActive) outer = MakeFogMaterial(source, snap, color, s.OuterAlpha, s.Emission, out outerVertexAlpha);
            var groundVertexAlpha = 0f;
            var ground = groundActive ? MakeFogMaterial(source, snap, color, s.GroundAlpha, s.Emission, out groundVertexAlpha) : null;
            var groundUpperVertexAlpha = 0f;
            var groundUpper = groundActive ? MakeFogMaterial(source, snap, color, s.GroundUpperAlpha, s.Emission, out groundUpperVertexAlpha) : null;

            var animator = p.m_animator;
            if (animator == null && !_noAnimatorWarned)
            {
                _noAnimatorWarned = true;
                Plugin.Log.LogWarning("veil fog: player has no animator; using one emitter at the player root");
            }

            // Inner layer: the body surface (Mesh) or one emitter per enabled anchor (Bones, also the Mesh fallback).
            var innerLayer = new FogLayer
            {
                Rate = s.Rate, Size = s.Size, Lifetime = s.Lifetime, SpreadX = s.SpreadX, SpreadY = s.SpreadY, SpreadZ = s.SpreadZ,
                Trail = s.InnerTrailMode, VertexAlpha = innerVertexAlpha, Material = inner,
                MaxParticles = s.InnerMaxParticles,   // round M ruling 2c: <= InnerParticleCap per anchor
            };
            // inner == null: inner layer off (alpha or rate 0, or solo outer).
            if (inner != null && !(s.EmitterMode == FogEmitterMode.Mesh && SpawnMeshFog(p, snap, s, color, innerLayer)))
            {
                var anyEnabled = false;
                foreach (var a in s.Anchors)
                {
                    if (!a.Enabled || s.Rate <= 0f) continue;
                    anyEnabled = true;
                    if (animator == null) break;
                    var bone = BoneFor(animator, a.Name);
                    if (bone == null) continue;
                    snap.Fog.Add(SpawnEmitter(p.transform, bone, a.Name, FogLayerKind.Inner, a.Radius, new Vector3(a.X, a.Y, a.Z), color, innerLayer, s, snap.MaterialHasColor));
                }
                // No animator at all: one emitter at chest height so the fog does not silently disappear.
                if (animator == null && anyEnabled)
                    snap.Fog.Add(SpawnEmitter(p.transform, p.transform, "Root", FogLayerKind.Inner, 0.4f, new Vector3(0f, 1f, 0f), color, innerLayer, s, snap.MaterialHasColor));
            }

            // Ground field: one world-space emitter at the feet (Hips projected to the ground; the root without an animator).
            if (ground != null)
            {
                var hips = animator != null ? FindBone(animator, "Hips") : null;
                snap.Fog.Add(SpawnGroundEmitter(p.transform, hips, color, s, false, ground, groundVertexAlpha, snap.MaterialHasColor));
                // Second, fainter and sparser layer higher up: the patches overlap into a volume instead of one flat disc.
                if (groundUpper != null)
                    snap.Fog.Add(SpawnGroundEmitter(p.transform, hips, color, s, true, groundUpper, groundUpperVertexAlpha, snap.MaterialHasColor));
            }

            // Outer layer: always on bones (OuterAnchors, independent of the inner anchors' on/off), in both emitter modes, offset
            // OuterOffsetY from the anchor offset, turning at OuterRotation (orbital velocity); its own rate, size, alpha, lifetime.
            // OuterShape Volume (round L ruling 1): large soft sprites inside a flattened sphere of OuterRadius (height OuterSpreadY x
            // radius), size 0.8..1.2 x OuterSize, slow drift in a random direction, alpha fading in over 20 % and out to the end.
            // OuterShape Ring (round J ruling): Circle shape near the rim of OuterRadius, band height OuterSpreadY x radius.
            // Round M ruling 1: with OuterTrail (world space) the volume is left behind; rateOverDistance lays it along the path,
            // the sprites grow by OuterVolumeGrow over their life, and at most OuterVolumeParticleCap live per emitter.
            // Round P ruling 1: the volume emits OuterBurstCount particles at once when it is created (every spawn, so also every
            // rebuild after a look change), so the fog exists the moment the effect starts instead of filling up over seconds.
            // Round Q ruling 1: the burst puffs start pre-aged (full alpha in the first frame), and the volume is two systems from
            // the same keys and material: Follow (local space, OuterFollowShare of rate/burst/cap, radius 0.7 x OuterRadius, so the
            // player is always inside fog) and Trail (world space as before, the rest, left behind). Without the split (Ring, or
            // OuterTrail false, or share 0) the trail system is the only one, exactly as in round P.
            // Round R ruling A: both volume systems follow the bone by position only and keep world rotation (identity), so turning
            // the player no longer swings the local-space follow puffs around like a spotlight. The follow part gets 60 % of the
            // burst on an even pattern (FogBurstPattern), puffs 0.85 x OuterSize, and orbits slowly (0.15 m/s) around the player.
            if (outer == null || animator == null) return;
            var volume = s.OuterShape == FogOuterShape.Volume;
            FogLayer OuterLayer(bool follow)
            {
                var l = new FogLayer
                {
                    Rate = follow ? s.OuterFollowRate : s.OuterTrailRate, Size = follow ? s.OuterFollowSize : s.OuterSize, Lifetime = s.OuterLifetime,
                    SpreadX = 1f, SpreadY = s.OuterSpreadY, SpreadZ = 1f,
                    Trail = follow ? FogTrailMode.Follow : s.OuterTrailMode, VertexAlpha = outerVertexAlpha, Material = outer, Horizontal = s.OuterHorizontal,
                    Ring = !volume, Volume = volume, Orbital = follow ? s.OuterFollowOrbitalRadPerSecond : s.OuterOrbitalRadPerSecond,
                    PositionOnly = volume, EvenBurst = follow,
                    RateDistance = follow ? 0f : s.OuterEffectiveRateDistance,
                    MaxParticles = follow ? s.OuterFollowMaxParticles : s.OuterTrailMaxParticles,
                };
                if (volume)
                {
                    l.SizeMinFactor = FogSettings.OuterVolumeSizeMinFactor;
                    l.SizeMaxFactor = FogSettings.OuterVolumeSizeMaxFactor;
                    l.Speed = FogSettings.OuterVolumeSpeed;
                    l.Grow = FogSettings.OuterVolumeGrow;
                    l.Burst = follow ? s.OuterFollowBurstCount : s.OuterTrailBurstCount;
                }
                return l;
            }
            var trailLayer = s.OuterHasTrail ? OuterLayer(false) : null;
            var followLayer = s.OuterHasFollow ? OuterLayer(true) : null;
            foreach (var name in s.OuterAnchors)
            {
                var a = s.Anchor(name);
                if (a == null) continue;
                var bone = BoneFor(animator, a.Name);
                if (bone == null) continue;
                var offset = new Vector3(a.X, a.Y + s.OuterOffsetY, a.Z);
                if (followLayer != null)
                    snap.Fog.Add(SpawnEmitter(p.transform, bone, a.Name + " follow", FogLayerKind.Outer, s.OuterFollowRadius, offset, color, followLayer, s, snap.MaterialHasColor, OuterFollowObjectName));
                if (trailLayer != null)
                    snap.Fog.Add(SpawnEmitter(p.transform, bone, followLayer != null ? a.Name + " trail" : a.Name, FogLayerKind.Outer, s.OuterRadius, offset, color, trailLayer, s, snap.MaterialHasColor));
            }
        }

        private static Transform BoneFor(Animator animator, string anchor)
        {
            var bone = FindBone(animator, anchor);
            if (bone == null && _missingBoneWarned.Add(anchor)) Plugin.Log.LogWarning($"veil fog: bone for anchor '{anchor}' not found; anchor skipped");
            return bone;
        }

        /// <summary>Per-layer emission values shared by every emitter of one fog layer.</summary>
        private sealed class FogLayer
        {
            public float Rate, Size, Lifetime, SpreadX, SpreadY, SpreadZ, VertexAlpha;
            public FogTrailMode Trail;
            public bool Horizontal;   // HorizontalBillboard (quads parallel to the ground) instead of camera-facing billboards
            public bool Ring;         // Circle shape near the rim (outer ring) instead of a scaled sphere; SpreadY = band height x radius
            // Outer fog volume (round L): flattened sphere of radius x (1, SpreadY, 1), random start direction, no inner Drift, alpha
            // 0 -> 1 at OuterVolumeFadeIn -> 0 at the end, and no renderer size clamp (3 m sprites near the camera stay full size).
            public bool Volume;
            public float SizeMinFactor = 0.6f, SizeMaxFactor = 1.25f;   // start size = random [min, max] x Size
            public float Speed = float.NaN;                              // start speed m/s; NaN = the tier's Speed
            public float Orbital;     // rad/s around the emitter's up axis (velocityOverLifetime.orbitalY); 0 = none
            public float RateDistance;  // rateOverDistance, particles per metre moved (world space only); 0 = none
            public float Grow = 1f;     // size at death = start size x Grow (linear sizeOverLifetime); 1 = constant size
            public int MaxParticles;    // maxParticles; 0 = rate x lifetime budget (FogSettings.MaxParticles)
            public int Burst;           // particles emitted at once right after Play (outer volume, round P); 0 = none
            // Round R ruling A1: the follower copies the bone position only and keeps world rotation identity (volume emitters).
            public bool PositionOnly;
            // Round R ruling A2: the burst goes on the even FogBurstPattern instead of random shape points (follow volume).
            public bool EvenBurst;
            public Material Material;
        }

        /// <summary>
        /// Per-veil copy of the borrowed material with the layer's alpha split between material and particle colour, the tier's
        /// emission (colour x Emission) and shorter soft/camera fades (see FogZFadeDistance).
        /// Brightness (round I, matte fog): _Color rgb is the configured colour clamped to 0..1 (never HDR-brightened; the vanilla
        /// swamp_mist value is white 1,1,1), the particle (vertex) colour rgb is white, and _EmissionColor is colour x Emission, i.e.
        /// black at Emission 0 (the vanilla swamp_mist value is black 0,0,0,1). The lighting-related properties of the borrowed
        /// material (_LightNormalFactor 0, _BumpScale 0.27, _NormalTex wave-normal, _SkyMask 0 in vanilla) are left as they are:
        /// how Custom/LitParticles lights a particle is not known from the decompile, so their semantics are not guessed.
        /// </summary>
        private static Material MakeFogMaterial(Material source, Snapshot snap, Color color, float alpha, float emission, out float vertexAlpha)
        {
            var mat = new Material(source) { name = "ip_fog_mat" };   // never mutate the vanilla shared material
            color = ClampRgb(color);
            var materialHasColor = mat.HasProperty(ColorId);
            float matAlpha;
            if (!materialHasColor || AlphaMode == FogAlphaMode.Vertex) { matAlpha = 1f; vertexAlpha = alpha; }
            else if (AlphaMode == FogAlphaMode.Material) { matAlpha = alpha; vertexAlpha = 1f; }
            else { matAlpha = vertexAlpha = Mathf.Sqrt(alpha); }   // Both: the product of the two is the target alpha
            if (materialHasColor) mat.SetColor(ColorId, new Color(color.r, color.g, color.b, matAlpha));
            if (mat.HasProperty(EmissionColorId)) mat.SetColor(EmissionColorId, Emission(color, emission));
            if (mat.HasProperty(ZFadeDistanceId)) mat.SetFloat(ZFadeDistanceId, FogZFadeDistance);
            if (mat.HasProperty(CameraFadeMinId) && mat.HasProperty(CameraFadeMaxId))
            {
                mat.SetFloat(CameraFadeMinId, FogCameraFadeMin);
                mat.SetFloat(CameraFadeMaxId, FogCameraFadeMax);
            }
            snap.MaterialHasColor = materialHasColor;
            snap.FogMaterials.Add(mat);
            return mat;
        }

        /// <summary>_EmissionColor: colour x Emission, black (0,0,0,1, the vanilla swamp_mist value) at Emission 0.</summary>
        private static Color Emission(Color color, float emission)
        {
            var k = Mathf.Clamp01(emission);
            color = ClampRgb(color);
            return new Color(color.r * k, color.g * k, color.b * k, 1f);
        }

        /// <summary>The fog colour never goes above 1 (an HDR environment fog colour in the DynamicColor blend must not brighten it).</summary>
        private static Color ClampRgb(Color c) => new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), c.a);

        /// <summary>The tier's fog colour; with DynamicColor blended 50/50 with the environment fog colour (EnvMan sets RenderSettings.fogColor).</summary>
        private static Color FogColor(FogSettings s)
        {
            if (!s.DynamicColor) return ClampRgb(new Color(s.R, s.G, s.B));
            var env = RenderSettings.fogColor;
            var c = FogRgb.Blend(s.Color, new FogRgb(env.r, env.g, env.b), DynamicColorBlend);
            return ClampRgb(new Color(c.R, c.G, c.B));
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
                    if (m.HasProperty(EmissionColorId)) m.SetColor(EmissionColorId, Emission(c, s.Emission));
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

        private static FogEmitter SpawnEmitter(Transform root, Transform bone, string anchor, FogLayerKind kind, float radius, Vector3 offset,
                                               Color color, FogLayer layer, FogSettings s, bool materialHasColor, string objectName = null)
        {
            var go = new GameObject(objectName ?? (kind == FogLayerKind.Outer ? OuterObjectName : FogObjectName));
            go.SetActive(false);   // configure before the system starts playing
            go.transform.SetParent(root, false);   // destroyed with the player; position driven by the follower
            var follower = go.AddComponent<FogAnchorFollower>();
            follower.Bone = bone;
            follower.Root = root;
            follower.Offset = offset;
            follower.PositionOnly = layer.PositionOnly;
            follower.SpawnedAt = Time.time;
            follower.Snap();

            var ps = ConfigureSystem(go, color, layer, s, materialHasColor);
            var shape = ps.shape;
            shape.enabled = true;
            if (layer.Ring)
            {
                // Ring: the Circle shape lies in its local XY plane; rotated 90 deg about x it lies flat (XZ, y up in the follower =
                // player frame). radiusThickness keeps the spawn points in the outer quarter of the radius, so it reads as a ring,
                // and the random position jitter gives the band its height (and a little radial softness).
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = Mathf.Max(0.001f, radius);
                shape.radiusThickness = FogSettings.OuterRingThickness;
                shape.arc = 360f;
                shape.rotation = new Vector3(90f, 0f, 0f);
                shape.scale = Vector3.one;
                shape.randomPositionAmount = Mathf.Max(0f, layer.SpreadY * radius);
            }
            else if (layer.Volume)
            {
                // Fog volume: the whole flattened sphere (radiusThickness 1), random start directions for the slow drift.
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = Mathf.Max(0.001f, radius);
                shape.radiusThickness = 1f;
                shape.scale = new Vector3(1f, layer.SpreadY, 1f);   // position-only follower (round R): world rotation, y is world up
                shape.randomDirectionAmount = 1f;
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = Mathf.Max(0.001f, radius);
                shape.scale = new Vector3(layer.SpreadX, layer.SpreadY, layer.SpreadZ);   // follower rotation = player rotation, so y is up
            }

            // Simulation space (Follow/Trail) is set in ConfigureSystem, before the system plays.
            go.SetActive(true);
            ps.Play();
            var burst = layer.Volume && layer.Burst > 0
                ? EmitBurst(ps, layer.Burst, layer.EvenBurst && layer.Trail == FogTrailMode.Follow, radius, layer.SpreadY * radius)
                : 0;
            return new FogEmitter { Go = go, Ps = ps, Anchor = anchor, Layer = kind, VertexAlpha = layer.VertexAlpha, Burst = burst };
        }

        private static readonly ParticleSystem.Particle[] BurstBuf = new ParticleSystem.Particle[FogSettings.ParticleHardCap];

        /// <summary>
        /// Round P ruling 1: fills the whole volume at once; the shape spreads the particles over the flattened sphere, and size,
        /// rotation, colour, speed and start lifetime come from the system as for normal emission. Round Q ruling 1a: the new
        /// particles are then aged in place (GetParticles/SetParticles): remaining lifetime random 30..90 % of their start
        /// lifetime, so they are past the fade-in and at full alpha in the first frame, and they do not all fade out together.
        /// Round R ruling A2: with <paramref name="even"/> (follow volume, local simulation space) the new particles are moved onto
        /// the even FogBurstPattern (golden-angle spiral, 0.3..1 x <paramref name="radius"/>, heights over +-<paramref name="halfHeight"/>)
        /// in the emitter's local space, so the instant cloud has neither holes nor clusters; the continuous rate stays random.
        /// Returns the number emitted.
        /// </summary>
        private static int EmitBurst(ParticleSystem ps, int count, bool even, float radius, float halfHeight)
        {
            var before = ps.particleCount;
            ps.Emit(count);
            var n = ps.GetParticles(BurstBuf);
            var first = Mathf.Min(before, n);
            for (var i = first; i < n; i++)
            {
                BurstBuf[i].remainingLifetime = FogSettings.OuterBurstRemainingLifetime(BurstBuf[i].startLifetime, UnityEngine.Random.value);
                if (!even) continue;
                var pt = FogBurstPattern.Point(i - first, n - first, radius, halfHeight);
                BurstBuf[i].position = new Vector3(pt.x, pt.y, pt.z);
            }
            ps.SetParticles(BurstBuf, n);
            return n - before;
        }

        /// <summary>
        /// Ground fog field (round H): one world-space emitter at the feet. Quads parallel to the ground (HorizontalBillboard), so
        /// they do not stand in the terrain the way upright billboards do (the borrowed soft-particle depth fade eats those). Each
        /// particle stays where it was emitted, almost still, and grows from GroundSize to GroundSize x GroundGrow; rateOverTime plus
        /// rateOverDistance (world space only) lays the field along the walked path. Alpha fades in over the first 15 % and out over
        /// the last 40 % of the lifetime.
        /// Fix round 2 (ruling 5): start size random in [GroundSizeMinFactor, GroundSizeMaxFactor] x GroundSize, and two height
        /// layers: the lower one at GroundHeight, the upper one (<paramref name="upper"/>) at GroundUpperHeight with
        /// GroundUpperRateFactor of the rates and GroundUpperAlpha (its material). Both share GroundParticleBudget.
        /// </summary>
        private static FogEmitter SpawnGroundEmitter(Transform root, Transform hips, Color color, FogSettings s, bool upper, Material material, float vertexAlpha, bool materialHasColor)
        {
            var go = new GameObject(GroundObjectName);
            go.SetActive(false);
            go.transform.SetParent(root, false);
            var follower = go.AddComponent<FogAnchorFollower>();
            follower.Bone = hips != null ? hips : root;
            follower.Root = root;
            follower.Ground = true;
            follower.GroundHeight = upper ? s.GroundUpperHeight : s.GroundHeight;
            follower.SpawnedAt = Time.time;
            follower.Snap();

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = Mathf.Max(0.05f, s.GroundLifetime);
            main.startSpeed = GroundStartSpeed;
            main.startSize = new ParticleSystem.MinMaxCurve(s.GroundSize * FogSettings.GroundSizeMinFactor, s.GroundSize * FogSettings.GroundSizeMaxFactor);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = materialHasColor ? new Color(1f, 1f, 1f, vertexAlpha) : new Color(color.r, color.g, color.b, vertexAlpha);
            main.gravityModifier = 0f;
            main.maxParticles = upper ? s.GroundUpperMaxParticles : s.GroundLowerMaxParticles;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = upper ? s.GroundUpperRate : Mathf.Max(0f, s.GroundRate);
            emission.rateOverDistance = upper ? s.GroundUpperRateDistance : Mathf.Max(0f, s.GroundRateDistance);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(0.001f, s.GroundRadius);
            shape.scale = new Vector3(1f, GroundShapeFlatten, 1f);   // a flat disc at GroundHeight

            // Random horizontal drift. All three axes must use the same curve mode (Unity rejects mixed modes).
            var vel = ps.velocityOverLifetime;
            var drift = Mathf.Max(0f, s.GroundDrift);
            vel.enabled = drift > 0.0001f;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-drift, drift);
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(-drift, drift);

            var grow = Mathf.Max(0.1f, s.GroundGrow);
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            // Curve x multiplier: 1 at birth, GroundGrow at death; ease-out so the patch spreads early and settles.
            var curve = new AnimationCurve(new Keyframe(0f, 1f / grow, 0f, 2f * (1f - 1f / grow)), new Keyframe(1f, 1f, 0f, 0f));
            sol.size = new ParticleSystem.MinMaxCurve(grow, curve);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = material;
            psr.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            psr.maxParticleSize = 10f;   // viewport fraction; the default 0.5 clips a grown 3 m patch when the camera is close
            psr.shadowCastingMode = ShadowCastingMode.Off;
            psr.receiveShadows = false;
            psr.lightProbeUsage = LightProbeUsage.Off;

            go.SetActive(true);
            ps.Play();
            return new FogEmitter { Go = go, Ps = ps, Anchor = upper ? GroundUpperAnchorName : GroundAnchorName, Layer = FogLayerKind.Ground, VertexAlpha = vertexAlpha };
        }

        /// <summary>Ground particles are almost still (start speed in m/s, outward from the spawn disc).</summary>
        private const float GroundStartSpeed = 0.01f;
        /// <summary>Vertical shape scale of the ground spawn sphere: a flat disc a few centimetres thick.</summary>
        private const float GroundShapeFlatten = 0.05f;

        private static readonly HashSet<string> _meshWarned = new HashSet<string>();
        private const string MeshAnchorName = "Mesh";

        /// <summary>
        /// FogEmitterMode Mesh: one inner emitter spawning on the body mesh surface (ShapeModule SkinnedMeshRenderer, like the
        /// vanilla Ghost's black_smoke). Rate = MeshRate, or Rate x enabled anchors when MeshRate is 0. The outer layer stays on
        /// bones (SpawnFog). False (caller falls back to Bones) when no readable body mesh exists: Unity cannot sample a mesh
        /// without Read/Write.
        /// </summary>
        private static bool SpawnMeshFog(Player p, Snapshot snap, FogSettings s, Color color, FogLayer inner)
        {
            var smr = FindBodyRenderer(p);
            string problem = null;
            if (smr == null) problem = "no body SkinnedMeshRenderer under Visual";
            else if (smr.sharedMesh == null) problem = $"body renderer '{smr.name}' has no mesh";
            else if (!smr.sharedMesh.isReadable) problem = $"mesh '{smr.sharedMesh.name}' of renderer '{smr.name}' is not readable";
            if (problem != null)
            {
                if (_meshWarned.Add(problem)) Plugin.Log.LogWarning($"veil fog: Mesh emitter not possible ({problem}); using Bones");
                return false;
            }
            var anchors = 0;
            foreach (var a in s.Anchors) if (a.Enabled) anchors++;
            var rate = s.MeshEmitterRate(anchors);
            if (rate <= 0f) return true;   // same as Bones with every anchor off: no inner fog
            var layer = new FogLayer
            {
                Rate = rate, Size = inner.Size, Lifetime = inner.Lifetime, SpreadX = inner.SpreadX, SpreadY = inner.SpreadY, SpreadZ = inner.SpreadZ, Trail = inner.Trail,
                VertexAlpha = inner.VertexAlpha, Material = inner.Material, MaxParticles = s.MeshMaxParticles(anchors),
            };
            snap.Fog.Add(SpawnMeshEmitter(p.transform, smr, s.MeshOffset, color, layer, s, snap.MaterialHasColor));
            if (_meshWarned.Add("ok:" + smr.name)) Plugin.Log.LogInfo($"veil fog: Mesh emitter on '{smr.name}' (mesh '{smr.sharedMesh.name}')");
            return true;
        }

        /// <summary>The player's body SkinnedMeshRenderer under Visual: the one named "body", else VisEquipment.m_bodyModel, else the first.</summary>
        private static SkinnedMeshRenderer FindBodyRenderer(Player p)
        {
            var visual = p.m_visual != null ? p.m_visual.transform : p.transform;
            SkinnedMeshRenderer first = null;
            foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || IsOurs(smr.gameObject)) continue;
                if (string.Equals(smr.name, "body", StringComparison.OrdinalIgnoreCase)) return smr;
                if (first == null) first = smr;
            }
            var ve = p.m_visEquipment;
            return ve != null && ve.m_bodyModel != null ? ve.m_bodyModel : first;
        }

        private static FogEmitter SpawnMeshEmitter(Transform root, SkinnedMeshRenderer smr, float normalOffset, Color color, FogLayer layer, FogSettings s, bool materialHasColor)
        {
            var go = new GameObject(FogObjectName);
            go.SetActive(false);
            // Identity under the player root: new particles always spawn on the current body surface; Follow (local space) keeps
            // them with the player, Trail (world space) leaves them where they were emitted.
            go.transform.SetParent(root, false);
            var ps = ConfigureSystem(go, color, layer, s, materialHasColor);
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
            shape.skinnedMeshRenderer = smr;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.normalOffset = normalOffset;
            shape.useMeshColors = false;

            go.SetActive(true);
            ps.Play();
            return new FogEmitter { Go = go, Ps = ps, Anchor = MeshAnchorName, Layer = FogLayerKind.Inner, VertexAlpha = layer.VertexAlpha };
        }

        /// <summary>
        /// Adds and configures the ParticleSystem shared by both emitter kinds (everything except the shape). Called while the
        /// GameObject is still inactive, so the simulation space is set before the system ever plays.
        /// </summary>
        private static ParticleSystem ConfigureSystem(GameObject go, Color color, FogLayer layer, FogSettings s, bool materialHasColor)
        {
            var rate = layer.Rate;
            var size = layer.Size;
            var vertexAlpha = layer.VertexAlpha;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            // Follow: local space, particles move with the emitter (bone / body). Trail: world space, the emitter (still parented to the
            // bone or the body) spawns new particles at the body, existing ones stay where they are, so moving leaves a trail.
            main.simulationSpace = layer.Trail == FogTrailMode.Trail ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(layer.Lifetime * 0.8f, layer.Lifetime * 1.2f);
            main.startSpeed = float.IsNaN(layer.Speed) ? s.Speed : layer.Speed;
            main.startSize = new ParticleSystem.MinMaxCurve(size * layer.SizeMinFactor, size * layer.SizeMaxFactor);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            // With a material colour the tint lives there (it can follow the environment); else in the particle colour.
            main.startColor = materialHasColor ? new Color(1f, 1f, 1f, vertexAlpha) : new Color(color.r, color.g, color.b, vertexAlpha);
            main.gravityModifier = 0f;
            main.maxParticles = layer.MaxParticles > 0 ? layer.MaxParticles : FogSettings.MaxParticles(rate, layer.Lifetime);

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = Mathf.Max(0f, rate);
            // Per metre moved; Unity only applies it in world space (FogSettings.OuterEffectiveRateDistance is 0 otherwise).
            emission.rateOverDistance = Mathf.Max(0f, layer.RateDistance);

            if (layer.Grow > 1.0001f)
            {
                // Linear growth from the start size to Grow x start size (round M: the volume's 3.2 m sprites end at 4.5 m).
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(layer.Grow, new AnimationCurve(new Keyframe(0f, 1f / layer.Grow), new Keyframe(1f, 1f)));
            }

            var vel = ps.velocityOverLifetime;
            var drift = layer.Volume ? 0f : s.Drift;   // the inner Drift is not the volume's (it drifts by its start speed)
            vel.enabled = Mathf.Abs(drift) > 0.0001f || Mathf.Abs(layer.Orbital) > 0.0001f;
            // Drift is vertical regardless of bone rotation (world). The follow volume (round R) orbits in local space; its emitter
            // keeps world rotation (position-only follower) and has no drift, so local y is world up there too.
            vel.space = layer.Volume && layer.Trail == FogTrailMode.Follow ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0f);
            vel.y = new ParticleSystem.MinMaxCurve(drift);
            vel.z = new ParticleSystem.MinMaxCurve(0f);
            // Outer ring turn (round J ruling): orbital velocity around the system's centre (the follower, y = player up), all three
            // orbital curves in Constant mode like the linear ones.
            vel.orbitalX = new ParticleSystem.MinMaxCurve(0f);
            vel.orbitalY = new ParticleSystem.MinMaxCurve(layer.Orbital);
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                layer.Volume
                    ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, FogSettings.OuterVolumeFadeIn), new GradientAlphaKey(0f, 1f) }
                    : new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = layer.Material;
            psr.renderMode = layer.Horizontal ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;
            // Viewport fraction; the default 0.5 shrinks a 3 m volume sprite when the camera is inside or close to the fog.
            if (layer.Volume) psr.maxParticleSize = 10f;
            psr.shadowCastingMode = ShadowCastingMode.Off;
            psr.receiveShadows = false;
            psr.lightProbeUsage = LightProbeUsage.Off;
            return ps;
        }

        /// <summary>Live particle count per emitter of a player's veil (tuning window readout). Empty when the player has no veil.</summary>
        public void CollectParticleCounts(Player p, List<KeyValuePair<string, int>> into)
        {
            into.Clear();
            if (p == null || !_snapshots.TryGetValue(p, out var snap)) return;
            foreach (var e in snap.Fog)
                if (e.Ps != null) into.Add(new KeyValuePair<string, int>(e.Layer == FogLayerKind.Inner ? e.Anchor : $"{e.Anchor} ({e.Layer.ToString().ToLowerInvariant()})", e.Ps.particleCount));
        }

        public bool HasVeil(Player p) => p != null && _snapshots.ContainsKey(p);

#if DEBUG
        // ---------- diagnostics (Debug only) ----------

        private static string _lastSpawnLog;

        /// <summary>
        /// auto fog dump (plan 4 fix round 2, ruling 6): Time.time at which <see cref="VeilController"/> logs the fog dump of the
        /// local veil once (same output as ip_fog dump), so every test log carries the ring's particle counts, renderer visibility,
        /// bounds and camera distances. Negative = nothing pending. Every local build re-arms it, so a burst of rebuilds (tuning
        /// window) produces one dump, 2.5 s after the last build.
        /// </summary>
        public static float AutoDumpAt = -1f;
        public static float AutoDumpBuiltAt;
        public const float AutoDumpDelay = 2.5f;
        private static readonly ParticleSystem.Particle[] ParticleBuf = new ParticleSystem.Particle[FogSettings.ParticleHardCap];

        /// <summary>
        /// One line per emitter of the local veil right after it was built (name, layer, anchor, rate, size, alpha), so the log of
        /// a test round carries the facts. Identical consecutive builds are logged once (the tuning window rebuilds often).
        /// </summary>
        private static void LogSpawn(Snapshot snap)
        {
            var s = Fog[snap.Tier];
            var lines = new List<string> { $"veil fog spawned T{snap.Tier} (local, body {snap.Effective}): {snap.Fog.Count} emitters" };
            foreach (var e in snap.Fog)
            {
                float rate, size, alpha;
                switch (e.Layer)
                {
                    case FogLayerKind.Outer:
                        rate = e.Ps != null ? e.Ps.emission.rateOverTime.constant : s.OuterRate;
                        size = e.Go != null && e.Go.name == OuterFollowObjectName ? s.OuterFollowSize : s.OuterSize;
                        alpha = s.OuterAlpha;
                        break;
                    case FogLayerKind.Ground when e.Anchor == GroundUpperAnchorName: rate = s.GroundUpperRate; size = s.GroundSize; alpha = s.GroundUpperAlpha; break;
                    case FogLayerKind.Ground: rate = s.GroundRate; size = s.GroundSize; alpha = s.GroundAlpha; break;
                    default: rate = e.Ps != null ? e.Ps.emission.rateOverTime.constant : s.Rate; size = s.Size; alpha = s.Alpha; break;
                }
                var upperGround = e.Anchor == GroundUpperAnchorName;
                var extra = e.Layer == FogLayerKind.Ground ? $" + {F(upperGround ? s.GroundUpperRateDistance : s.GroundRateDistance)}/m, grow x{F(s.GroundGrow)}, " +
                                                             $"height {F(upperGround ? s.GroundUpperHeight : s.GroundHeight)} m, max {(e.Ps != null ? e.Ps.main.maxParticles : 0)}"
                          : e.Layer == FogLayerKind.Outer ? $", {s.OuterShape} radius {(e.Ps != null ? F(e.Ps.shape.radius) : "-")} m, height {F(s.OuterSpreadY)} x r, offset y {F(s.OuterOffsetY)} m, rotation {F(s.OuterRotation)} deg/s, " +
                                                            $"space {(e.Ps != null ? e.Ps.main.simulationSpace.ToString() : "-")}, {(s.OuterHorizontal ? "horizontal" : "camera-facing")}, " +
                                                            $"+ {(e.Ps != null ? F(e.Ps.emission.rateOverDistance.constant) : "-")}/m, burst {e.Burst} (follow share {F(s.OuterFollowShareEffective)}, " +
                                                            $"follow burst fraction {F(s.OuterFollowBurstFractionEffective)}, fade-in {F(FogSettings.OuterVolumeFadeIn)}), " +
                                                            $"follower {FollowerMode(e.Go)}, alive at spawn {(e.Ps != null ? e.Ps.particleCount : 0)}" : "";
                lines.Add($"  {(e.Go != null ? e.Go.name : "<destroyed>")} [{e.Layer}] {e.Anchor}: rate {F(rate)}/s{extra}, size {F(size)} m, alpha {F(alpha)} (vertex {F(e.VertexAlpha)}), " +
                          $"max {(e.Ps != null ? e.Ps.main.maxParticles : 0)}");
            }
            AddMaterialLines(snap, s, lines);
            var text = string.Join("\n", lines);
            if (text == _lastSpawnLog) return;
            _lastSpawnLog = text;
            foreach (var l in lines) Plugin.Log.LogInfo(l);
        }

        /// <summary>
        /// Round M ruling 2d: the final values of every fog material of the veil (one line per material, with the layers using
        /// it): _Color, _EmissionColor, queue, keywords and any blend/alpha property the shader declares
        /// (FogMaterialCheck.BlendProperties; logged, never changed). A _Color brighter than the applied colour or a non-black
        /// _EmissionColor at Emission 0 is logged as a warning.
        /// </summary>
        private static void AddMaterialLines(Snapshot snap, FogSettings s, List<string> lines)
        {
            var users = new Dictionary<Material, List<string>>();
            foreach (var e in snap.Fog)
            {
                var psr = e.Go != null ? e.Go.GetComponent<ParticleSystemRenderer>() : null;
                var m = psr != null ? psr.sharedMaterial : null;
                if (m == null) continue;
                if (!users.TryGetValue(m, out var list)) users[m] = list = new List<string>();
                var tag = e.Layer == FogLayerKind.Inner ? "Inner" : e.Layer == FogLayerKind.Outer ? "Outer" : e.Anchor;
                if (!list.Contains(tag)) list.Add(tag);
            }
            var applied = new FogRgb(snap.AppliedColor.r, snap.AppliedColor.g, snap.AppliedColor.b);
            foreach (var kv in users)
            {
                var m = kv.Key;
                var blend = new List<string>();
                foreach (var name in FogMaterialCheck.BlendProperties)
                    if (m.HasProperty(name)) blend.Add($"{name}={F(m.GetFloat(name))}");
                var c = m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white;
                var em = m.HasProperty(EmissionColorId) ? m.GetColor(EmissionColorId) : Color.black;
                var problems = m.HasProperty(ColorId)
                    ? FogMaterialCheck.Problems(applied, new FogRgb(c.r, c.g, c.b), new FogRgb(em.r, em.g, em.b), s.Emission)
                    : new List<string>();
                lines.Add($"  material '{m.name}' ({string.Join("+", kv.Value)}): shader '{(m.shader != null ? m.shader.name : "-")}' queue {m.renderQueue}, " +
                          $"_Color {MatColor(m, ColorId)}, _EmissionColor {MatColor(m, EmissionColorId)}, applied colour {FormatRgb(snap.AppliedColor)}, " +
                          $"blend [{(blend.Count > 0 ? string.Join(" ", blend) : "no blend properties declared")}], keywords [{string.Join(" ", m.shaderKeywords)}]" +
                          (problems.Count > 0 ? $"; WARNING {string.Join("; ", problems)}" : "; check ok"));
            }
        }

        private static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        private static string V(Vector3 v) => $"({F(v.x)}, {F(v.y)}, {F(v.z)})";
        private static string C(Color c) => $"({F(c.r)}, {F(c.g)}, {F(c.b)}, {F(c.a)})";

        private static string Curve(ParticleSystem.MinMaxCurve c)
        {
            switch (c.mode)
            {
                case ParticleSystemCurveMode.Constant: return F(c.constant);
                case ParticleSystemCurveMode.TwoConstants: return $"{F(c.constantMin)}..{F(c.constantMax)}";
                default: return $"{c.mode} x{F(c.curveMultiplier)}";
            }
        }

        /// <summary>Round R: how the emitter follows its bone, for the spawn log and the fog dump.</summary>
        private static string FollowerMode(GameObject go)
        {
            var f = go != null ? go.GetComponent<FogAnchorFollower>() : null;
            return f == null ? "none" : f.Ground ? "ground (position, player rotation)" : f.PositionOnly ? "position only (world rotation)" : "position + player rotation";
        }

        private static float Curve0(ParticleSystem.MinMaxCurve c) => c.mode == ParticleSystemCurveMode.Constant ? c.constant : c.constantMax;

        private static string MatFloat(Material m, int id) => m != null && m.HasProperty(id) ? F(m.GetFloat(id)) : "-";
        private static string MatColor(Material m, int id) => m != null && m.HasProperty(id) ? C(m.GetColor(id)) : "-";

        /// <summary>
        /// Every fact about every fog emitter of a player's veil (ip_fog dump, ip_state): object, layer, anchor, play state, counts,
        /// emission, sizes, spaces, renderer, material values, bounds, positions relative to the player and the camera, and
        /// statistics of the live particles (current alpha and size, distance to the camera). Empty when the player has no veil.
        /// </summary>
        public void DumpFog(Player p, List<string> into)
        {
            into.Clear();
            if (p == null || !_snapshots.TryGetValue(p, out var snap)) return;
            var gc = GameCamera.instance;
            var cam = gc != null ? gc.m_camera : null;
            var camPos = cam != null ? cam.transform.position : Vector3.zero;
            var playerPos = p.transform.position;
            into.Add($"veil T{snap.Tier} on {p.GetPlayerName()}: body {snap.Requested} (effective {snap.Effective}), version {snap.Version}/{Version}, " +
                     $"age {F(Time.time - snap.BuiltAt)} s, fog wanted {snap.FogWanted}, {snap.Fog.Count} emitters, {snap.FogMaterials.Count} fog materials, alpha mode {AlphaMode}");
            into.Add($"  player {V(playerPos)}, camera {(cam != null ? V(camPos) : "-")} (distance {(gc != null ? F(gc.m_distance) : "-")} m, fov {(cam != null ? F(cam.fieldOfView) : "-")}, " +
                     $"depth mode {(cam != null ? cam.depthTextureMode.ToString() : "-")}, rendering path {(cam != null ? cam.actualRenderingPath.ToString() : "-")})");
            foreach (var e in snap.Fog)
            {
                if (e.Go == null || e.Ps == null) { into.Add($"  [{e.Layer}] {e.Anchor}: destroyed"); continue; }
                try
                {
                    var ps = e.Ps;
                    var main = ps.main;
                    var em = ps.emission;
                    var shape = ps.shape;
                    var psr = e.Go.GetComponent<ParticleSystemRenderer>();
                    var mat = psr != null ? psr.sharedMaterial : null;
                    var pos = e.Go.transform.position;
                    into.Add($"  {e.Go.name} [{e.Layer}] {e.Anchor}: active {e.Go.activeInHierarchy}, playing {ps.isPlaying}, emitting {ps.isEmitting}, particles {ps.particleCount}/{main.maxParticles}, " +
                             $"rate {Curve(em.rateOverTime)}/s + {Curve(em.rateOverDistance)}/m (emission {em.enabled}), startSize {Curve(main.startSize)}, lifetime {Curve(main.startLifetime)}, " +
                             $"speed {Curve(main.startSpeed)}, space {main.simulationSpace}, scaling {main.scalingMode}, shape {shape.shapeType} r {F(shape.radius)} scale {V(shape.scale)}, " +
                             $"startColor {C(main.startColor.color)} (vertex alpha {F(e.VertexAlpha)}), size over life {ps.sizeOverLifetime.enabled}, velocity {ps.velocityOverLifetime.enabled}" +
                             (e.Layer == FogLayerKind.Outer
                                 ? $"; outer shape {Fog[snap.Tier].OuterShape}: {shape.shapeType} r {F(shape.radius)} thickness {F(shape.radiusThickness)} rotation {V(shape.rotation)} jitter {F(shape.randomPositionAmount)} " +
                                   $"random direction {F(shape.randomDirectionAmount)}, " +
                                   $"orbital {Curve(ps.velocityOverLifetime.orbitalY)} rad/s ({F(Curve0(ps.velocityOverLifetime.orbitalY) * Mathf.Rad2Deg)} deg/s, velocity space {ps.velocityOverLifetime.space}), " +
                                   $"follower {FollowerMode(e.Go)}, emitter rotation {V(e.Go.transform.rotation.eulerAngles)}"
                                 : ""));
                    into.Add(psr == null ? "    renderer: none" :
                             $"    renderer enabled {psr.enabled}, visible {psr.isVisible}, mode {psr.renderMode}, sortingFudge {F(psr.sortingFudge)}, maxParticleSize {F(psr.maxParticleSize)}, " +
                             $"minParticleSize {F(psr.minParticleSize)}, layer {LayerMask.LayerToName(e.Go.layer)} ({e.Go.layer}), culling mask has layer {(cam != null ? ((cam.cullingMask & (1 << e.Go.layer)) != 0).ToString() : "-")}; " +
                             $"material '{(mat != null ? mat.name : "-")}' shader '{(mat != null && mat.shader != null ? mat.shader.name : "-")}' queue {(mat != null ? mat.renderQueue.ToString() : "-")}, " +
                             $"_Color {MatColor(mat, ColorId)}, _EmissionColor {MatColor(mat, EmissionColorId)}, _ZFadeDistance {MatFloat(mat, ZFadeDistanceId)}, " +
                             $"_CameraFadeDistanceMin/Max {MatFloat(mat, CameraFadeMinId)}/{MatFloat(mat, CameraFadeMaxId)}, _CameraYFadeDistance {MatFloat(mat, CameraYFadeId)}, _Billboard {MatFloat(mat, BillboardId)}, " +
                             $"_LightNormalFactor {MatFloat(mat, LightNormalFactorId)}, _BumpScale {MatFloat(mat, BumpScaleId)}, _SkyMask {MatFloat(mat, SkyMaskId)}, " +
                             $"keywords [{(mat != null ? string.Join(" ", mat.shaderKeywords) : "-")}]");
                    var line = $"    bounds centre {(psr != null ? V(psr.bounds.center) : "-")} size {(psr != null ? V(psr.bounds.size) : "-")}; emitter {V(pos)} = player + {V(pos - playerPos)}" +
                               (cam != null ? $", {F(Vector3.Distance(camPos, pos))} m from the camera" : "");
                    var n = ps.GetParticles(ParticleBuf);
                    if (n > 0)
                    {
                        float alphaSum = 0f, sizeSum = 0f, minCam = float.MaxValue, maxCam = 0f, ySum = 0f;
                        var local = main.simulationSpace == ParticleSystemSimulationSpace.Local;
                        for (var i = 0; i < n; i++)
                        {
                            var pt = ParticleBuf[i];
                            alphaSum += pt.GetCurrentColor(ps).a / 255f;
                            sizeSum += pt.GetCurrentSize(ps);
                            var wp = local ? e.Go.transform.TransformPoint(pt.position) : pt.position;
                            ySum += wp.y - playerPos.y;
                            if (cam == null) continue;
                            var d = Vector3.Distance(camPos, wp);
                            if (d < minCam) minCam = d;
                            if (d > maxCam) maxCam = d;
                        }
                        var matAlpha = mat != null && mat.HasProperty(ColorId) ? mat.GetColor(ColorId).a : 1f;
                        line += $"; live {n}: avg vertex alpha {F(alphaSum / n)} (x material {F(matAlpha)} = {F(alphaSum / n * matAlpha)}), avg size {F(sizeSum / n)} m, " +
                                $"avg height above the player {F(ySum / n)} m" + (cam != null ? $", camera distance {F(minCam)}..{F(maxCam)} m" : "");
                    }
                    else line += "; no live particles";
                    into.Add(line);

                }
                catch (Exception ex)
                {
                    into.Add($"  [{e.Layer}] {e.Anchor}: dump failed: {ex.Message}");   // one bad emitter must not hide the others
                }
            }
        }

        /// <summary>
        /// ip_fog strays: every ParticleSystem in the loaded scenes that is ours by object name (ip_fog*) or by renderer material
        /// (ip_fog_mat*, ip_distortion_*), with its parent chain (3 levels), active state, particle count, emitting state, age (when
        /// it has a follower) and whether a current veil snapshot tracks it. Emitters destroyed this frame are still listed
        /// (inactive): Object.Destroy is deferred to the end of the frame.
        /// </summary>
        public void ScanStrays(List<string> into)
        {
            into.Clear();
            var tracked = new HashSet<GameObject>();
            foreach (var kv in _snapshots)
                foreach (var e in kv.Value.Fog)
                    if (e.Go != null) tracked.Add(e.Go);
            int ours = 0, untracked = 0, visible = 0;
            foreach (var ps in Resources.FindObjectsOfTypeAll<ParticleSystem>())
            {
                if (ps == null) continue;
                var go = ps.gameObject;
                if (!go.scene.IsValid()) continue;   // prefabs and other assets
                var psr = go.GetComponent<ParticleSystemRenderer>();
                var mat = psr != null ? psr.sharedMaterial : null;
                var matName = mat != null ? mat.name : "";
                var byMaterial = matName.StartsWith("ip_fog_mat", StringComparison.Ordinal) || matName.StartsWith("ip_distortion_", StringComparison.Ordinal);
                if (!IsOurs(go) && !byMaterial) continue;
                ours++;
                var isTracked = tracked.Contains(go);
                if (!isTracked) untracked++;
                if (go.activeInHierarchy && psr != null && psr.enabled && ps.particleCount > 0) visible++;
                var follower = go.GetComponent<FogAnchorFollower>();
                into.Add($"  {go.name} under {ParentChain(go.transform, 3)}: activeSelf {go.activeSelf}, activeInHierarchy {go.activeInHierarchy}, " +
                         $"particles {ps.particleCount}, emitting {ps.isEmitting}, playing {ps.isPlaying}, age {(follower != null ? F(Time.time - follower.SpawnedAt) + " s" : "-")}, " +
                         $"renderer {(psr != null ? psr.enabled.ToString() : "none")}, material '{(mat != null ? matName : "-")}', {(isTracked ? "tracked" : "NOT TRACKED")}");
            }
            into.Insert(0, $"fog strays at {F(Time.time)} s: {ours} veil particle systems in the scene ({visible} active with live particles), {untracked} not tracked by a veil; " +
                           $"{_snapshots.Count} veils tracking {tracked.Count} emitters");
        }

        private static string ParentChain(Transform t, int levels)
        {
            var parts = new List<string>();
            for (var p = t.parent; p != null && parts.Count < levels; p = p.parent) parts.Add(p.name);
            return parts.Count == 0 ? "(scene root)" : string.Join(" < ", parts);
        }
#endif

        /// <summary>Vanilla material name and the prefabs that carry it, per selectable fog material (research doc 2026-10-01, section 4).</summary>
        private static readonly Dictionary<string, KeyValuePair<string, string[]>> FogMaterialSources = new Dictionary<string, KeyValuePair<string, string[]>>
        {
            ["ghost_smoke"] = new KeyValuePair<string, string[]>("ghost_smoke", new[] { "Ghost_Void" }),
            ["wraith_smoke"] = new KeyValuePair<string, string[]>("wraith_smoke", new[] { "Wraith", "vfx_ghost_spawn" }),
            ["slowwispysmoke"] = new KeyValuePair<string, string[]>("slowwispysmoke_gradient_alphablend", new[] { "Ghost" }),
        };
        private static readonly Dictionary<string, Material> _fogMaterials = new Dictionary<string, Material>();
        private static readonly HashSet<string> _fogMaterialMissing = new HashSet<string>();

        /// <summary>
        /// The tier's selected vanilla fog material (prefab asset, read-only; every veil uses a new Material copy). An unknown name or a
        /// material that cannot be found falls back to swamp_mist with one log line.
        /// </summary>
        private static Material FogSourceMaterial(string name)
        {
            name = FogSettings.NormalizeFogMaterial(name) ?? FogSettings.DefaultFogMaterial;
            if (name == "soft") return SoftFogMaterial();
            if (!FogMaterialSources.TryGetValue(name, out var source)) return SwampMistMaterial();
            if (_fogMaterials.TryGetValue(name, out var cached) && cached != null) return cached;
            if (ZNetScene.instance == null || _fogMaterialMissing.Contains(name)) return SwampMistMaterial();
            var found = FindMaterialOnPrefabs(source.Value, m => MaterialNameIs(m, source.Key), particlesOnly: true, out var from)
                        ?? FindLoadedMaterial(m => MaterialNameIs(m, source.Key));
            if (found == null)
            {
                _fogMaterialMissing.Add(name);
                Plugin.Log.LogWarning($"veil fog: material '{source.Key}' not found on {string.Join("/", source.Value)} or among loaded materials; using swamp_mist");
                return SwampMistMaterial();
            }
            _fogMaterials[name] = found;
            Plugin.Log.LogInfo($"veil fog: borrowed material '{found.name}' (shader '{found.shader?.name}') from {from ?? "loaded materials"} for FogMaterial {name}");
            LogProperties("fog " + name, found);
            return found;
        }

        /// <summary>Unity appends " (Instance)" to runtime copies; compare the asset name only.</summary>
        private static bool MaterialNameIs(Material m, string name)
        {
            if (m == null) return false;
            var n = m.name;
            const string suffix = " (Instance)";
            if (n.EndsWith(suffix, StringComparison.Ordinal)) n = n.Substring(0, n.Length - suffix.Length);
            return string.Equals(n, name, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>First shared material on the named ZNetScene prefabs that matches; <paramref name="from"/> names prefab and renderer.</summary>
        private static Material FindMaterialOnPrefabs(IEnumerable<string> prefabs, Func<Material, bool> match, bool particlesOnly, out string from)
        {
            from = null;
            var scene = ZNetScene.instance;
            if (scene == null) return null;
            foreach (var name in prefabs)
            {
                var prefab = scene.GetPrefab(name);
                if (prefab == null) continue;
                var renderers = particlesOnly ? (Renderer[])prefab.GetComponentsInChildren<ParticleSystemRenderer>(true) : prefab.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || !match(m)) continue;
                    from = $"{name}/{r.name}";
                    return m;
                }
            }
            return null;
        }

        /// <summary>Last resort: scans every loaded material once per lookup (callers cache the result or the miss).</summary>
        private static Material FindLoadedMaterial(Func<Material, bool> match)
        {
            foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
                if (m != null && match(m)) return m;
            return null;
        }

        private static Material _fogMaterial;

        /// <summary>Borrowed vanilla soft-particle material (shared, never mutated); falls back to Particles/Standard Unlit with a generated soft sprite.</summary>
        private static Material SwampMistMaterial()
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

        private static Material _softFogMaterial;

        /// <summary>
        /// FogMaterial "soft" (plan 4 fix round 2, ruling 4): a copy of the borrowed swamp_mist material (same Custom/LitParticles
        /// shader, same lighting properties) whose _MainTex, the brownish dust02, is replaced by a generated 64x64 white radial
        /// soft-falloff sprite, and whose _NormalTex (wave-normal) is replaced by a flat normal texture. _Color stays the configured
        /// colour (MakeFogMaterial), so only the dust tint goes. Shared and never mutated; every veil makes its own copy. With the
        /// Particles/Standard Unlit fallback (no swamp_mist) the fallback is returned: it already uses the soft sprite.
        /// </summary>
        private static Material SoftFogMaterial()
        {
            if (_softFogMaterial != null) return _softFogMaterial;
            var swamp = SwampMistMaterial();
            if (swamp == null) return null;   // too early; retried on the next build
            if (swamp.name == "ip_fog_fallback") return swamp;
            var m = new Material(swamp) { name = "ip_fog_soft" };
            var oldMain = m.HasProperty(MainTexId) ? m.GetTexture(MainTexId) : null;
            var oldNormal = m.HasProperty(NormalTexId) ? m.GetTexture(NormalTexId) : null;
            if (m.HasProperty(MainTexId)) m.SetTexture(MainTexId, SoftSprite());
            if (m.HasProperty(NormalTexId)) m.SetTexture(NormalTexId, FlatNormal());
            _softFogMaterial = m;
            Plugin.Log.LogInfo($"veil fog: soft material from '{swamp.name}' (shader '{m.shader?.name}'): _MainTex {(oldMain != null ? oldMain.name : "-")} -> " +
                               $"{(m.HasProperty(MainTexId) ? $"ip_fog_sprite ({FogSprite.Size}x{FogSprite.Size} white, gaussian alpha, centre {FloatList.Format(FogSprite.CentreAlpha)}, noise +-{FloatList.Format(FogSprite.NoiseAmount * 100f)} %)" : "not declared, unchanged")}, _NormalTex {(oldNormal != null ? oldNormal.name : "-")} -> " +
                               $"{(m.HasProperty(NormalTexId) ? "ip_fog_flat_normal" : "not declared, unchanged")}");
            LogProperties("fog soft", m);
            return m;
        }

        /// <summary>4x4 flat tangent-space normal (0.5, 0.5, 1, 1): decodes to (0, 0, 1) as RGB and as AG (DXT5nm) normal map.</summary>
        private static Texture2D FlatNormal()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false, true) { name = "ip_fog_flat_normal", wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[16];
            for (var i = 0; i < px.Length; i++) px[i] = new Color32(128, 128, 255, 255);
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>
        /// The soft fog sprite (round L ruling 5): white, alpha from <see cref="FogSprite.Alpha"/> = a gaussian falloff
        /// (centre <see cref="FogSprite.CentreAlpha"/>, exactly 0 at the edge) times 1 +- 10 % smooth value noise, so overlapping
        /// particles blend into fog instead of showing stacked disc edges. Replaces the round 2 smoothstep cone (hard-ish rim).
        /// </summary>
        private static Texture2D SoftSprite()
        {
            const int size = FogSprite.Size;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "ip_fog_sprite" };
            var px = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                px[y * size + x] = new Color(1f, 1f, 1f, FogSprite.Alpha(x, y, size));
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

        // ---------- Shadow (copy of the vanilla ShadowPerson material) ----------

        private const string ShadowPrefabName = "ShadowPerson";
        private const string ShadowMaterialName = "ShadowPerson 1";
        private static Material _shadowSource;
        private static Material _shadowInstance;
        private static bool _shadowSearched;

        /// <summary>
        /// One shared copy of "ShadowPerson 1" (Standard, transparent premultiplied) tinted with ShadowColor. Located on the
        /// ShadowPerson prefab, else on any SP_* item prefab (they reuse the material), else among loaded materials.
        /// </summary>
        private static Material ShadowInstance()
        {
            if (_shadowInstance != null) return _shadowInstance;
            if (_shadowSource == null)
            {
                if (_shadowSearched || ZNetScene.instance == null) return null;
                _shadowSearched = true;
                string from;
                _shadowSource = FindMaterialOnPrefabs(new[] { ShadowPrefabName }, m => MaterialNameIs(m, ShadowMaterialName), false, out from)
                                ?? FindMaterialOnPrefabs(SpPrefabNames(), m => MaterialNameIs(m, ShadowMaterialName), false, out from)
                                ?? FindMaterialOnPrefabs(new[] { ShadowPrefabName }, m => m.name.StartsWith("ShadowPerson", StringComparison.OrdinalIgnoreCase), false, out from)
                                ?? FindLoadedMaterial(m => MaterialNameIs(m, ShadowMaterialName));
                if (_shadowSource == null) return null;
                Plugin.Log.LogInfo($"veil shadow: borrowed material '{_shadowSource.name}' from {from ?? "loaded materials"}");
                LogProperties("shadow", _shadowSource);
            }
            _shadowInstance = new Material(_shadowSource) { name = "ip_shadow" };
            ApplyShadowLook(_shadowInstance);
            return _shadowInstance;
        }

        private static IEnumerable<string> SpPrefabNames()
        {
            var scene = ZNetScene.instance;
            if (scene == null) yield break;
            foreach (var go in scene.m_prefabs)
                if (go != null && go.name.StartsWith("SP_", StringComparison.Ordinal)) yield return go.name;
        }

        private static void ApplyShadowLook(Material m)
        {
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, ShadowColor);
        }

        // ---------- Spirit (Custom/Fallen Warrior per original material) ----------

        private const string SpiritShaderName = "Custom/Fallen Warrior";
        private static readonly string[] SpiritPrefabNames = { "FallenWarrior", "Wolf_spiritcaller", "Boar_spiritcaller", "Moose_spiritcaller", "Bjorn_spiritcaller" };
        /// <summary>Texture slots shared by Custom/Player (and VisEquipment) and Custom/Fallen Warrior; copied so armour still reads.</summary>
        private static readonly string[] SpiritTextureSlots = { "_MainTex", "_SkinBumpMap", "_ChestTex", "_ChestBumpMap", "_LegsTex", "_LegsBumpMap" };
        private static readonly int SkinColorId = Shader.PropertyToID("_SkinColor");
        private static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
        private static Shader _spiritShader;
        private static Material _spiritSource;
        private static bool _spiritSearched;

        /// <summary>The Custom/Fallen Warrior shader, taken from a FallenWarrior / spiritcaller prefab material, else Shader.Find.</summary>
        private static Shader SpiritShader()
        {
            if (_spiritShader != null) return _spiritShader;
            if (_spiritSearched || ZNetScene.instance == null) return null;
            _spiritSearched = true;
            _spiritSource = FindMaterialOnPrefabs(SpiritPrefabNames, m => m.shader != null && m.shader.name == SpiritShaderName, false, out var from);
            if (_spiritSource != null)
            {
                _spiritShader = _spiritSource.shader;
                Plugin.Log.LogInfo($"veil spirit: shader '{SpiritShaderName}' from material '{_spiritSource.name}' on {from}");
                LogProperties("spirit", _spiritSource);
            }
            else
            {
                _spiritShader = Shader.Find(SpiritShaderName);
                if (_spiritShader != null) Plugin.Log.LogInfo($"veil spirit: shader '{SpiritShaderName}' via Shader.Find (no source prefab found)");
            }
            return _spiritShader;
        }

        /// <summary>Per-veil Spirit copy of one original material (shared between renderers that share the original).</summary>
        private static Material SpiritFor(Snapshot snap, Material original)
        {
            if (original != null && snap.SpiritByOriginal.TryGetValue(original, out var existing) && existing != null) return existing;
            var m = new Material(SpiritShader()) { name = "ip_spirit" };
            if (original != null)
            {
                foreach (var slot in SpiritTextureSlots)
                {
                    if (!original.HasProperty(slot) || !m.HasProperty(slot)) continue;
                    m.SetTexture(slot, original.GetTexture(slot));
                    m.SetTextureScale(slot, original.GetTextureScale(slot));
                    m.SetTextureOffset(slot, original.GetTextureOffset(slot));
                }
                if (original.HasProperty(SkinColorId) && m.HasProperty(SkinColorId)) m.SetColor(SkinColorId, original.GetColor(SkinColorId));
                if (original.HasProperty(BumpScaleId) && m.HasProperty(BumpScaleId)) m.SetFloat(BumpScaleId, original.GetFloat(BumpScaleId));
            }
            // Hair, beards and capes are alpha-tested: keep their cutoff; otherwise use the vanilla Fallen Warrior value.
            if (m.HasProperty(CutoffId))
            {
                if (original != null && original.HasProperty(CutoffId)) m.SetFloat(CutoffId, original.GetFloat(CutoffId));
                else if (_spiritSource != null && _spiritSource.HasProperty(CutoffId)) m.SetFloat(CutoffId, _spiritSource.GetFloat(CutoffId));
            }
            ApplySpiritLook(m);
            if (original != null) snap.SpiritByOriginal[original] = m;
            snap.SpiritOwned.Add(m);
            return m;
        }

        private static void ApplySpiritLook(Material m)
        {
            var k = SpiritStrength;
            if (m.HasProperty(TintColorId)) m.SetColor(TintColorId, new Color(SpiritColor.r * k, SpiritColor.g * k, SpiritColor.b * k, 1f));
        }

        // ---------- Distortion ----------

        private const string ShieldPrefabName = "vfx_StaffShield";
        private const string ShieldMaterialName = "staff_shield_shard";
        private static bool _shieldSearched;
        private static Texture _shieldNormal;
        private static float _shieldNormalScale = float.NaN;
        /// <summary>_WaveVel of staff_shield_shard; NaN until found (DistortionWave &lt; 0 uses it).</summary>
        public static float BorrowedDistortionWave { get; private set; } = float.NaN;

        /// <summary>Reads _NormalTex/_NormalScale/_WaveVel once from staff_shield_shard (vfx_StaffShield, else loaded materials). Read-only.</summary>
        private static void FindShieldShard()
        {
            if (_shieldSearched || ZNetScene.instance == null) return;
            _shieldSearched = true;
            var m = FindMaterialOnPrefabs(new[] { ShieldPrefabName }, x => MaterialNameIs(x, ShieldMaterialName), false, out var from)
                    ?? FindLoadedMaterial(x => MaterialNameIs(x, ShieldMaterialName));
            if (m == null)
            {
                Plugin.Log.LogWarning($"veil distortion: material '{ShieldMaterialName}' not found ({ShieldPrefabName}, loaded materials); no ripple normal map");
                return;
            }
            if (m.HasProperty(NormalTexId)) _shieldNormal = m.GetTexture(NormalTexId);
            if (m.HasProperty(NormalScaleId)) _shieldNormalScale = m.GetFloat(NormalScaleId);
            if (m.HasProperty(WaveVelId)) BorrowedDistortionWave = m.GetFloat(WaveVelId);
            Plugin.Log.LogInfo($"veil distortion: ripple from '{m.name}' on {from ?? "loaded materials"}: _NormalTex {(_shieldNormal != null ? _shieldNormal.name : "-")}, " +
                               $"_NormalScale {_shieldNormalScale}, _WaveVel {BorrowedDistortionWave}");
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
            FindShieldShard();
            if (_shieldNormal != null && instance.HasProperty(NormalTexId)) instance.SetTexture(NormalTexId, _shieldNormal);
            if (!float.IsNaN(_shieldNormalScale) && _shieldNormal != null && instance.HasProperty(NormalScaleId)) instance.SetFloat(NormalScaleId, _shieldNormalScale);
            ApplyDistortionLook(instance, tier);
            _distortionInstances[tier] = instance;
            return instance;
        }

        private static void ApplyDistortionLook(Material m, int tier)
        {
            var s = Fog[tier];
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, new Color(s.DR, s.DG, s.DB, s.DA));
            if (m.HasProperty(RefractionId)) m.SetFloat(RefractionId, s.DistortionStrength);
            var wave = s.DistortionWave >= 0f ? s.DistortionWave : BorrowedDistortionWave;
            if (!float.IsNaN(wave) && m.HasProperty(WaveVelId)) m.SetFloat(WaveVelId, wave);
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
            List<string> vanillaParticles = null;   // vanilla particle renderers hidden by a swap mode and shown again (summary line)
            try
            {
                foreach (var kv in snap.Cutoffs) if (kv.Key != null) kv.Key.SetFloat(CutoffId, kv.Value);
                foreach (var kv in snap.Colors) if (kv.Key != null) kv.Key.SetColor(ColorId, kv.Value);
                var body = p.m_visEquipment != null ? p.m_visEquipment.m_bodyModel : null;
                foreach (var kv in snap.SharedMaterials)
                {
                    if (kv.Key == null) continue;   // destroyed (old armour piece): nothing to restore
                    try
                    {
                        if (kv.Key == body && snap.Effective == BodyVeilMode.Spirit) CarryBodyChanges(kv.Key, kv.Value, snap);
                        // Vanilla replaced the materials since the swap (they no longer hold one of ours): keep vanilla's, restoring
                        // the snapshot would bring back stale ones.
                        if (!HoldsOurMaterial(kv.Key)) continue;
                        kv.Key.sharedMaterials = kv.Value;
                    }
                    catch (Exception e)
                    {
                        // One failing renderer must not leave the others in the swapped state.
                        Plugin.Log.LogError($"veil remove: restoring renderer '{kv.Key.name}' failed: {e}");
                    }
                }
                foreach (var r in snap.Hidden)
                {
                    if (r == null) continue;
                    r.enabled = true;
                    if (r is ParticleSystemRenderer) (vanillaParticles ?? (vanillaParticles = new List<string>())).Add(r.name);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"veil remove: restoring the body failed: {e}");
            }
            finally
            {
                var destroyed = DestroyFog(snap);
                // Belt and braces (round I): whatever path may have left an untracked ip_fog* child, it goes with the veil.
                var swept = SweepStrayFog(p);
                Plugin.Log.LogInfo($"veil removed T{snap.Tier} on {p.GetPlayerName()}: {destroyed} emitters destroyed, {swept} strays swept" +
                                   (vanillaParticles != null ? $", {vanillaParticles.Count} vanilla particle renderers shown again ({string.Join(", ", vanillaParticles)})" : ""));
            }
        }

        /// <summary>True when one of the renderer's materials is a veil material (ip_ghost, ip_shadow, ip_distortion_tN, ip_spirit) or a clone of one.</summary>
        private static bool HoldsOurMaterial(Renderer r)
        {
            foreach (var m in r.sharedMaterials)
                if (m != null && m.name.StartsWith("ip_", StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// Armour change during a swap mode (task 10f): VisEquipment.SetChestEquipped/SetLegEquipped write the armour textures
        /// through m_bodyModel.material (VisEquipment.cs:1025-1044, 1106-1125). With our material in the slot that write lands on
        /// a clone of the veil material (Custom/Distortion and Standard have no _ChestTex, so it is lost) and the restore on
        /// Remove would bring back the pre-change body. Called by a prefix right before such a write: puts the body's original
        /// materials back and forgets them, so vanilla writes into the real body material. <see cref="ResumeBody"/> (postfix,
        /// same frame, before rendering) snapshots the updated materials and swaps again. False when the body is not swapped.
        /// </summary>
        public bool SuspendBody(Player p, Renderer body)
        {
            if (p == null || body == null || !_snapshots.TryGetValue(p, out var snap) || !snap.SharedMaterials.TryGetValue(body, out var originals)) return false;
            if (snap.Effective == BodyVeilMode.Spirit) CarryBodyChanges(body, originals, snap);
            if (HoldsOurMaterial(body)) body.sharedMaterials = originals;
            snap.SharedMaterials.Remove(body);
            return true;
        }

        /// <summary>Re-applies the body mode after <see cref="SuspendBody"/>: the body (and new armour renderers) are snapshotted and swapped again.</summary>
        public void ResumeBody(Player p)
        {
            if (p == null || !_snapshots.TryGetValue(p, out var snap)) return;
            ApplyBody(p, snap);
        }

        /// <summary>
        /// VisEquipment changes body textures/skin colour through m_bodyModel.materials[i], which clones a material the renderer
        /// did not instantiate: armour or skin changed while Spirit was on lands on a clone of our Spirit copy. Carry those slots
        /// back to the original body material (a per-player instance created by VisEquipment.Awake, not a shared asset) so the
        /// restore does not bring back pre-veil armour, and destroy the orphaned clone.
        /// </summary>
        private static void CarryBodyChanges(Renderer body, Material[] originals, Snapshot snap)
        {
            var current = body.sharedMaterials;
            for (var i = 0; i < current.Length && i < originals.Length; i++)
            {
                var clone = current[i];
                var original = originals[i];
                if (clone == null || original == null || snap.SpiritOwned.Contains(clone)) continue;
                if (clone.shader == null || clone.shader.name != SpiritShaderName) continue;   // not derived from our Spirit copy
                foreach (var slot in SpiritTextureSlots)
                    if (clone.HasProperty(slot) && original.HasProperty(slot)) original.SetTexture(slot, clone.GetTexture(slot));
                if (clone.HasProperty(SkinColorId) && original.HasProperty(SkinColorId)) original.SetColor(SkinColorId, clone.GetColor(SkinColorId));
                snap.SpiritOwned.Add(clone);   // destroyed with the veil's other Spirit copies
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
        /// Destroys any active ip_fog* child of an unveiled player (safety net: fog must never outlive the veil, whatever path left
        /// it). Only direct children are checked; the fog emitters are parented to the player root. Inactive ones are skipped:
        /// DestroyFog deactivates the emitters it destroys (deferred to the end of the frame). Returns the number swept.
        /// </summary>
        public int SweepStrayFog(Player p)
        {
            if (p == null || _snapshots.ContainsKey(p)) return 0;
            var swept = 0;
            var root = p.transform;
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                if (!IsOurs(child) || !child.activeSelf) continue;
                StopAndClear(child.GetComponent<ParticleSystem>());
                child.SetActive(false);
                Object.Destroy(child);
                swept++;
                Plugin.Log.LogWarning($"veil: destroyed stray fog object '{child.name}' on {p.GetPlayerName()} (no veil recorded)");
            }
            return swept;
        }

        /// <summary>Stops emitting and removes every live particle at once (world-space trail particles included).</summary>
        private static void StopAndClear(ParticleSystem ps)
        {
            if (ps == null) return;
            try { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
            catch (Exception e) { Plugin.Log.LogWarning($"veil: stopping fog '{ps.name}' failed: {e.Message}"); }
        }

        /// <summary>Destroys the veil's fog emitters and materials and its Spirit copies. Returns the number of emitters destroyed.</summary>
        private static int DestroyFog(Snapshot snap)
        {
            // Spirit copies: the renderers already got their originals back (Remove restores before this runs).
            foreach (var m in snap.SpiritOwned) if (m != null) Object.Destroy(m);
            snap.SpiritOwned.Clear();
            snap.SpiritByOriginal.Clear();
            var destroyed = 0;
            foreach (var e in snap.Fog)
            {
                if (e.Go == null) continue;
                StopAndClear(e.Ps);      // belt and braces (round I): no particle survives, whatever deactivation does
                e.Go.SetActive(false);   // Destroy is deferred to the end of the frame; hide it now
                Object.Destroy(e.Go);
                destroyed++;
            }
            snap.Fog.Clear();
            foreach (var m in snap.FogMaterials) if (m != null) Object.Destroy(m);
            snap.FogMaterials.Clear();
            return destroyed;
        }
    }

    /// <summary>
    /// Keeps a fog emitter on its bone: bone position plus an offset in the player's frame, rotation of the player. Ground mode
    /// (ground fog field): the bone's x/z at the root's height plus GroundHeight, i.e. the bone projected to the feet.
    /// PositionOnly (round R ruling A1, the outer fog volume): the same position, but world rotation identity, so the local-space
    /// follow puffs do not swing around with every turn of the player.
    /// </summary>
    internal sealed class FogAnchorFollower : MonoBehaviour
    {
        public Transform Bone;
        public Transform Root;
        public Vector3 Offset;
        public bool Ground;
        public float GroundHeight;
        public bool PositionOnly;
        public float SpawnedAt;   // Time.time of the spawn; the Debug stray scan prints the age

        public void Snap()
        {
            if (Bone == null || Root == null) return;
            if (Ground)
            {
                var b = Bone.position;
                transform.SetPositionAndRotation(new Vector3(b.x, Root.position.y + GroundHeight, b.z), Root.rotation);
                return;
            }
            transform.SetPositionAndRotation(Bone.position + Root.rotation * Offset, PositionOnly ? Quaternion.identity : Root.rotation);
        }

        private void LateUpdate() => Snap();
    }
}
