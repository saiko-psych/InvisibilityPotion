namespace InvisibilityPotion.Effects
{
    /// <summary>Carry-weight penalty while an invisibility tier is active. Pure; applied by the Player.GetMaxCarryWeight postfix.</summary>
    public static class CarryWeight
    {
        /// <summary>The final limit (base, every status effect bonus such as Megingjord, and the world carry-weight rate) times the multiplier, clamped to 0..1 so it never raises the limit or goes negative. NaN counts as 1 (off).</summary>
        public static float Reduced(float limit, float multiplier)
        {
            if (float.IsNaN(multiplier) || multiplier >= 1f) return limit;
            if (multiplier <= 0f) return 0f;
            return limit * multiplier;
        }
    }
}
