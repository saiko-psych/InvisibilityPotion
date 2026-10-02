using System;

namespace InvisibilityPotion.Goggles
{
    /// <summary>
    /// Goggle level rules (spec plan 5 §3.5), no game types. Level N (VeilGoggles_TN in the helmet slot) reveals plants of tier
    /// &lt;= N; level III also reveals players hidden by tier III when the server switch allows it.
    /// </summary>
    public static class GoggleLevel
    {
        public const int Max = 3;
        public const string PrefabPrefix = "VeilGoggles_T";

        /// <summary>
        /// Level of a helmet by its drop prefab name: VeilGoggles_T1..T3 -> 1..3, anything else (null, other helmets, other case,
        /// suffixes such as "(Clone)") -> 0. Prefab names are exact (ObjectDB/ZNetScene look them up by ordinal name), so the
        /// comparison is case-sensitive on purpose.
        /// </summary>
        public static int LevelOf(string dropPrefabName)
        {
            if (string.IsNullOrEmpty(dropPrefabName) || dropPrefabName.Length != PrefabPrefix.Length + 1) return 0;
            if (!dropPrefabName.StartsWith(PrefabPrefix, StringComparison.Ordinal)) return 0;
            var digit = dropPrefabName[dropPrefabName.Length - 1] - '0';
            return digit >= 1 && digit <= Max ? digit : 0;
        }

        /// <summary>Clamps any value (dev override, ZDO int from a peer) into 0..3.</summary>
        public static int Clamp(int level) => level < 0 ? 0 : level > Max ? Max : level;

        /// <summary>A plant that needs <paramref name="required"/> is visible at <paramref name="level"/>. Required &lt;= 0 means always.</summary>
        public static bool Reveals(int level, int required) => required <= 0 || Clamp(level) >= required;

        private static readonly string[] Recipes =
        {
            null,
            "Bronze:5,Resin:4,TrollHide:2",
            "Silver:5,Crystal:2,WolfPelt:3,VeilGoggles_T1:1",
            "FlametalNew:5,BlackCore:1,Obsidian:2,VeilGoggles_T2:1",
        };

        /// <summary>Default [Goggles] RecipeTN (spec §2): II and III consume the previous goggles.</summary>
        public static string DefaultRecipe(int tier) => Recipes[CheckTier(tier)];

        /// <summary>Crafting station prefab name: forge for I and II, blackforge (Galdr table's forge, Mistlands) for III.</summary>
        public static string Station(int tier) => CheckTier(tier) == 3 ? "blackforge" : "forge";

        private static int CheckTier(int tier)
        {
            if (tier < 1 || tier > Max) throw new ArgumentOutOfRangeException(nameof(tier));
            return tier;
        }

        /// <summary>Level III cancels "hidden from players" for the wearer, if the server switch [Goggles] RevealHiddenPlayers is on.</summary>
        public static bool SeesHiddenPlayers(int level, bool revealSwitch) => revealSwitch && Clamp(level) >= Max;
    }
}
