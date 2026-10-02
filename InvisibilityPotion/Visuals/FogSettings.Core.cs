using System;
using System.Collections.Generic;
using System.Globalization;

namespace InvisibilityPotion.Visuals
{
    /// <summary>One fog emitter anchored to a body bone. Offset is in player space (x right, y up, z forward), metres.</summary>
    public sealed class FogAnchor
    {
        public string Name = "";
        public bool Enabled = true;
        public float Radius = 0.15f;
        public float X, Y, Z;

        public FogAnchor Clone() => (FogAnchor)MemberwiseClone();

        /// <summary>Config form "on|off,radius,x,y,z".</summary>
        public string Format() =>
            $"{(Enabled ? "on" : "off")},{FloatList.Format(Radius)},{FloatList.Format(X)},{FloatList.Format(Y)},{FloatList.Format(Z)}";

        public static bool TryParse(string name, string text, out FogAnchor anchor)
        {
            anchor = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var parts = text.Split(',');
            if (parts.Length != 5) return false;
            if (!TryParseSwitch(parts[0], out var enabled)) return false;
            var values = new float[4];
            for (var i = 0; i < 4; i++)
                if (!FloatList.TryParseOne(parts[i + 1], out values[i])) return false;
            if (values[0] < 0f) return false;
            anchor = new FogAnchor { Name = name, Enabled = enabled, Radius = values[0], X = values[1], Y = values[2], Z = values[3] };
            return true;
        }

        public static bool TryParseSwitch(string text, out bool on)
        {
            switch ((text ?? "").Trim().ToLowerInvariant())
            {
                case "on": case "true": case "1": on = true; return true;
                case "off": case "false": case "0": on = false; return true;
                default: on = false; return false;
            }
        }
    }

    /// <summary>How the fog alpha is split between the material colour and the particle (vertex) colour. Global ([Fog] FogAlphaMode).</summary>
    public enum FogAlphaMode { Both, Material, Vertex }

    /// <summary>Plain rgb colour (no game types), used for the dynamic fog colour blend.</summary>
    public struct FogRgb
    {
        public float R, G, B;

        public FogRgb(float r, float g, float b) { R = r; G = g; B = b; }

        /// <summary>Linear blend a→b; t is clamped to 0..1 (0 = a, 1 = b).</summary>
        public static FogRgb Blend(FogRgb a, FogRgb b, float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return new FogRgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
        }
    }

    /// <summary>Where the fog particles spawn: around body bones (one emitter per anchor) or on the body mesh surface (one emitter).</summary>
    public enum FogEmitterMode { Bones, Mesh }

    /// <summary>
    /// Simulation space of a fog layer: Follow = local space (particles move with the body), Trail = world space (particles stay
    /// where they were emitted, so moving leaves a trail). Config: Trail / OuterTrail (bool).
    /// </summary>
    public enum FogTrailMode { Follow, Trail }

    public enum FogValueKind { Float, Bool, Text }

    /// <summary>The three kinds of fog emitter: inner (on the body), outer (wide ring on bones), ground (fog field at the feet).</summary>
    public enum FogLayerKind { Inner, Outer, Ground }

    /// <summary>
    /// Revision of the per-tier look defaults and what a migration to it changes (pure; PluginConfig applies it to the file).
    /// Revision 3 (round H, task 10h): tier I is the normal body in a thin fog layer plus a ground fog field, tier II gets the
    /// ground field too. Revision 4 (round I, task 10i): matte fog (Emission 0), tier I fainter and greyer, tier II = the values
    /// the user saved from the tuning window. Revision 5 (plan 4 fix round 2): FogMaterial soft is the default of every tier;
    /// files at revision 4 keep their tuning and only move FogMaterial swamp_mist to soft (all three tiers).
    /// </summary>
    public static class LookDefaults
    {
        public const int Revision = 5;
        /// <summary>Files below this revision get the [Fog.TierN] sections of <see cref="ResetTiers"/> reset to the defaults.</summary>
        public const int FullResetRevision = 4;
        /// <summary>First revision whose FogMaterial default is soft.</summary>
        public const int SoftFogRevision = 5;

        /// <summary>True when a file at <paramref name="fromRevision"/> gets the full reset of <see cref="ResetTiers"/>.</summary>
        public static bool ResetsTiers(int fromRevision) => fromRevision < FullResetRevision;

        /// <summary>
        /// FogMaterial after the migration from <paramref name="fromRevision"/>: swamp_mist (the default before revision 5) becomes
        /// soft; any other value, and swamp_mist chosen in a revision 5 file, is kept.
        /// </summary>
        public static string MigrateFogMaterial(string current, int fromRevision) =>
            fromRevision < SoftFogRevision && FogSettings.NormalizeFogMaterial(current) == "swamp_mist" ? "soft" : current;
        /// <summary>First revision whose [Tier1] BodyVeilMode default is Off; files from it on keep their tier I body mode.</summary>
        public const int Tier1BodyOffRevision = 3;
        /// <summary>[Fog.TierN] sections reset to the new defaults when a file is below <see cref="Revision"/>; tier III is untouched.</summary>
        public static readonly int[] ResetTiers = { 1, 2 };
        /// <summary>Default [TierN] BodyVeilMode per tier (index 1..3).</summary>
        public static readonly string[] BodyVeilModes = { null, "Off", "Distortion", "Distortion" };

        /// <summary>
        /// BodyVeilMode after the migration from <paramref name="fromRevision"/>: tier I on the pre-revision-3 default Distortion
        /// becomes Off; anything else is kept (from revision 3 on, Distortion on tier I is the user's own choice).
        /// </summary>
        public static string MigrateBodyVeilMode(int tier, string current, int fromRevision) =>
            tier == 1 && fromRevision < Tier1BodyOffRevision && string.Equals((current ?? "").Trim(), "Distortion", StringComparison.Ordinal) ? "Off" : current;
    }

    /// <summary>One config key of a per-tier fog section ([Fog.TierN]).</summary>
    public sealed class FogKey
    {
        public readonly string Name;
        public readonly FogValueKind Kind;
        public readonly string Description;

        public FogKey(string name, FogValueKind kind, string description) { Name = name; Kind = kind; Description = description; }
    }

    /// <summary>
    /// Per-tier look of the veil: body-anchored fog (inner layer plus an optional wider, fainter outer layer), an optional ground
    /// fog field at the feet, and the tier's
    /// Distortion parameters. One instance per tier, config section [Fog.TierN] (local, not server-synced).
    /// </summary>
    public sealed class FogSettings
    {
        /// <summary>Live particles per emitter above which the tuning window warns.</summary>
        public const int ParticleWarnThreshold = 200;
        /// <summary>Hard cap on particles per emitter, whatever rate × lifetime asks for.</summary>
        public const int ParticleHardCap = 300;
        /// <summary>Default anchors of the outer layer (OuterAnchors) when a tier does not set its own.</summary>
        public static readonly string[] DefaultOuterAnchors = { "Head", "Chest", "Hips" };

        public static readonly string[] AnchorNames =
        {
            "Head", "Chest", "Hips", "LeftShoulder", "RightShoulder", "LeftHand", "RightHand",
            "LeftUpperLeg", "RightUpperLeg", "LeftLowerLeg", "RightLowerLeg", "LeftFoot", "RightFoot",
        };

        public bool Enabled = true;
        public float Rate = 4f;          // particles per second per emitter
        public float Size = 1.5f;        // metres; start size randomised in [0.6, 1.25] x Size
        public float Lifetime = 5f;      // seconds; randomised in [0.8, 1.2] x Lifetime
        public float Speed = 0.05f;      // start speed, m/s
        public float Alpha = 0.5f;
        public float R = 0.85f, G = 0.87f, B = 0.9f;
        public bool DynamicColor;        // blend 50/50 with the environment fog colour (darkens the fog in rain and at night)
        /// <summary>Self-illumination of the fog: _EmissionColor = colour x Emission (0..1), keeps it whitish in the dark.</summary>
        public float Emission;
        public float SpreadX = 1f, SpreadY = 0.35f, SpreadZ = 1f;   // emitter shape scale: flattened, spreads sideways
        public float Drift = 0f;         // vertical drift, m/s (world space); 0 = no plume
        /// <summary>Inner layer in world space: particles stay where they were emitted (trail behind a moving player).</summary>
        public bool Trail;
        /// <summary>Mesh emitter: particles per second on the body surface; 0 = Rate x enabled anchors.</summary>
        public float MeshRate;
        public bool OuterEnabled;
        /// <summary>Outer layer: spawn radius in metres around each outer anchor (absolute, independent of the anchor radius).</summary>
        public float OuterRadius = 1.4f;
        public float OuterAlpha = 0.35f;
        /// <summary>Outer layer: particles per second per outer anchor (independent of the inner Rate).</summary>
        public float OuterRate = 14f;
        public float OuterSize = 1.8f;    // metres; randomised like Size
        public float OuterLifetime = 3f;  // seconds; randomised like Lifetime
        /// <summary>Outer layer vertical shape scale on OuterRadius; below 1 flattens the disc.</summary>
        public float OuterSpreadY = 0.35f;
        public bool OuterTrail;
        /// <summary>
        /// Outer layer quads lie parallel to the ground (HorizontalBillboard) instead of facing the camera. Round H: upright 1.8 m
        /// billboards cannot form a flat ring (each quad is ~4x taller than the 0.42 m disc), so the ring read as part of the body cloud.
        /// </summary>
        public bool OuterHorizontal = true;
        /// <summary>Bones the outer layer spawns on (one emitter each, radius OuterRadius, offset from the Anchor.* key), whatever the emitter mode.</summary>
        public List<string> OuterAnchors = new List<string>(DefaultOuterAnchors);
        /// <summary>Ground fog field: world-space, ground-parallel particles at the feet that stay where they were emitted and grow.</summary>
        public bool GroundEnabled;
        public float GroundRate = 4f;          // particles per second (also while standing)
        public float GroundRateDistance = 2f;  // extra particles per metre walked (world space rateOverDistance)
        public float GroundSize = 0.8f;        // metres at birth
        public float GroundGrow = 3f;          // size at death = GroundSize x GroundGrow
        public float GroundLifetime = 7f;      // seconds
        public float GroundAlpha = 0.22f;
        public float GroundRadius = 0.6f;      // spawn radius around the feet, metres
        public float GroundHeight = 0.15f;     // metres above the player's root (feet)
        public float GroundDrift = 0.05f;      // random horizontal drift amplitude, m/s
        public float DistortionStrength = 0.1f;
        public float DR = 1f, DG = 1f, DB = 1f, DA = 0.08f;
        /// <summary>Ripple speed (_WaveVel) of the Distortion body mode; negative = keep the value borrowed from staff_shield_shard.</summary>
        public float DistortionWave = -1f;
        /// <summary>Borrowed vanilla particle material of the fog, one of <see cref="FogMaterialNames"/>.</summary>
        public string FogMaterial = DefaultFogMaterial;
        public FogEmitterMode EmitterMode = FogEmitterMode.Bones;
        /// <summary>Mesh emitter: distance in metres the particles spawn off the body surface (along the normal).</summary>
        public float MeshOffset = 0.03f;
        public List<FogAnchor> Anchors = new List<FogAnchor>();

        public const string DefaultFogMaterial = "soft";

        /// <summary>
        /// The vanilla material the "soft" fog material is made from (FogVeil: a copy of swamp_mist, same Custom/LitParticles
        /// shader and lighting properties, with a generated white radial-falloff _MainTex instead of the brownish dust02 and a
        /// flat _NormalTex).
        /// </summary>
        public const string SoftFogBase = "swamp_mist";

        /// <summary>
        /// Selectable fog materials (config names). soft is generated from swamp_mist (see <see cref="SoftFogBase"/>). slowwispysmoke stands for the Ghost's slowwispysmoke_gradient_alphablend;
        /// FogVeil maps each name to the vanilla material and its source prefab.
        /// </summary>
        public static readonly string[] FogMaterialNames = { "soft", "swamp_mist", "ghost_smoke", "wraith_smoke", "slowwispysmoke" };

        /// <summary>
        /// Former [Fog.TierN] keys that are no longer bound (round G replaced the relative outer factors by the absolute
        /// OuterRadius/OuterAlpha/OuterRate/OuterSize/OuterLifetime). PluginConfig logs and drops them.
        /// </summary>
        public static readonly string[] ObsoleteKeys = { "OuterRadiusMultiplier", "OuterAlphaFactor", "OuterRateFactor", "OuterSizeFactor", "OuterLifetimeFactor" };

        /// <summary>The ground fog field keys (round H), in file order.</summary>
        public static readonly string[] GroundKeys =
        {
            "GroundEnabled", "GroundRate", "GroundRateDistance", "GroundSize", "GroundGrow", "GroundLifetime", "GroundAlpha", "GroundRadius", "GroundHeight", "GroundDrift",
        };

        /// <summary>Movement speed (m/s) the ground field budget assumes for rateOverDistance; roughly a running player (assumption, not read from the game).</summary>
        public const float GroundBudgetSpeed = 7f;

        /// <summary>Canonical fog material name for a config value (case-insensitive), or null when it is not one of <see cref="FogMaterialNames"/>.</summary>
        public static string NormalizeFogMaterial(string text)
        {
            var t = (text ?? "").Trim();
            foreach (var n in FogMaterialNames)
                if (string.Equals(n, t, StringComparison.OrdinalIgnoreCase)) return n;
            return null;
        }

        public static bool TryParseEmitterMode(string text, out FogEmitterMode mode) =>
            Enum.TryParse((text ?? "").Trim(), true, out mode) && Enum.IsDefined(typeof(FogEmitterMode), mode);

        public static FogSettings Defaults(int tier)
        {
            var s = new FogSettings();
            foreach (var name in AnchorNames) s.Anchors.Add(DefaultAnchor(name));
            switch (tier)
            {
                case 1:
                    // Light tier (round H): the normal body ([Tier1] BodyVeilMode Off) in a thin, close fog layer that follows the
                    // body (no trail plume), plus a ground fog field along the walked path. Distortion values stay for the mode switch.
                    // Round I: matte (Emission 0), fainter and greyer so it reads as mist, not as a glow.
                    s.Rate = 5f; s.Size = 0.45f; s.Lifetime = 2f; s.Speed = 0.03f; s.Alpha = 0.18f;
                    s.R = 0.8f; s.G = 0.82f; s.B = 0.85f; s.Emission = 0f;
                    s.SpreadX = 1f; s.SpreadY = 0.5f; s.SpreadZ = 1f; s.Drift = 0.03f;
                    s.Trail = false;
                    s.GroundEnabled = true;
                    s.DistortionStrength = 0.04f; s.DA = 0.5f;
                    break;
                case 2:
                    // Round G: overlapping blobs on every bone form one cloud head to feet (follows the body), plus a flat disc of
                    // large slow particles at hip height that trails.
                    // Round I: the inner cloud is the user's tuning saved from ip_fogui (2026-10-02), matte (Emission 0). The faint
                    // alpha with the full SpreadY reads as a thin haze; the slight downward drift keeps it from rising into a plume.
                    s.Rate = 12f; s.Size = 0.685f; s.Lifetime = 2.5f; s.Speed = 0.05f; s.Alpha = 0.035f;
                    s.R = 0.775f; s.G = 0.775f; s.B = 0.775f; s.Emission = 0f;
                    s.SpreadX = 1.125f; s.SpreadY = 1f; s.SpreadZ = 1.025f; s.Drift = -0.066f;
                    s.OuterEnabled = true;
                    s.OuterAnchors = new List<string> { "Hips" };
                    s.OuterRadius = 1.4f; s.OuterAlpha = 0.35f; s.OuterRate = 14f; s.OuterSize = 1.8f; s.OuterLifetime = 3f;
                    s.OuterSpreadY = 0.15f; s.OuterTrail = true;
                    // Round H: a denser, wider ground fog field than tier I.
                    s.GroundEnabled = true;
                    s.GroundRate = 6f; s.GroundRateDistance = 3f; s.GroundSize = 1f; s.GroundGrow = 3.5f; s.GroundAlpha = 0.3f; s.GroundRadius = 0.9f;
                    s.DistortionStrength = 0.1f; s.DA = 0.08f;
                    break;
                case 3:
                    s.Enabled = false;
                    s.DistortionStrength = 0.03f; s.DA = 0.02f;
                    break;
            }
            return s;
        }

        public static FogAnchor DefaultAnchor(string name)
        {
            switch (name)
            {
                case "Head": return new FogAnchor { Name = name, Radius = 0.15f, Y = 0.05f };
                case "Chest": return new FogAnchor { Name = name, Radius = 0.25f };
                case "Hips": return new FogAnchor { Name = name, Radius = 0.22f };
                case "LeftShoulder": case "RightShoulder": return new FogAnchor { Name = name, Radius = 0.12f };
                case "LeftHand": case "RightHand": return new FogAnchor { Name = name, Radius = 0.1f };
                // Upper/lower leg bones sit at the hip joint and the knee; the offset moves the emitter to mid-thigh / mid-shin.
                case "LeftUpperLeg": case "RightUpperLeg": return new FogAnchor { Name = name, Radius = 0.12f, Y = -0.22f };
                case "LeftLowerLeg": case "RightLowerLeg": return new FogAnchor { Name = name, Radius = 0.1f, Y = -0.2f };
                case "LeftFoot": case "RightFoot": return new FogAnchor { Name = name, Radius = 0.1f, Y = 0.05f };
                default: return new FogAnchor { Name = name };
            }
        }

        public FogSettings Clone()
        {
            var copy = (FogSettings)MemberwiseClone();
            copy.Anchors = new List<FogAnchor>(Anchors.Count);
            foreach (var a in Anchors) copy.Anchors.Add(a.Clone());
            copy.OuterAnchors = new List<string>(OuterAnchors);
            return copy;
        }

        public FogAnchor Anchor(string name)
        {
            foreach (var a in Anchors)
                if (string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)) return a;
            return null;
        }

        public FogRgb Color => new FogRgb(R, G, B);

        public FogTrailMode InnerTrailMode => Trail ? FogTrailMode.Trail : FogTrailMode.Follow;
        public FogTrailMode OuterTrailMode => OuterTrail ? FogTrailMode.Trail : FogTrailMode.Follow;

        /// <summary>True when the inner layer emits anything: alpha above 0 and a rate (Rate, or MeshRate in Mesh mode).</summary>
        public bool InnerActive => Alpha > 0f && (Rate > 0f || (EmitterMode == FogEmitterMode.Mesh && MeshRate > 0f));

        /// <summary>True when the outer layer emits anything; independent of the inner layer (it can be tested alone).</summary>
        public bool OuterActive => OuterEnabled && OuterRate > 0f && OuterAlpha > 0f && OuterAnchors.Count > 0;

        /// <summary>True when the ground fog field emits anything: enabled, alpha above 0 and a rate over time or over distance.</summary>
        public bool GroundActive => GroundEnabled && GroundAlpha > 0f && (GroundRate > 0f || GroundRateDistance > 0f);

        /// <summary>Emission the ground field is budgeted for: GroundRate plus GroundRateDistance at <see cref="GroundBudgetSpeed"/>.</summary>
        public float GroundBudgetRate => Math.Max(0f, GroundRate) + Math.Max(0f, GroundRateDistance) * GroundBudgetSpeed;

        /// <summary>Expected live particles of the ground emitter while running; 0 when the field is off.</summary>
        public float LiveParticlesGround => GroundEnabled ? GroundBudgetRate * Math.Max(0f, GroundLifetime) : 0f;

        /// <summary>maxParticles of the ground emitter (budget rate x lifetime with headroom, hard-capped).</summary>
        public int GroundMaxParticles => MaxParticles(GroundBudgetRate, GroundLifetime);

        // ---- ground field look (plan 4 fix round 2, ruling 5): two height layers so the patches overlap into a volume ----

        /// <summary>Start size of a ground particle: random in [min, max] x GroundSize.</summary>
        public const float GroundSizeMinFactor = 0.7f;
        public const float GroundSizeMaxFactor = 1.3f;
        /// <summary>The upper layer floats this many metres above GroundHeight (0.45 m at the default 0.15 m).</summary>
        public const float GroundUpperOffset = 0.3f;
        /// <summary>The upper layer emits this fraction of the ground rates (over time and over distance).</summary>
        public const float GroundUpperRateFactor = 0.5f;
        /// <summary>The upper layer's alpha is this fraction of GroundAlpha.</summary>
        public const float GroundUpperAlphaFactor = 0.7f;
        /// <summary>maxParticles of both ground emitters together.</summary>
        public const int GroundParticleBudget = 300;

        public float GroundUpperHeight => GroundHeight + GroundUpperOffset;
        public float GroundUpperAlpha => GroundAlpha * GroundUpperAlphaFactor;
        public float GroundUpperRate => Math.Max(0f, GroundRate) * GroundUpperRateFactor;
        public float GroundUpperRateDistance => Math.Max(0f, GroundRateDistance) * GroundUpperRateFactor;

        /// <summary>maxParticles of both layers together: headroom over (1 + upper factor) x budget rate x lifetime, capped at <see cref="GroundParticleBudget"/>.</summary>
        private int GroundTotalMaxParticles
        {
            get
            {
                var expected = GroundBudgetRate * (1f + GroundUpperRateFactor) * Math.Max(0f, GroundLifetime) * 1.3f + 12f;
                return (int)Math.Min(GroundParticleBudget, Math.Max(12f, Math.Ceiling(expected)));
            }
        }

        /// <summary>maxParticles of the lower ground emitter: its rate share of the combined budget (at least 8).</summary>
        public int GroundLowerMaxParticles
        {
            get
            {
                var total = GroundTotalMaxParticles;
                var lower = (int)Math.Round(total / (1f + GroundUpperRateFactor));
                return Math.Min(total - 4, Math.Max(8, lower));
            }
        }

        /// <summary>maxParticles of the upper ground emitter: the rest of the combined budget (at least 4).</summary>
        public int GroundUpperMaxParticles => GroundTotalMaxParticles - GroundLowerMaxParticles;

        /// <summary>Rate of the single Mesh emitter for <paramref name="enabledAnchors"/> enabled anchors: MeshRate, or Rate x anchors when MeshRate is 0.</summary>
        public float MeshEmitterRate(int enabledAnchors) => MeshRate > 0f ? MeshRate : Math.Max(0f, Rate) * Math.Max(0, enabledAnchors);

        /// <summary>Expected live particles of one inner emitter (rate × lifetime).</summary>
        public float LiveParticlesInner => Math.Max(0f, Rate) * Math.Max(0f, Lifetime);

        /// <summary>Expected live particles of one outer emitter (outer rate × outer lifetime).</summary>
        public float LiveParticlesOuter => OuterEnabled ? Math.Max(0f, OuterRate) * Math.Max(0f, OuterLifetime) : 0f;

        /// <summary>Expected live particles of the Mesh emitter (MeshRate or Rate × anchors, × lifetime).</summary>
        public float LiveParticlesMesh
        {
            get
            {
                var n = 0;
                foreach (var a in Anchors) if (a.Enabled) n++;
                return MeshEmitterRate(n) * Math.Max(0f, Lifetime);
            }
        }

        public bool ExceedsParticleBudget => (EmitterMode == FogEmitterMode.Mesh ? LiveParticlesMesh : LiveParticlesInner) > ParticleWarnThreshold
                                             || LiveParticlesOuter > ParticleWarnThreshold || LiveParticlesGround > ParticleWarnThreshold;

        /// <summary>maxParticles for an inner emitter that emits <paramref name="rate"/> per second (lifetime = Lifetime).</summary>
        public int MaxParticles(float rate) => MaxParticles(rate, Lifetime);

        /// <summary>maxParticles for an emitter that emits <paramref name="rate"/> per second: headroom over rate × lifetime, hard-capped.</summary>
        public static int MaxParticles(float rate, float lifetime)
        {
            var expected = Math.Max(0f, rate) * Math.Max(0f, lifetime) * 1.3f + 4f;
            return (int)Math.Min(ParticleHardCap, Math.Max(8f, Math.Ceiling(expected)));
        }

        /// <summary>Canonical anchor names of a comma list ("chest, Hips"); empty text = no anchors. False on an unknown name.</summary>
        public static bool TryParseAnchorList(string text, out List<string> names)
        {
            names = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return true;
            foreach (var part in text.Split(','))
            {
                var t = part.Trim();
                if (t.Length == 0) continue;
                string match = null;
                foreach (var n in AnchorNames)
                    if (string.Equals(n, t, StringComparison.OrdinalIgnoreCase)) { match = n; break; }
                if (match == null) { names = null; return false; }
                if (!names.Contains(match)) names.Add(match);
            }
            return true;
        }

        public static bool TryParseAlphaMode(string text, out FogAlphaMode mode) =>
            Enum.TryParse((text ?? "").Trim(), true, out mode) && Enum.IsDefined(typeof(FogAlphaMode), mode);

        // ---------- config keys ----------

        public const string AnchorPrefix = "Anchor.";

        /// <summary>Every key of a [Fog.TierN] section in file order, including one Anchor.&lt;Name&gt; per anchor.</summary>
        public static readonly IReadOnlyList<FogKey> Keys = BuildKeys();

        private static List<FogKey> BuildKeys()
        {
            var k = new List<FogKey>
            {
                new FogKey("Enabled", FogValueKind.Bool, "Body-anchored fog around the hidden player"),
                new FogKey("Rate", FogValueKind.Float, "Fog particles per second per body emitter"),
                new FogKey("Size", FogValueKind.Float, "Fog particle size in metres (randomised 0.6x..1.25x)"),
                new FogKey("Lifetime", FogValueKind.Float, "Fog particle lifetime in seconds (randomised 0.8x..1.2x)"),
                new FogKey("Speed", FogValueKind.Float, "Fog particle start speed in m/s"),
                new FogKey("Alpha", FogValueKind.Float, "Fog alpha 0..1"),
                new FogKey("Color", FogValueKind.Text, "Fog colour as r,g,b (0..1)"),
                new FogKey("DynamicColor", FogValueKind.Bool, "Blend the fog colour 50/50 with the environment's fog colour (day, night, weather); turns the fog dark in rain and at night"),
                new FogKey("Emission", FogValueKind.Float, "Fog self-illumination 0..1 (_EmissionColor = Color x Emission); keeps the fog whitish in rain and at night but makes it glow, 0 = matte, lit by the scene only (default)"),
                new FogKey("SpreadX", FogValueKind.Float, "Emitter shape scale sideways (multiplies each anchor's radius)"),
                new FogKey("SpreadY", FogValueKind.Float, "Emitter shape scale vertically; below 1 flattens the fog"),
                new FogKey("SpreadZ", FogValueKind.Float, "Emitter shape scale front/back"),
                new FogKey("Drift", FogValueKind.Float, "Vertical drift of fog particles in m/s (world space); 0 = no plume, negative = sinks"),
                new FogKey("Trail", FogValueKind.Bool, "true = world space: fog particles stay where they were emitted, so moving leaves a trail; false = they follow the body"),
                new FogKey("MeshRate", FogValueKind.Float, "Mesh emitter: particles per second on the body surface; 0 = Rate x enabled anchors"),
                new FogKey("OuterEnabled", FogValueKind.Bool, "Second, wider fog layer on the OuterAnchors bones"),
                new FogKey("OuterAnchors", FogValueKind.Text, "Bones of the outer layer as a comma list of anchor names (Head, Chest, Hips, LeftShoulder, RightShoulder, LeftHand, RightHand, LeftUpperLeg, RightUpperLeg, LeftLowerLeg, RightLowerLeg, LeftFoot, RightFoot); the offset comes from the Anchor.* key, the radius from OuterRadius"),
                new FogKey("OuterRadius", FogValueKind.Float, "Outer layer spawn radius in metres around each outer anchor"),
                new FogKey("OuterAlpha", FogValueKind.Float, "Outer layer alpha 0..1 (independent of Alpha)"),
                new FogKey("OuterRate", FogValueKind.Float, "Outer layer particles per second per outer anchor (independent of Rate; 0 = no outer layer)"),
                new FogKey("OuterSize", FogValueKind.Float, "Outer layer particle size in metres (randomised 0.6x..1.25x)"),
                new FogKey("OuterSpreadY", FogValueKind.Float, "Outer layer vertical shape scale on OuterRadius; below 1 flattens the disc"),
                new FogKey("OuterLifetime", FogValueKind.Float, "Outer layer particle lifetime in seconds (randomised 0.8x..1.2x)"),
                new FogKey("OuterTrail", FogValueKind.Bool, "Outer layer in world space (leaves a trail), like Trail for the inner layer"),
                new FogKey("OuterHorizontal", FogValueKind.Bool, "Outer layer particles lie flat, parallel to the ground (a visible flat ring from above); false = they face the camera like the inner fog"),
                new FogKey("GroundEnabled", FogValueKind.Bool, "Ground fog field: flat fog patches at the feet that stay where they were emitted and spread, so walking leaves a field of fog"),
                new FogKey("GroundRate", FogValueKind.Float, "Ground field particles per second (also while standing still)"),
                new FogKey("GroundRateDistance", FogValueKind.Float, "Ground field extra particles per metre walked"),
                new FogKey("GroundSize", FogValueKind.Float, "Ground field particle size in metres at birth"),
                new FogKey("GroundGrow", FogValueKind.Float, "Ground field growth: a particle ends at GroundSize x GroundGrow (1 = no growth)"),
                new FogKey("GroundLifetime", FogValueKind.Float, "Ground field particle lifetime in seconds"),
                new FogKey("GroundAlpha", FogValueKind.Float, "Ground field alpha 0..1 (fades in over the first 15 % and out over the last 40 % of the lifetime)"),
                new FogKey("GroundRadius", FogValueKind.Float, "Ground field spawn radius in metres around the feet"),
                new FogKey("GroundHeight", FogValueKind.Float, "Ground field height in metres above the player's feet (-0.5..2)"),
                new FogKey("GroundDrift", FogValueKind.Float, "Ground field random horizontal drift in m/s"),
                new FogKey("DistortionStrength", FogValueKind.Float, "Refraction strength when this tier's body mode is Distortion (shader property _RefractionIntensity)"),
                new FogKey("DistortionColor", FogValueKind.Text, "Colour of the Distortion body mode as r,g,b,a (shader property _Color)"),
                new FogKey("DistortionWave", FogValueKind.Float, "Ripple speed of the Distortion body mode (_WaveVel, normal map borrowed from staff_shield_shard); negative = the borrowed vanilla value"),
                new FogKey("FogMaterial", FogValueKind.Text, "Particle material of the fog: soft (swamp_mist with a neutral white soft sprite instead of its brownish dust texture), swamp_mist, ghost_smoke, wraith_smoke or slowwispysmoke"),
                new FogKey("FogEmitterMode", FogValueKind.Text, "Bones = one emitter per anchor below; Mesh = one emitter on the body mesh surface with rate = Rate x enabled anchors (falls back to Bones when the mesh is not readable)"),
                new FogKey("MeshOffset", FogValueKind.Float, "Mesh emitter: distance in metres the fog spawns off the body surface"),
            };
            foreach (var name in AnchorNames)
                k.Add(new FogKey(AnchorPrefix + name, FogValueKind.Text,
                    $"Fog emitter on the {name} bone: on|off,radius,x,y,z (offset in metres, player space: x right, y up, z forward)"));
            return k;
        }

        /// <summary>The key's value in config form (invariant culture; bools as true/false).</summary>
        public string Get(string key)
        {
            if (key.StartsWith(AnchorPrefix, StringComparison.Ordinal))
                return Anchor(key.Substring(AnchorPrefix.Length))?.Format();
            switch (key)
            {
                case "Enabled": return Bool(Enabled);
                case "Rate": return FloatList.Format(Rate);
                case "Size": return FloatList.Format(Size);
                case "Lifetime": return FloatList.Format(Lifetime);
                case "Speed": return FloatList.Format(Speed);
                case "Alpha": return FloatList.Format(Alpha);
                case "Color": return FloatList.Format(R, G, B);
                case "DynamicColor": return Bool(DynamicColor);
                case "SpreadX": return FloatList.Format(SpreadX);
                case "SpreadY": return FloatList.Format(SpreadY);
                case "SpreadZ": return FloatList.Format(SpreadZ);
                case "Drift": return FloatList.Format(Drift);
                case "Trail": return Bool(Trail);
                case "MeshRate": return FloatList.Format(MeshRate);
                case "OuterEnabled": return Bool(OuterEnabled);
                case "OuterAnchors": return string.Join(",", OuterAnchors);
                case "OuterSpreadY": return FloatList.Format(OuterSpreadY);
                case "OuterLifetime": return FloatList.Format(OuterLifetime);
                case "OuterTrail": return Bool(OuterTrail);
                case "OuterHorizontal": return Bool(OuterHorizontal);
                case "OuterRadius": return FloatList.Format(OuterRadius);
                case "OuterAlpha": return FloatList.Format(OuterAlpha);
                case "OuterRate": return FloatList.Format(OuterRate);
                case "OuterSize": return FloatList.Format(OuterSize);
                case "GroundEnabled": return Bool(GroundEnabled);
                case "GroundRate": return FloatList.Format(GroundRate);
                case "GroundRateDistance": return FloatList.Format(GroundRateDistance);
                case "GroundSize": return FloatList.Format(GroundSize);
                case "GroundGrow": return FloatList.Format(GroundGrow);
                case "GroundLifetime": return FloatList.Format(GroundLifetime);
                case "GroundAlpha": return FloatList.Format(GroundAlpha);
                case "GroundRadius": return FloatList.Format(GroundRadius);
                case "GroundHeight": return FloatList.Format(GroundHeight);
                case "GroundDrift": return FloatList.Format(GroundDrift);
                case "Emission": return FloatList.Format(Emission);
                case "DistortionStrength": return FloatList.Format(DistortionStrength);
                case "DistortionColor": return FloatList.Format(DR, DG, DB, DA);
                case "DistortionWave": return FloatList.Format(DistortionWave);
                case "FogMaterial": return FogMaterial;
                case "FogEmitterMode": return EmitterMode.ToString();
                case "MeshOffset": return FloatList.Format(MeshOffset);
                default: return null;
            }
        }

        private static string Bool(bool b) => b ? "true" : "false";

        /// <summary>Sets a key from its config form; values are clamped to sane ranges. False for an unknown key or malformed text.</summary>
        public bool TrySet(string key, string text)
        {
            if (key.StartsWith(AnchorPrefix, StringComparison.Ordinal))
            {
                var name = key.Substring(AnchorPrefix.Length);
                var i = Anchors.FindIndex(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
                if (i < 0 || !FogAnchor.TryParse(Anchors[i].Name, text, out var anchor)) return false;
                Anchors[i] = anchor;
                return true;
            }
            switch (key)
            {
                case "Enabled": return SetBool(text, ref Enabled);
                case "DynamicColor": return SetBool(text, ref DynamicColor);
                case "OuterEnabled": return SetBool(text, ref OuterEnabled);
                case "Trail": return SetBool(text, ref Trail);
                case "OuterTrail": return SetBool(text, ref OuterTrail);
                case "OuterHorizontal": return SetBool(text, ref OuterHorizontal);
                case "GroundEnabled": return SetBool(text, ref GroundEnabled);
                case "OuterAnchors":
                    if (!TryParseAnchorList(text, out var outerAnchors)) return false;
                    OuterAnchors = outerAnchors;
                    return true;
                case "Color":
                    if (!FloatList.TryParse(text, 3, out var rgb)) return false;
                    R = Clamp01(rgb[0]); G = Clamp01(rgb[1]); B = Clamp01(rgb[2]);
                    return true;
                case "DistortionColor":
                    if (!FloatList.TryParse(text, 4, out var rgba)) return false;
                    DR = Clamp01(rgba[0]); DG = Clamp01(rgba[1]); DB = Clamp01(rgba[2]); DA = Clamp01(rgba[3]);
                    return true;
                case "FogMaterial":
                    var name = NormalizeFogMaterial(text);
                    if (name == null) return false;
                    FogMaterial = name;
                    return true;
                case "FogEmitterMode":
                    if (!TryParseEmitterMode(text, out var em)) return false;
                    EmitterMode = em;
                    return true;
            }
            if (!FloatList.TryParseOne(text, out var v)) return false;
            switch (key)
            {
                case "Rate": Rate = Math.Max(0f, v); return true;
                case "Size": Size = Math.Max(0.01f, v); return true;
                case "Lifetime": Lifetime = Math.Max(0.05f, v); return true;
                case "Speed": Speed = v; return true;
                case "Alpha": Alpha = Clamp01(v); return true;
                case "SpreadX": SpreadX = Math.Max(0.01f, v); return true;
                case "SpreadY": SpreadY = Math.Max(0.01f, v); return true;
                case "SpreadZ": SpreadZ = Math.Max(0.01f, v); return true;
                case "Drift": Drift = v; return true;
                case "OuterRadius": OuterRadius = Math.Max(0f, v); return true;
                case "OuterAlpha": OuterAlpha = Clamp01(v); return true;
                case "OuterRate": OuterRate = Math.Max(0f, v); return true;
                case "OuterSize": OuterSize = Math.Max(0.01f, v); return true;
                case "Emission": Emission = Clamp01(v); return true;
                case "DistortionStrength": DistortionStrength = Math.Max(0f, v); return true;
                case "DistortionWave": DistortionWave = v < 0f ? -1f : v; return true;
                case "MeshOffset": MeshOffset = Math.Max(0f, v); return true;
                case "MeshRate": MeshRate = Math.Max(0f, v); return true;
                case "OuterSpreadY": OuterSpreadY = Math.Max(0.01f, v); return true;
                case "OuterLifetime": OuterLifetime = Math.Max(0.05f, v); return true;
                case "GroundRate": GroundRate = Math.Max(0f, v); return true;
                case "GroundRateDistance": GroundRateDistance = Math.Max(0f, v); return true;
                case "GroundSize": GroundSize = Math.Max(0.01f, v); return true;
                case "GroundGrow": GroundGrow = Math.Max(0.1f, v); return true;
                case "GroundLifetime": GroundLifetime = Math.Max(0.05f, v); return true;
                case "GroundAlpha": GroundAlpha = Clamp01(v); return true;
                case "GroundRadius": GroundRadius = Math.Max(0f, v); return true;
                case "GroundHeight": GroundHeight = v < -0.5f ? -0.5f : v > 2f ? 2f : v; return true;
                case "GroundDrift": GroundDrift = Math.Max(0f, v); return true;
                default: return false;
            }
        }

        private static bool SetBool(string text, ref bool field)
        {
            if (!FogAnchor.TryParseSwitch(text, out var on)) return false;
            field = on;
            return true;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>
        /// Builds a tier's settings from config text: starts from the tier's defaults and applies every key that
        /// <paramref name="read"/> returns; a malformed value keeps the default and adds a warning.
        /// </summary>
        public static FogSettings Parse(int tier, Func<string, string> read, List<string> warnings)
        {
            var s = Defaults(tier);
            foreach (var key in Keys)
            {
                var text = read(key.Name);
                if (text == null) continue;
                if (!s.TrySet(key.Name, text)) warnings?.Add($"{key.Name} '{text}' is malformed; using default {s.Get(key.Name)}");
            }
            return s;
        }
    }

    /// <summary>Comma-separated floats in invariant culture ("1,1,1,0.15").</summary>
    public static class FloatList
    {
        public static bool TryParseOne(string text, out float value)
        {
            if (!float.TryParse((text ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
            if (float.IsNaN(value) || float.IsInfinity(value)) { value = 0f; return false; }
            return true;
        }

        public static bool TryParse(string text, int count, out float[] values)
        {
            values = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var parts = text.Split(',');
            if (parts.Length != count) return false;
            var result = new float[count];
            for (var i = 0; i < count; i++)
                if (!TryParseOne(parts[i], out result[i])) return false;
            values = result;
            return true;
        }

        public static string Format(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        public static string Format(params float[] values)
        {
            var parts = new string[values.Length];
            for (var i = 0; i < values.Length; i++) parts[i] = Format(values[i]);
            return string.Join(",", parts);
        }
    }
}
