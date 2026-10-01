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

    /// <summary>One config key of a per-tier fog section ([Fog.TierN]).</summary>
    public sealed class FogKey
    {
        public readonly string Name;
        public readonly FogValueKind Kind;
        public readonly string Description;

        public FogKey(string name, FogValueKind kind, string description) { Name = name; Kind = kind; Description = description; }
    }

    /// <summary>
    /// Per-tier look of the veil: body-anchored fog (inner layer plus an optional wider, fainter outer layer) and the tier's
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
        public bool DynamicColor = true; // blend 50/50 with the environment fog colour
        public float SpreadX = 1f, SpreadY = 0.35f, SpreadZ = 1f;   // emitter shape scale: flattened, spreads sideways
        public float Drift = 0f;         // vertical drift, m/s (world space); 0 = no plume
        /// <summary>Inner layer in world space: particles stay where they were emitted (trail behind a moving player).</summary>
        public bool Trail;
        /// <summary>Mesh emitter: particles per second on the body surface; 0 = Rate x enabled anchors.</summary>
        public float MeshRate;
        public bool OuterEnabled;
        public float OuterRadiusMultiplier = 2.5f;
        public float OuterAlphaFactor = 0.35f;
        public float OuterRateFactor = 0.6f;
        public float OuterSizeFactor = 1.5f;
        /// <summary>Outer layer vertical shape scale (sideways it uses SpreadX/SpreadZ); below 1 flattens the ring.</summary>
        public float OuterSpreadY = 0.35f;
        public float OuterLifetimeFactor = 1f;
        public bool OuterTrail;
        /// <summary>Bones the outer layer spawns on (one emitter each, anchor radius x OuterRadiusMultiplier), whatever the emitter mode.</summary>
        public List<string> OuterAnchors = new List<string>(DefaultOuterAnchors);
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

        public const string DefaultFogMaterial = "swamp_mist";

        /// <summary>
        /// Selectable fog materials (config names). slowwispysmoke stands for the Ghost's slowwispysmoke_gradient_alphablend;
        /// FogVeil maps each name to the vanilla material and its source prefab.
        /// </summary>
        public static readonly string[] FogMaterialNames = { "swamp_mist", "ghost_smoke", "wraith_smoke", "slowwispysmoke" };

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
                    // Small wisps on the body surface that linger behind a moving player, plus a light shimmer (round F).
                    s.EmitterMode = FogEmitterMode.Mesh; s.MeshRate = 18f; s.Rate = 2.5f;
                    s.Size = 0.28f; s.Lifetime = 4f; s.Speed = 0.02f; s.Alpha = 0.3f;
                    s.R = 0.88f; s.G = 0.9f; s.B = 0.93f;
                    s.SpreadX = 1f; s.SpreadY = 0.5f; s.SpreadZ = 1f; s.Drift = 0.02f;
                    s.Trail = true;
                    s.DistortionStrength = 0.04f; s.DA = 0.03f;
                    break;
                case 2:
                    // Dense thin inner layer that follows the body, wide flat outer ring that trails (round F).
                    s.Rate = 10f; s.Size = 0.45f; s.Lifetime = 2f; s.Alpha = 0.4f;
                    s.OuterEnabled = true;
                    s.OuterAnchors = new List<string> { "Chest", "Hips", "Head", "LeftHand", "RightHand" };
                    s.OuterRadiusMultiplier = 6f; s.OuterSpreadY = 0.25f;
                    s.OuterAlphaFactor = 0.75f; s.OuterSizeFactor = 3.5f; s.OuterRateFactor = 0.5f;
                    s.OuterTrail = true; s.OuterLifetimeFactor = 1.5f;
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

        /// <summary>Lifetime of the outer layer's particles.</summary>
        public float OuterLifetime => Math.Max(0.05f, Lifetime * Math.Max(0f, OuterLifetimeFactor));

        /// <summary>Rate of the single Mesh emitter for <paramref name="enabledAnchors"/> enabled anchors: MeshRate, or Rate x anchors when MeshRate is 0.</summary>
        public float MeshEmitterRate(int enabledAnchors) => MeshRate > 0f ? MeshRate : Math.Max(0f, Rate) * Math.Max(0, enabledAnchors);

        /// <summary>Expected live particles of one inner emitter (rate × lifetime).</summary>
        public float LiveParticlesInner => Math.Max(0f, Rate) * Math.Max(0f, Lifetime);

        /// <summary>Expected live particles of one outer emitter (outer rate × outer lifetime).</summary>
        public float LiveParticlesOuter => OuterEnabled ? Math.Max(0f, Rate) * Math.Max(0f, OuterRateFactor) * OuterLifetime : 0f;

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
                                             || LiveParticlesOuter > ParticleWarnThreshold;

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
                new FogKey("DynamicColor", FogValueKind.Bool, "Blend the fog colour 50/50 with the environment's fog colour (day, night, weather)"),
                new FogKey("SpreadX", FogValueKind.Float, "Emitter shape scale sideways (multiplies each anchor's radius)"),
                new FogKey("SpreadY", FogValueKind.Float, "Emitter shape scale vertically; below 1 flattens the fog"),
                new FogKey("SpreadZ", FogValueKind.Float, "Emitter shape scale front/back"),
                new FogKey("Drift", FogValueKind.Float, "Vertical drift of fog particles in m/s (world space); 0 = no plume"),
                new FogKey("Trail", FogValueKind.Bool, "true = world space: fog particles stay where they were emitted, so moving leaves a trail; false = they follow the body"),
                new FogKey("MeshRate", FogValueKind.Float, "Mesh emitter: particles per second on the body surface; 0 = Rate x enabled anchors"),
                new FogKey("OuterEnabled", FogValueKind.Bool, "Second, wider fog layer on the OuterAnchors bones"),
                new FogKey("OuterAnchors", FogValueKind.Text, "Bones of the outer layer as a comma list of anchor names (Head, Chest, Hips, LeftShoulder, RightShoulder, LeftHand, RightHand, LeftUpperLeg, RightUpperLeg, LeftLowerLeg, RightLowerLeg, LeftFoot, RightFoot); radius and offset come from the Anchor.* key"),
                new FogKey("OuterRadiusMultiplier", FogValueKind.Float, "Outer layer radius as a multiple of the anchor radius"),
                new FogKey("OuterAlphaFactor", FogValueKind.Float, "Outer layer alpha as a fraction of Alpha"),
                new FogKey("OuterRateFactor", FogValueKind.Float, "Outer layer rate as a fraction of Rate"),
                new FogKey("OuterSizeFactor", FogValueKind.Float, "Outer layer particle size as a multiple of Size"),
                new FogKey("OuterSpreadY", FogValueKind.Float, "Outer layer vertical shape scale (sideways it uses SpreadX/SpreadZ); below 1 flattens the ring"),
                new FogKey("OuterLifetimeFactor", FogValueKind.Float, "Outer layer particle lifetime as a multiple of Lifetime"),
                new FogKey("OuterTrail", FogValueKind.Bool, "Outer layer in world space (leaves a trail), like Trail for the inner layer"),
                new FogKey("DistortionStrength", FogValueKind.Float, "Refraction strength when this tier's body mode is Distortion (shader property _RefractionIntensity)"),
                new FogKey("DistortionColor", FogValueKind.Text, "Colour of the Distortion body mode as r,g,b,a (shader property _Color)"),
                new FogKey("DistortionWave", FogValueKind.Float, "Ripple speed of the Distortion body mode (_WaveVel, normal map borrowed from staff_shield_shard); negative = the borrowed vanilla value"),
                new FogKey("FogMaterial", FogValueKind.Text, "Vanilla particle material of the fog: swamp_mist, ghost_smoke, wraith_smoke or slowwispysmoke"),
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
                case "OuterLifetimeFactor": return FloatList.Format(OuterLifetimeFactor);
                case "OuterTrail": return Bool(OuterTrail);
                case "OuterRadiusMultiplier": return FloatList.Format(OuterRadiusMultiplier);
                case "OuterAlphaFactor": return FloatList.Format(OuterAlphaFactor);
                case "OuterRateFactor": return FloatList.Format(OuterRateFactor);
                case "OuterSizeFactor": return FloatList.Format(OuterSizeFactor);
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
                case "OuterRadiusMultiplier": OuterRadiusMultiplier = Math.Max(0f, v); return true;
                case "OuterAlphaFactor": OuterAlphaFactor = Math.Max(0f, v); return true;
                case "OuterRateFactor": OuterRateFactor = Math.Max(0f, v); return true;
                case "OuterSizeFactor": OuterSizeFactor = Math.Max(0.01f, v); return true;
                case "DistortionStrength": DistortionStrength = Math.Max(0f, v); return true;
                case "DistortionWave": DistortionWave = v < 0f ? -1f : v; return true;
                case "MeshOffset": MeshOffset = Math.Max(0f, v); return true;
                case "MeshRate": MeshRate = Math.Max(0f, v); return true;
                case "OuterSpreadY": OuterSpreadY = Math.Max(0.01f, v); return true;
                case "OuterLifetimeFactor": OuterLifetimeFactor = Math.Max(0.05f, v); return true;
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
        public static bool TryParseOne(string text, out float value) =>
            float.TryParse((text ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

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
