using InvisibilityPotion.Config;

namespace InvisibilityPotion.Effects
{
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
        }

        private readonly TierConfig _tier;

        public InvisibilityPhase Phase { get; private set; } = InvisibilityPhase.Hidden;

        public float Elapsed { get; private set; }
        public float RehideTimer { get; private set; }
        public bool PendingReveal { get; private set; }

        public InvisibilityStateMachine(TierConfig tier) { _tier = tier; }

        /// <summary>Safe to call from any hook, including OnDamaged: only sets a flag.</summary>
        public void MarkRevealed() { if (Phase != InvisibilityPhase.Ended) PendingReveal = true; }

        public StepResult Tick(float dt)
        {
            var r = new StepResult();
            if (Phase == InvisibilityPhase.Ended) { r.End = true; return r; }

            Elapsed += dt;

            if (PendingReveal)
            {
                PendingReveal = false;
                r.ApplyDebuff = true;
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
