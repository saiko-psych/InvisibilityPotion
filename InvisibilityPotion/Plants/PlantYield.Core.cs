using System;
using System.Globalization;

namespace InvisibilityPotion.Plants
{
    /// <summary>
    /// Yield and size rules of the hidden plants (user decisions 2026-10-02), no game types. [Plants] YieldTN is a "min-max" range
    /// rolled per pick; ground plants have a random uniform size per instance, and the yield grows mildly with it:
    /// yield = round(roll × (0.75 + 0.5 × normalizedScale)), at least 1, where normalizedScale ∈ [0, 1] across the plant's range.
    /// </summary>
    public static class PlantYield
    {
        public static readonly (int min, int max)[] DefaultYields = { (0, 0), (1, 2), (1, 1), (2, 4) };
        /// <summary>Size ranges per tier: Huldra's Hair has none (1-1), Baldr's Tear 0.8-1.3, Hel's Ember Fern 0.7-1.6.</summary>
        public static readonly (float min, float max)[] ScaleRanges = { (1f, 1f), (1f, 1f), (0.8f, 1.3f), (0.7f, 1.6f) };

        public static string DefaultYieldText(int tier) => $"{DefaultYields[CheckTier(tier)].min}-{DefaultYields[CheckTier(tier)].max}";

        /// <summary>
        /// Parses "min-max" or a single "n" (= n-n). Whitespace allowed; min and max are swapped when reversed; values below 1 are
        /// raised to 1. Returns false on anything else (the caller falls back to the default).
        /// </summary>
        public static bool TryParseRange(string text, out int min, out int max)
        {
            min = max = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var parts = text.Split('-');
            if (parts.Length > 2) return false;
            if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out min)) return false;
            if (parts.Length == 1) max = min;
            else if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out max)) return false;
            if (min > max) { var t = min; min = max; max = t; }
            if (min < 1) min = 1;
            if (max < 1) max = 1;
            return true;
        }

        /// <summary>Uniform integer in [min, max] from a value in [0, 1).</summary>
        public static int Roll(int min, int max, double random01)
        {
            if (max < min) { var t = min; min = max; max = t; }
            if (double.IsNaN(random01) || random01 < 0) random01 = 0;
            var n = min + (int)Math.Floor(random01 * (max - min + 1));
            return n > max ? max : n;
        }

        /// <summary>0..1 position of <paramref name="scale"/> in [min, max]; 0.5 when the range is empty.</summary>
        public static double NormalizedScale(float scale, float min, float max)
        {
            if (!(max > min)) return 0.5;
            var n = (scale - min) / (double)(max - min);
            return n < 0 ? 0 : n > 1 ? 1 : n;
        }

        /// <summary>round(roll × (0.75 + 0.5 × normalizedScale)), half away from zero, at least 1.</summary>
        public static int Scaled(int roll, double normalizedScale)
        {
            if (double.IsNaN(normalizedScale)) normalizedScale = 0.5;
            normalizedScale = normalizedScale < 0 ? 0 : normalizedScale > 1 ? 1 : normalizedScale;
            var y = (int)Math.Round(roll * (0.75 + 0.5 * normalizedScale), MidpointRounding.AwayFromZero);
            return y < 1 ? 1 : y;
        }

        /// <summary>A uniform size in the tier's range from a value in [0, 1).</summary>
        public static float RollScale(int tier, double random01)
        {
            var (min, max) = ScaleRanges[CheckTier(tier)];
            if (double.IsNaN(random01) || random01 < 0) random01 = 0;
            if (random01 > 1) random01 = 1;
            return (float)(min + (max - min) * random01);
        }

        private static int CheckTier(int tier)
        {
            if (tier < 1 || tier > 3) throw new ArgumentOutOfRangeException(nameof(tier));
            return tier;
        }
    }
}
