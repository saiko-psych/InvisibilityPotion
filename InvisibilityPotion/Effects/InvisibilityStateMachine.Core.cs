using InvisibilityPotion.Config;

namespace InvisibilityPotion.Effects
{
    public enum RevealReason { DamageDealt, BowDraw, StaffCast, Block, DamageTaken, Command, ToolUse }

    /// <summary>Round Q ruling 2: the stamina penalty when the hidden player's own action reveals them. Pure.</summary>
    public static class RevealPenalty
    {
        /// <summary>The hidden player's own action: hitting, drawing a bow, casting, tool use. Not taking damage, blocking or a command.</summary>
        public static bool IsOwnAttack(RevealReason reason)
        {
            switch (reason)
            {
                case RevealReason.DamageDealt:
                case RevealReason.BowDraw:
                case RevealReason.StaffCast:
                case RevealReason.ToolUse:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>[General] DrainStaminaOnAttackReveal applied to a reveal reason.</summary>
        public static bool DrainsStamina(RevealReason reason, bool enabled) => enabled && IsOwnAttack(reason);

        /// <summary>
        /// Argument for Player.UseStamina that empties the bar: UseStamina multiplies by Game.m_staminaRate (world modifier) and
        /// RPC_UseStamina clamps at 0, so current / rate plus a margin. 0 when the bar is empty or the world uses no stamina (rate 0).
        /// </summary>
        public static float StaminaToUse(float current, float staminaRate)
        {
            if (float.IsNaN(current) || float.IsNaN(staminaRate) || current <= 0f || staminaRate <= 0f) return 0f;
            return current / staminaRate + 1f;
        }
    }

    /// <summary>Phase and timer logic of one invisibility effect. Pure: the game side calls MarkRevealed from hooks and Tick from UpdateStatusEffect and acts on the returned flags.</summary>
    public sealed class InvisibilityStateMachine
    {
        public enum InvisibilityPhase { Hidden, Revealed, Ended }

        public struct StepResult
        {
            public bool ApplyDebuff;    // add or reset SE_Revealed
            public bool EnterHidden;    // write IP_Hidden = true
            public bool EnterRevealed;  // write IP_Hidden = false
            public bool End;            // the effect is over, let it expire
            public bool DrainStamina;   // the player's own action broke a hidden veil (round Q): empty the stamina bar if configured
        }

        private readonly TierConfig _tier;

        public InvisibilityPhase Phase { get; private set; } = InvisibilityPhase.Hidden;

        public float Elapsed { get; private set; }
        public float RehideTimer { get; private set; }
        public bool PendingReveal { get; private set; }
        /// <summary>At least one of the pending reveals was the player's own action (<see cref="RevealPenalty.IsOwnAttack"/>).</summary>
        public bool PendingOwnAttack { get; private set; }

        public InvisibilityStateMachine(TierConfig tier) { _tier = tier; }

        /// <summary>Safe to call from any hook, including OnDamaged: only sets a flag.</summary>
        public void MarkRevealed(RevealReason reason = RevealReason.Command)
        {
            if (Phase == InvisibilityPhase.Ended) return;
            PendingReveal = true;
            if (RevealPenalty.IsOwnAttack(reason)) PendingOwnAttack = true;
        }

        public StepResult Tick(float dt)
        {
            var r = new StepResult();
            if (Phase == InvisibilityPhase.Ended) { r.End = true; return r; }

            Elapsed += dt;

            if (PendingReveal)
            {
                PendingReveal = false;
                r.ApplyDebuff = true;
                // Only an action that breaks a hidden veil drains; hits while already revealed only restart the rehide timer.
                r.DrainStamina = PendingOwnAttack && Phase == InvisibilityPhase.Hidden;
                PendingOwnAttack = false;
                if (_tier.EndsOnReveal)
                {
                    Phase = InvisibilityPhase.Ended;
                    r.End = true;
                    return r;
                }
                if (Phase == InvisibilityPhase.Hidden) { Phase = InvisibilityPhase.Revealed; r.EnterRevealed = true; }
                RehideTimer = _tier.RehideDelay;
            }
            else if (Phase == InvisibilityPhase.Revealed)
            {
                RehideTimer -= dt;
                if (RehideTimer <= 0f) { Phase = InvisibilityPhase.Hidden; RehideTimer = 0f; r.EnterHidden = true; }
            }

            if (Elapsed >= _tier.Duration) { Phase = InvisibilityPhase.Ended; r.End = true; }
            return r;
        }
    }
}
