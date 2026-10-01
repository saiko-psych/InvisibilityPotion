using System;
using System.Collections.Generic;

namespace InvisibilityPotion.Items
{
    /// <summary>Which vanilla mead lends its drink/expire effects (sound and particle burst) to the invisibility potions.</summary>
    public static class PotionVfx
    {
        public const string DefaultSource = "MeadFrostResist";

        /// <summary>Fallbacks after the configured mead: frost resist (bluish-white burst), tasty mead, finally the minor health mead.</summary>
        public static readonly string[] Fallbacks = { "MeadFrostResist", "MeadTasty", "MeadHealthMinor" };

        /// <summary>Prefab names to try in order: the configured one (if any), then <see cref="Fallbacks"/>, without duplicates.</summary>
        public static List<string> Candidates(string configured)
        {
            var list = new List<string>();
            var c = (configured ?? "").Trim();
            if (c.Length > 0) list.Add(c);
            foreach (var f in Fallbacks)
                if (!list.Exists(x => string.Equals(x, f, StringComparison.OrdinalIgnoreCase))) list.Add(f);
            return list;
        }
    }
}
