namespace InvisibilityPotion.Effects
{
    /// <summary>Stamina-regeneration debuff applied on every reveal. Values come from the tier that revealed; the effect is one shared prefab, so the values are set on the clone in Setup from the static NextMultiplier/NextDuration.</summary>
    public class SE_Revealed : SE_Stats
    {
        /// <summary>Set by SE_Invisibility right before AddStatusEffect; read once in Setup. Single-threaded game loop, and only the local player's effects are simulated on a machine, so the static handoff is safe.</summary>
        public static float NextMultiplier = 0.5f;
        public static float NextDuration = 20f;

        public override void Setup(Character character)
        {
            m_staminaRegenMultiplier = NextMultiplier;
            m_ttl = NextDuration;
            base.Setup(character);
        }

        public override void ResetTime()
        {
            m_staminaRegenMultiplier = NextMultiplier;
            m_ttl = NextDuration;
            base.ResetTime();
        }
    }
}
