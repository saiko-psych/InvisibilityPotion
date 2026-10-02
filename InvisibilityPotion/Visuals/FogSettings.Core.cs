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

    /// <summary>
    /// Shape of the outer layer (OuterShape): Volume (round L) = large soft camera-facing sprites inside a flattened sphere of
    /// OuterRadius (height OuterSpreadY x radius), overlapping into fog; Ring (round J) = the Circle shape near the rim of
    /// OuterRadius (band height OuterSpreadY x radius).
    /// </summary>
    public enum FogOuterShape { Volume, Ring }

    /// <summary>The three kinds of fog emitter: inner (on the body), outer (wide ring on bones), ground (fog field at the feet).</summary>
    public enum FogLayerKind { Inner, Outer, Ground }

    /// <summary>
    /// Revision of the per-tier look defaults and what a migration to it changes (pure; PluginConfig applies it to the file).
    /// Revision 3 (round H, task 10h): tier I is the normal body in a thin fog layer plus a ground fog field, tier II gets the
    /// ground field too. Revision 4 (round I, task 10i): matte fog (Emission 0), tier I fainter and greyer, tier II = the values
    /// the user saved from the tuning window. Revision 5 (plan 4 fix round 2): FogMaterial soft is the default of every tier;
    /// files at revision 4 keep their tuning and only move FogMaterial swamp_mist to soft (all three tiers). Revision 6 (plan 4,
    /// round J ruling): the tier II outer layer is a distinct ring that stays around the player; files at revision 4 or 5 get only
    /// their [Fog.Tier2] Outer* keys reset (see <see cref="ResetsOuterKey"/>). Revision 7 (plan 4, round L ruling): the outer
    /// layer is a fog volume (OuterShape Volume), tier I and II get an enveloping body cloud; every older file gets [Fog.Tier1] and
    /// [Fog.Tier2] fully reset (<see cref="FullResetRevision"/> = 7), so the outer-only reset of revision 6 no longer runs alone.
    /// Revision 8 (plan 4, round M ruling): subtle haze: mid-grey fog, low alphas, the fog volume in world space (left behind);
    /// every older file gets [Fog.Tier1] and [Fog.Tier2] fully reset (<see cref="FullResetRevision"/> = 8).
    /// </summary>
    public static class LookDefaults
    {
        public const int Revision = 8;
        /// <summary>Files below this revision get the [Fog.TierN] sections of <see cref="ResetTiers"/> reset to the defaults (round M: 8).</summary>
        public const int FullResetRevision = 8;
        /// <summary>First revision whose FogMaterial default is soft.</summary>
        public const int SoftFogRevision = 5;
        /// <summary>First revision whose tier II outer layer is the ring (local space, 2.5 m, turning).</summary>
        public const int OuterRingRevision = 6;
        /// <summary>Key prefix of the outer layer keys.</summary>
        public const string OuterKeyPrefix = "Outer";

        /// <summary>
        /// True when the migration from <paramref name="fromRevision"/> resets [Fog.Tier<paramref name="tier"/>] <paramref name="key"/>
        /// to its default for the outer ring: only tier II, only the Outer* keys, only files below <see cref="OuterRingRevision"/>
        /// (files below <see cref="FullResetRevision"/> get the full tier reset anyway).
        /// </summary>
        public static bool ResetsOuterKey(int tier, string key, int fromRevision) =>
            tier == 2 && fromRevision < OuterRingRevision && key != null && key.StartsWith(OuterKeyPrefix, StringComparison.Ordinal);

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
        /// <summary>
        /// Outer ring: Circle shape radiusThickness (0 = particles on the rim only, 1 = the whole disc). 0.25 keeps them in the
        /// outer quarter of OuterRadius, so the layer reads as a ring, not a filled disc (round J ruling).
        /// </summary>
        public const float OuterRingThickness = 0.25f;
        /// <summary>Outer volume (round L): start size random in [min, max] x OuterSize.</summary>
        public const float OuterVolumeSizeMinFactor = 0.8f;
        public const float OuterVolumeSizeMaxFactor = 1.2f;
        /// <summary>Outer volume: start speed in m/s, random direction (slow drift of the fog).</summary>
        public const float OuterVolumeSpeed = 0.03f;
        /// <summary>Outer volume: alpha over the lifetime rises 0 to 1 until this fraction, then falls to 0 at the end (round M: 25 %).</summary>
        public const float OuterVolumeFadeIn = 0.25f;
        /// <summary>Outer volume (round M): size at death = start size x this factor (3.2 m grows to 4.5 m), linear over the lifetime.</summary>
        public const float OuterVolumeGrow = 4.5f / 3.2f;
        /// <summary>
        /// Round M ruling 2c: caps on simultaneously alive particles, so overlapping lit sprites cannot saturate into a white blob.
        /// Inner: per anchor (the Mesh emitter gets this x enabled anchors); outer volume: per emitter. The ground cap is
        /// <see cref="GroundParticleBudget"/> (both ground layers together).
        /// </summary>
        public const int InnerParticleCap = 10;
        public const int OuterVolumeParticleCap = 40;
        /// <summary>Clamp of OuterRotation (deg/s, either direction).</summary>
        public const float MaxOuterRotation = 180f;
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
        /// <summary>Outer layer: extra particles per metre walked (rateOverDistance; only in world space, OuterTrail true).</summary>
        public float OuterRateDistance;
        public float OuterSize = 1.8f;    // metres; randomised like Size
        public float OuterLifetime = 3f;  // seconds; randomised like Lifetime
        /// <summary>Outer ring band height as a fraction of OuterRadius (random vertical jitter of the spawn point).</summary>
        public float OuterSpreadY = 0.35f;
        public bool OuterTrail;
        /// <summary>Outer ring: turn speed around the player's up axis in deg/s (orbital velocity of the particles); 0 = still.</summary>
        public float OuterRotation;
        /// <summary>Outer ring: vertical offset in metres added to the outer anchor's offset (Anchor.* Y), so the ring can sit below the bone.</summary>
        public float OuterOffsetY;
        /// <summary>
        /// Outer layer quads lie parallel to the ground (HorizontalBillboard) instead of facing the camera. Round H: upright 1.8 m
        /// billboards cannot form a flat ring (each quad is ~4x taller than the 0.42 m disc), so the ring read as part of the body cloud.
        /// </summary>
        public bool OuterHorizontal = true;
        /// <summary>Outer layer shape: Volume (fog around the player, round L) or Ring (round J).</summary>
        public FogOuterShape OuterShape = FogOuterShape.Volume;
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

        public static bool TryParseOuterShape(string text, out FogOuterShape shape)
        {
            shape = FogOuterShape.Volume;
            var t = (text ?? "").Trim();
            foreach (FogOuterShape v in Enum.GetValues(typeof(FogOuterShape)))
                if (string.Equals(v.ToString(), t, StringComparison.OrdinalIgnoreCase)) { shape = v; return true; }
            return false;
        }

        /// <summary>
        /// Round L ruling 1: the outer layer as a fog volume. Large soft camera-facing sprites (OuterSize 3.2, random 0.8..1.2x) at
        /// low alpha in a flattened sphere (OuterRadius x OuterSpreadY high, about +-1 m around hip height at 3 m), slow drift.
        /// Round M ruling 1: emitted into the world and left behind: world space (OuterTrail), no turn, 4/s plus 2 per metre
        /// walked, 9 s lifetime, so a walking player lays fog along the path and a standing one slowly fills the spot.
        /// </summary>
        private static void ApplyFogVolume(FogSettings s, float alpha)
        {
            s.OuterEnabled = true;
            s.OuterShape = FogOuterShape.Volume;
            s.OuterAnchors = new List<string> { "Hips" };
            s.OuterHorizontal = false;
            s.OuterSize = 3.2f; s.OuterAlpha = alpha; s.OuterRadius = 3f; s.OuterSpreadY = 0.35f;
            s.OuterRate = 4f; s.OuterRateDistance = 2f; s.OuterLifetime = 9f; s.OuterRotation = 0f; s.OuterOffsetY = 0f; s.OuterTrail = true;
        }

        /// <summary>Round M ruling 2a: mid grey, darker than the sky, so the fog reads as haze and never as light.</summary>
        private static void ApplyHazeColor(FogSettings s) { s.R = 0.55f; s.G = 0.57f; s.B = 0.6f; }

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
                    // Round L: Rate 5 x Size 0.45 left gaps between the 13 bone emitters (fog on hands and feet only); the cloud
                    // now envelops the whole body like tier II's, only lighter, plus the fog volume and the ground field.
                    // Round M (revision 8): subtle haze: mid grey, alpha 0.09 inner, 0.045 volume, 0.10 ground.
                    s.Rate = 10f; s.Size = 0.75f; s.Lifetime = 2.2f; s.Speed = 0.03f; s.Alpha = 0.09f;
                    ApplyHazeColor(s); s.Emission = 0f;
                    s.SpreadX = 1f; s.SpreadY = 0.6f; s.SpreadZ = 1f; s.Drift = 0.03f;
                    s.Trail = false;
                    ApplyFogVolume(s, 0.045f);
                    s.GroundEnabled = true;
                    s.GroundAlpha = 0.1f;
                    s.DistortionStrength = 0.04f; s.DA = 0.5f;
                    break;
                case 2:
                    // Round G: overlapping blobs on every bone form one cloud head to feet (follows the body), plus a flat disc of
                    // large slow particles at hip height that trails.
                    // Round I: the inner cloud is the user's tuning saved from ip_fogui (2026-10-02), matte (Emission 0). The faint
                    // alpha with the full SpreadY reads as a thin haze; the slight downward drift keeps it from rising into a plume.
                    // Round L ruling 2 (revision 7): a denser cloud enveloping the whole body (all anchors), 0.8 grey, matte.
                    // Round M (revision 8): tier II was a blown-out white blob in daylight; mid grey and alpha 0.12 (rate/size kept).
                    s.Rate = 14f; s.Size = 0.8f; s.Lifetime = 2.5f; s.Speed = 0.05f; s.Alpha = 0.12f;
                    ApplyHazeColor(s); s.Emission = 0f;
                    s.SpreadX = 1.125f; s.SpreadY = 0.6f; s.SpreadZ = 1.025f; s.Drift = -0.066f;
                    // Round L ruling 1: light fog in a wide area around the player (replaces the round J ring of flat discs);
                    // round M: left behind in the world, alpha 0.06.
                    ApplyFogVolume(s, 0.06f);
                    // Round H: a wider ground fog field than tier I; round L: lighter (alpha, size, growth); round M: alpha 0.12.
                    s.GroundEnabled = true;
                    s.GroundRate = 6f; s.GroundRateDistance = 3f; s.GroundSize = 1.2f; s.GroundGrow = 2.5f; s.GroundAlpha = 0.12f; s.GroundRadius = 0.9f;
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
                // Round L: limb radius at least 0.12 m so neighbouring emitters overlap into one cloud.
                case "LeftHand": case "RightHand": return new FogAnchor { Name = name, Radius = 0.12f };
                // Upper/lower leg bones sit at the hip joint and the knee; the offset moves the emitter to mid-thigh / mid-shin.
                case "LeftUpperLeg": case "RightUpperLeg": return new FogAnchor { Name = name, Radius = 0.12f, Y = -0.22f };
                case "LeftLowerLeg": case "RightLowerLeg": return new FogAnchor { Name = name, Radius = 0.12f, Y = -0.2f };
                case "LeftFoot": case "RightFoot": return new FogAnchor { Name = name, Radius = 0.12f, Y = 0.05f };
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

        /// <summary>OuterRotation in rad/s (ParticleSystem velocityOverLifetime.orbitalY).</summary>
        public float OuterOrbitalRadPerSecond => OuterRotation * (float)Math.PI / 180f;

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
        /// <summary>maxParticles of both ground emitters together (round M ruling 2c: 60, was 300).</summary>
        public const int GroundParticleBudget = 60;

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

        /// <summary>maxParticles of one inner Bones emitter: the rate x lifetime budget, capped at <see cref="InnerParticleCap"/>.</summary>
        public int InnerMaxParticles => Math.Min(InnerParticleCap, MaxParticles(Rate, Lifetime));

        /// <summary>maxParticles of the Mesh emitter for <paramref name="enabledAnchors"/> anchors: budget capped at InnerParticleCap per anchor (at least 8).</summary>
        public int MeshMaxParticles(int enabledAnchors) =>
            Math.Min(Math.Max(8, InnerParticleCap * Math.Max(0, enabledAnchors)), MaxParticles(MeshEmitterRate(enabledAnchors), Lifetime));

        /// <summary>rateOverDistance of the outer layer: OuterRateDistance in world space (OuterTrail), 0 in local space (Unity ignores it there).</summary>
        public float OuterEffectiveRateDistance => OuterTrail ? Math.Max(0f, OuterRateDistance) : 0f;

        /// <summary>
        /// maxParticles of one outer emitter. Volume: budget of OuterRate plus the distance rate at <see cref="GroundBudgetSpeed"/>,
        /// capped at <see cref="OuterVolumeParticleCap"/>; Ring: rate x lifetime (hard cap only, as in round J).
        /// </summary>
        public int OuterMaxParticles => OuterShape == FogOuterShape.Volume
            ? Math.Min(OuterVolumeParticleCap, MaxParticles(Math.Max(0f, OuterRate) + OuterEffectiveRateDistance * GroundBudgetSpeed, OuterLifetime))
            : MaxParticles(OuterRate, OuterLifetime);

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
                new FogKey("OuterRateDistance", FogValueKind.Float, "Outer layer extra particles per metre walked (only with OuterTrail true: world space)"),
                new FogKey("OuterSize", FogValueKind.Float, "Outer layer particle size in metres (randomised 0.6x..1.25x)"),
                new FogKey("OuterSpreadY", FogValueKind.Float, "Outer layer height as a fraction of OuterRadius: Volume = vertical scale of the spawn sphere, Ring = band height (random vertical jitter); small = flat"),
                new FogKey("OuterLifetime", FogValueKind.Float, "Outer layer particle lifetime in seconds (randomised 0.8x..1.2x)"),
                new FogKey("OuterTrail", FogValueKind.Bool, "Outer layer in world space (leaves a trail), like Trail for the inner layer"),
                new FogKey("OuterRotation", FogValueKind.Float, "Outer layer turn speed around the player in deg/s (negative = the other way, 0 = still)"),
                new FogKey("OuterOffsetY", FogValueKind.Float, "Outer ring vertical offset in metres, added to the outer anchor's Anchor.* offset (negative = lower)"),
                new FogKey("OuterShape", FogValueKind.Text, "Outer layer shape: Volume = large soft fog sprites inside a flattened sphere of OuterRadius (height OuterSpreadY x radius) around the player; Ring = a band near the rim of OuterRadius"),
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
                case "OuterShape": return OuterShape.ToString();
                case "OuterRadius": return FloatList.Format(OuterRadius);
                case "OuterAlpha": return FloatList.Format(OuterAlpha);
                case "OuterRate": return FloatList.Format(OuterRate);
                case "OuterRateDistance": return FloatList.Format(OuterRateDistance);
                case "OuterSize": return FloatList.Format(OuterSize);
                case "OuterRotation": return FloatList.Format(OuterRotation);
                case "OuterOffsetY": return FloatList.Format(OuterOffsetY);
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
                case "OuterShape":
                    if (!TryParseOuterShape(text, out var shape)) return false;
                    OuterShape = shape;
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
                case "OuterRateDistance": OuterRateDistance = Math.Max(0f, v); return true;
                case "OuterSize": OuterSize = Math.Max(0.01f, v); return true;
                case "Emission": Emission = Clamp01(v); return true;
                case "DistortionStrength": DistortionStrength = Math.Max(0f, v); return true;
                case "DistortionWave": DistortionWave = v < 0f ? -1f : v; return true;
                case "MeshOffset": MeshOffset = Math.Max(0f, v); return true;
                case "MeshRate": MeshRate = Math.Max(0f, v); return true;
                case "OuterSpreadY": OuterSpreadY = Math.Max(0.01f, v); return true;
                case "OuterLifetime": OuterLifetime = Math.Max(0.05f, v); return true;
                case "OuterRotation": OuterRotation = v < -MaxOuterRotation ? -MaxOuterRotation : v > MaxOuterRotation ? MaxOuterRotation : v; return true;
                case "OuterOffsetY": OuterOffsetY = v < -2f ? -2f : v > 2f ? 2f : v; return true;
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

    /// <summary>
    /// Round M ruling 2d: checks the final fog material values against the configured colour. Overlapping lit sprites saturate,
    /// so _Color rgb must not be above the configured colour and _EmissionColor must be black unless Emission is configured.
    /// </summary>
    public static class FogMaterialCheck
    {
        /// <summary>Blend/alpha properties FogVeil logs when the shader declares them (never changed).</summary>
        public static readonly string[] BlendProperties = { "_BlendOp", "_SrcBlend", "_DstBlend", "_AlphaChannel", "_ZWrite", "_Mode" };

        private const float Epsilon = 0.002f;

        public static List<string> Problems(FogRgb configured, FogRgb color, FogRgb emission, float emissionSetting)
        {
            var problems = new List<string>();
            if (color.R > configured.R + Epsilon || color.G > configured.G + Epsilon || color.B > configured.B + Epsilon)
                problems.Add($"_Color rgb {FloatList.Format(color.R, color.G, color.B)} is above the configured {FloatList.Format(configured.R, configured.G, configured.B)}");
            if (emissionSetting <= 0f && (emission.R > Epsilon || emission.G > Epsilon || emission.B > Epsilon))
                problems.Add($"_EmissionColor rgb {FloatList.Format(emission.R, emission.G, emission.B)} is not black at Emission 0");
            return problems;
        }
    }

    /// <summary>
    /// Alpha of the generated "soft" fog sprite (round L ruling 5), white rgb. Falloff: a gaussian exp(-k r^2) shifted and scaled
    /// so it is <see cref="CentreAlpha"/> at the centre (r = 0) and exactly 0 at the edge (r = 1) and beyond:
    /// alpha(r) = CentreAlpha x (exp(-k r^2) - exp(-k)) / (1 - exp(-k)), k = <see cref="Sharpness"/>. Noise: smooth value noise
    /// (random values on a <see cref="NoiseCells"/> lattice, bilinear with smoothstep) multiplies the falloff by 1 +- NoiseAmount, so
    /// overlapping particles (each with its own random rotation) do not stack into visible circles. Result clamped to CentreAlpha.
    /// Pixel (x, y) of a size x size sprite maps to -1..1 corner to corner, so the border pixels have r &gt;= 1 and alpha 0.
    /// </summary>
    public static class FogSprite
    {
        public const int Size = 128;
        public const float CentreAlpha = 0.8f;
        public const float Sharpness = 4f;
        public const float NoiseAmount = 0.1f;
        public const int NoiseCells = 6;
        private const int Seed = 7919;

        public static float Falloff(float r)
        {
            if (r >= 1f) return 0f;
            if (r < 0f) r = 0f;
            var edge = Math.Exp(-Sharpness);
            return CentreAlpha * (float)((Math.Exp(-Sharpness * r * r) - edge) / (1.0 - edge));
        }

        /// <summary>Distance of pixel (x, y) from the sprite centre, 1 at the middle of each border.</summary>
        public static float Radius(int x, int y, int size)
        {
            var half = (size - 1) * 0.5f;
            var dx = (x - half) / half;
            var dy = (y - half) / half;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Smooth value noise in -1..1 at pixel (x, y), deterministic.</summary>
        public static float Noise(int x, int y, int size)
        {
            var fx = (float)x / Math.Max(1, size - 1) * NoiseCells;
            var fy = (float)y / Math.Max(1, size - 1) * NoiseCells;
            var ix = (int)Math.Floor(fx);
            var iy = (int)Math.Floor(fy);
            var tx = Smooth(fx - ix);
            var ty = Smooth(fy - iy);
            var a = Lattice(ix, iy) + (Lattice(ix + 1, iy) - Lattice(ix, iy)) * tx;
            var b = Lattice(ix, iy + 1) + (Lattice(ix + 1, iy + 1) - Lattice(ix, iy + 1)) * tx;
            return a + (b - a) * ty;
        }

        /// <summary>Final alpha of pixel (x, y): Falloff x (1 + NoiseAmount x Noise), clamped to 0..CentreAlpha.</summary>
        public static float Alpha(int x, int y, int size)
        {
            var f = Falloff(Radius(x, y, size));
            if (f <= 0f) return 0f;
            var a = f * (1f + NoiseAmount * Noise(x, y, size));
            return a < 0f ? 0f : a > CentreAlpha ? CentreAlpha : a;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>Hash of a lattice point to -1..1.</summary>
        private static float Lattice(int x, int y)
        {
            unchecked
            {
                var h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)Seed;
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFF) / 32767.5f - 1f;
            }
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
