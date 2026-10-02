using System;
using System.Collections.Generic;

namespace InvisibilityPotion.Plants
{
    /// <summary>
    /// [Plants] defaults that changed after release candidates went out, no game types. Once per file ([General]
    /// PlantDefaultsRevision below <see cref="Revision"/>) a key still at an old default moves to the new default; values the admin
    /// changed stay.
    /// </summary>
    public static class PlantDefaults
    {
        /// <summary>1: zone chances raised (user 2026-10-03): Baldr's Tear 1/6 -> 1/5, Hel's Ember Fern 1/15 -> 1/10.</summary>
        public const int Revision = 1;

        public const float BaldrZoneChance = 0.2f;
        public const float HelFernZoneChance = 0.1f;

        /// <summary>key -> (revision that introduced the new default, old default, new default).</summary>
        private static readonly Dictionary<string, (int rev, float oldValue, float newValue)> Changes =
            new Dictionary<string, (int, float, float)>
            {
                ["BaldrZoneChance"] = (1, 0.167f, BaldrZoneChance),
                ["HelFernZoneChance"] = (1, 0.0667f, HelFernZoneChance),
            };

        public static IEnumerable<string> Keys => Changes.Keys;

        /// <summary>
        /// The value <paramref name="key"/> should hold after migrating a file at <paramref name="fileRevision"/>: the new default
        /// when the stored value equals the old default (within 1e-4, config files round-trip floats as text) and the change is newer
        /// than the file; otherwise <paramref name="stored"/>.
        /// </summary>
        public static float Migrate(string key, float stored, int fileRevision)
        {
            if (key == null || !Changes.TryGetValue(key, out var c)) return stored;
            if (fileRevision >= c.rev) return stored;
            return Math.Abs(stored - c.oldValue) < 1e-4f ? c.newValue : stored;
        }
    }
}
