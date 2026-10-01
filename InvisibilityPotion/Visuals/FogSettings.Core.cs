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

    /// <summary>How the fog alpha is split between the material colour and the particle (vertex) colour.</summary>
    public enum FogAlphaMode { Both, Material, Vertex }

    /// <summary>Look of the body-anchored fog, shared by all veiled players. Density (per tier) scales rate and alpha.</summary>
    public sealed class FogSettings
    {
        /// <summary>Density at which FogAlpha applies unchanged (tier I default).</summary>
        public const float ReferenceDensity = 0.35f;

        public static readonly string[] AnchorNames = { "Head", "Chest", "Hips", "LeftHand", "RightHand", "LeftFoot", "RightFoot" };

        public float Rate = 12f;        // particles per second per emitter at density 1
        public float Size = 0.4f;       // metres; start size is randomised in [0.6, 1.25] x Size
        public float Lifetime = 1.5f;   // seconds; randomised in [0.8, 1.2] x Lifetime
        public float Speed = 0.05f;     // start speed, m/s
        public float Alpha = 0.12f;     // alpha at ReferenceDensity, scaled linearly with density
        public float R = 0.82f, G = 0.84f, B = 0.88f;
        public float Drift = 0.15f;     // upward drift, m/s (world space)
        public FogAlphaMode AlphaMode = FogAlphaMode.Both;
        public readonly List<FogAnchor> Anchors = new List<FogAnchor>();

        public static FogSettings Defaults()
        {
            var s = new FogSettings();
            foreach (var name in AnchorNames) s.Anchors.Add(DefaultAnchor(name));
            return s;
        }

        public static FogAnchor DefaultAnchor(string name)
        {
            switch (name)
            {
                case "Head": return new FogAnchor { Name = name, Radius = 0.15f, Y = 0.05f };
                case "Chest": return new FogAnchor { Name = name, Radius = 0.25f };
                case "Hips": return new FogAnchor { Name = name, Radius = 0.22f };
                case "LeftHand": case "RightHand": return new FogAnchor { Name = name, Radius = 0.1f };
                case "LeftFoot": case "RightFoot": return new FogAnchor { Name = name, Radius = 0.1f, Y = 0.05f };
                default: return new FogAnchor { Name = name };
            }
        }

        public FogSettings Clone()
        {
            var copy = new FogSettings
            {
                Rate = Rate, Size = Size, Lifetime = Lifetime, Speed = Speed, Alpha = Alpha,
                R = R, G = G, B = B, Drift = Drift, AlphaMode = AlphaMode,
            };
            foreach (var a in Anchors) copy.Anchors.Add(a.Clone());
            return copy;
        }

        public FogAnchor Anchor(string name)
        {
            foreach (var a in Anchors)
                if (string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)) return a;
            return null;
        }

        public float EffectiveAlpha(float density)
        {
            var a = Alpha * Math.Max(0f, density) / ReferenceDensity;
            return a < 0f ? 0f : a > 1f ? 1f : a;
        }

        public static bool TryParseAlphaMode(string text, out FogAlphaMode mode) =>
            Enum.TryParse((text ?? "").Trim(), true, out mode) && Enum.IsDefined(typeof(FogAlphaMode), mode);
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
