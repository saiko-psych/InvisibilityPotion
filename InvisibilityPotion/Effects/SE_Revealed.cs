namespace InvisibilityPotion.Effects
{
    /// <summary>
    /// "Veil Broken": the debuff applied on every reveal. Values come from the tier that revealed; the effect is one shared
    /// prefab, so the values are set on the clone in Setup/ResetTime from the static <see cref="Next"/>. Round R (user 23:32): a
    /// harsher debuff: stamina, eitr and health regeneration multipliers (SE_Stats.ModifyStaminaRegen/ModifyEitrRegen/
    /// ModifyHealthRegen multiply at or below 1) and a movement penalty (SE_Stats.ModifySpeed: speed += base x m_speedModifier).
    /// Vanilla SE_Stats.GetTooltipString lists every field that differs from neutral. Round R ruling B: the lifetime is
    /// RevealPenalty.DebuffSeconds (RehideDelay for tiers that re-hide, so it ends when the veil returns; DebuffDuration for tier I).
    /// Shows in the status bar with the se_veil_broken icon (SEMan.GetHUDStatusEffects needs m_icon) and the remaining time
    /// (StatusEffect.GetIconText: m_ttl - m_time).
    /// </summary>
    public class SE_Revealed : SE_Stats
    {
        /// <summary>The debuff values handed from SE_Invisibility to the SE_Revealed clone.</summary>
        public struct Values
        {
            public float StaminaRegen, EitrRegen, HealthRegen, Speed, Duration;
        }

        /// <summary>Set by SE_Invisibility right before AddStatusEffect; read once in Setup/ResetTime. Single-threaded game loop, and only the local player's effects are simulated on a machine, so the static handoff is safe.</summary>
        public static Values Next = new Values
        {
            StaminaRegen = InvisibilityPotion.Config.GameplayDefaults.DebuffStaminaRegenMultiplier,
            EitrRegen = InvisibilityPotion.Config.GameplayDefaults.DebuffEitrRegenMultiplier,
            HealthRegen = InvisibilityPotion.Config.GameplayDefaults.DebuffHealthRegenMultiplier,
            Speed = InvisibilityPotion.Config.GameplayDefaults.DebuffSpeedModifier(2),
            Duration = InvisibilityPotion.Config.GameplayDefaults.DebuffDuration,
        };

        private void ApplyNext()
        {
            m_staminaRegenMultiplier = Next.StaminaRegen;
            m_eitrRegenMultiplier = Next.EitrRegen;
            m_healthRegenMultiplier = Next.HealthRegen;
            m_speedModifier = Next.Speed;
            m_ttl = Next.Duration;
        }

        public override void Setup(Character character)
        {
            ApplyNext();
            base.Setup(character);
        }

        public override void ResetTime()
        {
            ApplyNext();
            base.ResetTime();
        }
    }
}
