namespace InvisibilityPotion.Config
{
    /// <summary>Pure rule for tier values read from replicated state: only 1..3 are valid tiers.</summary>
    public static class TierRange
    {
        public const int Min = 1;
        public const int Max = 3;

        public static bool IsValid(int tier) => tier >= Min && tier <= Max;

        /// <summary>Returns the tier when it is 1..3, otherwise 0 (= no tier).</summary>
        public static int Clamp(int tier) => IsValid(tier) ? tier : 0;
    }
}
