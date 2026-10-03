using System;
using System.Collections.Generic;

namespace InvisibilityPotion.Plants
{
    /// <summary>
    /// [Plants] defaults that changed after release candidates went out, no game types. Once per file ([General]
    /// PlantDefaultsRevision below <see cref="Revision"/>) a key still at an old default moves to the new default; values the admin
    /// changed stay. Changes chain: a file at revision 0 walks every later step in order, so an untouched value ends at the current
    /// default.
    /// </summary>
    public static class PlantDefaults
    {
        /// <summary>
        /// 1: zone chances raised (user 2026-10-03): Baldr's Tear 1/6 -> 1/5, Hel's Ember Fern 1/15 -> 1/10.
        /// 2 (0.4.0): both 1/2, the rate of vanilla carrot and turnip seeds (pickable_seedcarrot_blackforest / pickable_seedturnip_swamp:
        /// m_max 0.5, group 1-2 in 5 m; bundle d59cfac, UnityPy) at the user's request.
        /// </summary>
        public const int Revision = 2;

        public const float BaldrZoneChance = 0.5f;
        public const float HelFernZoneChance = 0.5f;

        /// <summary>key -> steps (revision that introduced the new default, old default, new default), oldest first.</summary>
        private static readonly Dictionary<string, (int rev, float oldValue, float newValue)[]> Changes =
            new Dictionary<string, (int, float, float)[]>
            {
                ["BaldrZoneChance"] = new[] { (1, 0.167f, 0.2f), (2, 0.2f, BaldrZoneChance) },
                ["HelFernZoneChance"] = new[] { (1, 0.0667f, 0.1f), (2, 0.1f, HelFernZoneChance) },
            };

        public static IEnumerable<string> Keys => Changes.Keys;

        /// <summary>
        /// The value <paramref name="key"/> should hold after migrating a file at <paramref name="fileRevision"/>: every step newer
        /// than the file is applied in order, and a step applies only when the current value equals its old default (within 1e-4,
        /// config files round-trip floats as text). A customised value never matches a step, so it is kept.
        /// </summary>
        public static float Migrate(string key, float stored, int fileRevision)
        {
            if (key == null || !Changes.TryGetValue(key, out var steps)) return stored;
            var value = stored;
            foreach (var step in steps)
            {
                if (fileRevision >= step.rev) continue;
                if (Math.Abs(value - step.oldValue) < 1e-4f) value = step.newValue;
            }
            return value;
        }
    }
}
