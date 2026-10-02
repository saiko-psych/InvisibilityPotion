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
        /// <summary>Veil Broken: SE_Stats.m_speedModifier, additive fraction of the base speed (-0.3 = 30 % slower); 0 = none.</summary>
        public float DebuffSpeedModifier;
        /// <summary>Veil Broken: SE_Stats.m_eitrRegenMultiplier (multiplies at or below 1); 1 = none.</summary>
        public float DebuffEitrRegenMultiplier = 1f;
        /// <summary>Veil Broken: SE_Stats.m_healthRegenMultiplier (multiplies at or below 1); 1 = none.</summary>
        public float DebuffHealthRegenMultiplier = 1f;
        public float DebuffDuration;
        public float Cooldown;
        public string Recipe = "";
        public float CarryWeightMultiplier = 1f;   // max carry weight while the effect is active, 1 = off
        public string BodyVeilMode = "Off";  // Off, Cutoff, Hide, Tint, Ghost, Distortion, Shadow, Spirit; the fog look lives in [Fog.TierN]

        public bool EndsOnReveal => RehideDelay <= 0f;

        public void Validate()
        {
            if (Tier < 1 || Tier > 3) throw new ArgumentOutOfRangeException(nameof(Tier), Tier, "tier must be 1..3");
            if (Duration <= 0f) throw new ArgumentOutOfRangeException(nameof(Duration), Duration, "duration must be > 0");
            if (AggroLossTime < 0f) throw new ArgumentOutOfRangeException(nameof(AggroLossTime));
            if (DebuffDuration < 0f) throw new ArgumentOutOfRangeException(nameof(DebuffDuration));
            CheckMultiplier(nameof(DebuffStaminaRegenMultiplier), DebuffStaminaRegenMultiplier);
            CheckMultiplier(nameof(DebuffEitrRegenMultiplier), DebuffEitrRegenMultiplier);
            CheckMultiplier(nameof(DebuffHealthRegenMultiplier), DebuffHealthRegenMultiplier);
            if (!(DebuffSpeedModifier >= GameplayDefaults.DebuffSpeedMin && DebuffSpeedModifier <= GameplayDefaults.DebuffSpeedMax))
                throw new ArgumentOutOfRangeException(nameof(DebuffSpeedModifier), DebuffSpeedModifier,
                    $"speed modifier must be {GameplayDefaults.DebuffSpeedMin}..{GameplayDefaults.DebuffSpeedMax}");
            if (!(CarryWeightMultiplier >= 0f && CarryWeightMultiplier <= 1f))
                throw new ArgumentOutOfRangeException(nameof(CarryWeightMultiplier), CarryWeightMultiplier, "carry weight multiplier must be 0..1");
        }

        private static void CheckMultiplier(string name, float value)
        {
            if (!(value >= 0f && value <= GameplayDefaults.DebuffMultiplierMax))
                throw new ArgumentOutOfRangeException(name, value, $"multiplier must be 0..{GameplayDefaults.DebuffMultiplierMax}");
        }
    }

    public sealed class GlobalConfig
    {
        public bool RevealOnDamage = true;
        public bool RevealOnBlock = true;
        public bool RevealOnBowDraw = true;
        public bool RevealOnToolUse = true;   // axe/pickaxe swings, hammer/hoe/cultivator placement
        public bool DrainStaminaOnAttackReveal = true;   // round Q: own attack that breaks the veil empties the stamina bar
        public bool ShowSelfFaintly = true;
        public float FogCutoffLight = 0.5f;
        public float FogCutoffDense = 0.8f;
        public float FogCutoffSelf = 0.3f;
        public string GhostColor = "0.75,0.8,0.9,1";
        public float GhostEmission = 0.25f;
        public string ShadowColor = "0,0,0,0.12";
        public string SpiritColor = "0.6,0.7,0.8";
        public float SpiritStrength = 0.25f;
        public bool AllowPvpInvisibility = true;
    }

    /// <summary>
    /// Revisions of the server-synced [TierN] gameplay defaults (pure; PluginConfig applies it once per file). A key still at its
    /// old default moves to the new one; a value the admin changed stays. Revision 1 (round Q): DebuffStaminaRegenMultiplier
    /// 0.5 -> 0.25, DebuffDuration 20 -> 15. Revision 2 (round Q): Cooldown 0 (reserved, unused before) -> 90/180/240 s. Revision 3 (user, 2026-10-02 23:20): 30/60/90 s, shorter than the
    /// tier durations so a higher mead can still replace a running veil near its end (the upgrade stays possible). Revision 4
    /// (user, 2026-10-02 23:32, round R): a harsher Veil Broken: DebuffStaminaRegenMultiplier 0.25 -> 0.15; the new keys
    /// DebuffSpeedModifier, DebuffEitrRegenMultiplier and DebuffHealthRegenMultiplier need no migration (absent keys get their default).
    /// </summary>
    public static class GameplayDefaults
    {
        public const int Revision = 4;

        /// <summary>[TierN] Cooldown default: the shared Veil Cooldown started by drinking tier N (seconds).</summary>
        public static float Cooldown(int tier)
        {
            switch (tier)
            {
                case 1: return 30f;
                case 2: return 60f;
                case 3: return 90f;
                default: return 0f;
            }
        }

        /// <summary>The revision-2 cooldown defaults (90/180/240), migrated to the revision-3 values when still untouched.</summary>
        public static float Revision2Cooldown(int tier) => tier == 1 ? 90f : tier == 2 ? 180f : tier == 3 ? 240f : 0f;

        public const float DebuffStaminaRegenMultiplier = 0.15f;
        /// <summary>The revision 1..3 default of DebuffStaminaRegenMultiplier, migrated to the revision-4 value when still untouched.</summary>
        public const float Revision3DebuffStaminaRegenMultiplier = 0.25f;
        public const float DebuffEitrRegenMultiplier = 0.25f;
        public const float DebuffHealthRegenMultiplier = 0.5f;
        public const float DebuffDuration = 15f;
        /// <summary>Allowed range of the Veil Broken regen multipliers (config and Validate).</summary>
        public const float DebuffMultiplierMax = 5f;
        /// <summary>Allowed range of DebuffSpeedModifier (additive fraction; -0.9 = 90 % slower).</summary>
        public const float DebuffSpeedMin = -0.9f;
        public const float DebuffSpeedMax = 1f;

        /// <summary>[TierN] DebuffSpeedModifier default: tier I -0.2, tier II/III -0.3.</summary>
        public static float DebuffSpeedModifier(int tier) => tier == 1 ? -0.2f : -0.3f;

        /// <summary>The value of [Tier<paramref name="tier"/>] <paramref name="key"/> after migrating a file from <paramref name="fromRevision"/>.</summary>
        public static float Migrate(string key, int tier, float current, int fromRevision)
        {
            // Steps run in order, so an old file moves through every revision (0.5 -> 0.25 -> 0.15).
            if (fromRevision < 1)
            {
                if (key == "DebuffStaminaRegenMultiplier" && Same(current, 0.5f)) current = Revision3DebuffStaminaRegenMultiplier;
                if (key == "DebuffDuration" && Same(current, 20f)) current = DebuffDuration;
            }
            if (fromRevision < 2 && key == "Cooldown" && Same(current, 0f)) return Cooldown(tier);
            if (fromRevision < 3 && key == "Cooldown" && Same(current, Revision2Cooldown(tier))) return Cooldown(tier);
            if (fromRevision < 4 && key == "DebuffStaminaRegenMultiplier" && Same(current, Revision3DebuffStaminaRegenMultiplier)) return DebuffStaminaRegenMultiplier;
            return current;
        }

        private static bool Same(float a, float b) => Math.Abs(a - b) < 0.0001f;
    }

    /// <summary>
    /// Round Q ruling 3: the potion cooldown, mirroring vanilla (a status effect that lives for the cooldown and blocks the
    /// consume in Player.CanConsumeItem). One shared "Veil Cooldown" blocks all three veil meads. Pure.
    /// </summary>
    public static class VeilCooldown
    {
        /// <summary>Drinking starts a cooldown only for a positive, finite [TierN] Cooldown (0 = off).</summary>
        public static bool Starts(float cooldown) => !float.IsNaN(cooldown) && !float.IsInfinity(cooldown) && cooldown > 0f;

        /// <summary>A veil mead is refused while the shared cooldown has time left; other items never.</summary>
        public static bool Refuses(bool incomingIsVeil, float cooldownRemaining) => incomingIsVeil && cooldownRemaining > 0f;
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
