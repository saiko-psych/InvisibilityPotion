using System;
using System.Collections.Generic;

namespace InvisibilityPotion.Config
{
    /// <summary>Per-tier values as plain data. Built from BepInEx config entries by PluginConfig; no game types here.</summary>
    public sealed class TierConfig
    {
        public int Tier;
        public float Duration;
        public float StealthModifier = 1f;   // fraction of vanilla visibility, 1 = unchanged
        public float NoiseModifier = 1f;     // fraction of vanilla noise, 1 = unchanged
        public bool IgnoredByEnemies;
        public bool HiddenFromPlayers;       // other players cannot see position, model or nameplate
        public float AggroLossTime;
        public float RehideDelay;            // <= 0: an attack ends the effect
        public float DebuffStaminaRegenMultiplier = 1f;
        public float DebuffDuration;
        public float Cooldown;
        public string Recipe = "";
        public string BodyVeilMode = "Off";  // Off, Cutoff, Hide, Tint, Ghost, Distortion
        public bool FogEnabled;
        public float FogDensity;             // 0..1, scales fog rate and alpha

        public bool EndsOnReveal => RehideDelay <= 0f;

        public void Validate()
        {
            if (Tier < 1 || Tier > 3) throw new ArgumentOutOfRangeException(nameof(Tier), Tier, "tier must be 1..3");
            if (Duration <= 0f) throw new ArgumentOutOfRangeException(nameof(Duration), Duration, "duration must be > 0");
            if (AggroLossTime < 0f) throw new ArgumentOutOfRangeException(nameof(AggroLossTime));
            if (DebuffDuration < 0f) throw new ArgumentOutOfRangeException(nameof(DebuffDuration));
        }
    }

    public sealed class GlobalConfig
    {
        public bool RevealOnDamage = true;
        public bool RevealOnBlock = true;
        public bool RevealOnBowDraw = true;
        public bool ShowSelfFaintly = true;
        public float FogCutoffLight = 0.5f;
        public float FogCutoffDense = 0.8f;
        public float FogCutoffSelf = 0.3f;
        public float DistortionStrength = 0.2f;
        public string DistortionColor = "1,1,1,0.15";
        public string GhostColor = "0.75,0.8,0.9,1";
        public float GhostEmission = 0.25f;
        public bool AllowPvpInvisibility = true;
    }

    public static class ModifierMath
    {
        /// <summary>Vanilla SE_Stats modifiers are additive fractions of the base value (stealth += base * m). A config fraction f of vanilla maps to m = f - 1.</summary>
        public static float ToAdditive(float fraction) => fraction - 1f;
    }

    public static class RecipeParser
    {
        /// <summary>Parses "Item:Amount,Item:Amount". Empty entries are ignored. Throws FormatException on anything else.</summary>
        public static IReadOnlyList<(string item, int amount)> Parse(string recipe)
        {
            var result = new List<(string, int)>();
            if (string.IsNullOrWhiteSpace(recipe)) return result;
            foreach (var raw in recipe.Split(','))
            {
                var entry = raw.Trim();
                if (entry.Length == 0) continue;
                var colon = entry.IndexOf(':');
                if (colon <= 0) throw new FormatException($"recipe entry '{entry}' must be Item:Amount");
                var item = entry.Substring(0, colon).Trim();
                var amountText = entry.Substring(colon + 1).Trim();
                if (item.Length == 0 || !int.TryParse(amountText, out var amount) || amount <= 0)
                    throw new FormatException($"recipe entry '{entry}' must be Item:Amount with Amount > 0");
                result.Add((item, amount));
            }
            return result;
        }
    }
}
