namespace InvisibilityPotion.Effects
{
    /// <summary>
    /// "Veil Cooldown" (round Q ruling 3), shared by all three veil meads. Mirrors the vanilla potion cooldown: vanilla keeps the
    /// consume status effect alive for the whole cooldown and refuses another drink in Player.CanConsumeItem while it (or its
    /// m_category) is present, with the status-bar timer (GetIconText) and the cooldown overlay (m_cooldownIcon). Ours is a separate
    /// effect because the veil can end before the cooldown (tier I on reveal) and a category on SE_Invisibility would block tier
    /// upgrades; CanConsumeItemPatch does the refusal. Added only from SE_Invisibility.UpdateStatusEffect.
    /// </summary>
    public class SE_VeilCooldown : StatusEffect
    {
        /// <summary>Set by SE_Invisibility right before AddStatusEffect: the drunk tier's [TierN] Cooldown. Read in Setup/ResetTime.</summary>
        public static float NextDuration = 90f;

        public override void Setup(Character character)
        {
            m_ttl = NextDuration;
            base.Setup(character);
        }

        public override void ResetTime()
        {
            m_ttl = NextDuration;
            base.ResetTime();
        }
    }
}
