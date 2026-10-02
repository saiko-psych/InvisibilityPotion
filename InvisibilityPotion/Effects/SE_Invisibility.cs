using System.Linq;
using InvisibilityPotion.Config;
using InvisibilityPotion.Net;

namespace InvisibilityPotion.Effects
{
    /// <summary>Owner-side status effect. Hosts the state machine, writes the ZDO state, applies the debuff. Visuals are driven separately from the ZDO by VeilController (Task 9).</summary>
    public class SE_Invisibility : SE_Stats
    {
        public int Tier;                       // set on the prefab by StatusEffects.Register
        public InvisibilityStateMachine Machine { get; private set; }
        private bool _cleaned;
        private bool _cooldownPending;   // start the Veil Cooldown on the next UpdateStatusEffect (adds only from there)

        private TierConfig Cfg => PluginConfig.Tier(Tier);
        private Player Owner => m_character as Player;

        public static SE_Invisibility ActiveOn(Player p)
        {
            if (p == null) return null;
            foreach (var se in p.GetSEMan().GetStatusEffects())
                // An Ended effect lingers until the next SEMan.Update; it no longer counts as active.
                if (se is SE_Invisibility inv && inv.Machine != null && inv.Machine.Phase != InvisibilityStateMachine.InvisibilityPhase.Ended) return inv;
            return null;
        }

        public override void Setup(Character character)
        {
            var cfg = Cfg;
            m_ttl = cfg.Duration;
            m_stealthModifier = ModifierMath.ToAdditive(cfg.StealthModifier);
            m_noiseModifier = ModifierMath.ToAdditive(cfg.NoiseModifier);
            base.Setup(character);
            Machine = new InvisibilityStateMachine(cfg);
            _cleaned = false;
            _cooldownPending = true;
            var owner = character as Player;
            if (owner != null)
            {
                // Any other active invisibility tier gives way (refusal of lower tiers is a CanConsumeItem postfix in a later task); its Stop() runs Cleanup, so write our state afterwards.
                foreach (var se in owner.GetSEMan().GetStatusEffects().ToArray())
                    if (se is SE_Invisibility other && !ReferenceEquals(other, this)) owner.GetSEMan().RemoveStatusEffect(other, true);
                HiddenState.Write(owner, Tier, true);
            }
            Plugin.Log.LogInfo($"SE_Invisibility T{Tier} started for {character?.GetHoverName()}");
        }

        public void MarkRevealed(RevealReason reason)
        {
            if (Machine == null) return;
            Machine.MarkRevealed(reason);
            Plugin.Log.LogInfo($"T{Tier} reveal marked: {reason}");
        }

        /// <summary>Runs inside SEMan.OnDamaged's foreach: flag only.</summary>
        public override void OnDamaged(HitData hit, Character attacker)
        {
            if (PluginConfig.Global.RevealOnDamage) MarkRevealed(RevealReason.DamageTaken);
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            if (Machine == null || Owner == null) return;
            if (_cooldownPending)
            {
                // Round Q ruling 3: drinking starts the shared Veil Cooldown (all three meads) for this tier's Cooldown.
                _cooldownPending = false;
                var cooldown = Cfg.Cooldown;
                if (VeilCooldown.Starts(cooldown))
                {
                    SE_VeilCooldown.NextDuration = cooldown;
                    Owner.GetSEMan().AddStatusEffect(StatusEffects.CooldownHash, resetTime: true);   // add is safe inside SEMan.Update
                }
            }
            var r = Machine.Tick(dt);
            if (r.DrainStamina && PluginConfig.Global.DrainStaminaOnAttackReveal)
            {
                // Round Q ruling 2: the player's own action broke the veil. Player.UseStamina runs RPC_UseStamina directly on the
                // owner (m_stamina clamped at 0, m_staminaRegenTimer = m_staminaRegenDelay), the HUD reads m_stamina.
                var use = RevealPenalty.StaminaToUse(Owner.GetStamina(), Game.m_staminaRate);
                if (use > 0f)
                {
                    Owner.UseStamina(use);
                    Plugin.Log.LogInfo($"T{Tier} veil broken by an own action: stamina emptied");
                }
            }
            if (r.ApplyDebuff)
            {
                // Round R ruling B: Veil Broken lasts until the veil returns (RehideDelay) for tiers that re-hide, DebuffDuration
                // for tier I. m_ttl 0 would make SE_Revealed permanent (IsDone needs m_ttl > 0), so 0 adds nothing.
                var cfg = Cfg;
                var seconds = RevealPenalty.DebuffSeconds(cfg);
                if (seconds > 0f)
                {
                    SE_Revealed.Next = new SE_Revealed.Values
                    {
                        StaminaRegen = cfg.DebuffStaminaRegenMultiplier, EitrRegen = cfg.DebuffEitrRegenMultiplier,
                        HealthRegen = cfg.DebuffHealthRegenMultiplier, Speed = cfg.DebuffSpeedModifier, Duration = seconds,
                    };
                    Owner.GetSEMan().AddStatusEffect(StatusEffects.RevealedHash, resetTime: true);   // add is safe inside SEMan.Update
                }
            }
            if (r.End)
            {
                // Stop() -> Cleanup() writes the final state, so skip the Hidden/Revealed write on the ending tick.
                // IsDone is a strict m_time > m_ttl, and base.UpdateStatusEffect already advanced m_time this tick.
                // Never lower it (End is returned on every tick once Ended); the next SEMan.Update then finishes the effect.
                if (m_time < m_ttl) m_time = m_ttl;
                return;
            }
            if (r.EnterRevealed) HiddenState.Write(Owner, Tier, false);
            if (r.EnterHidden) HiddenState.Write(Owner, Tier, true);
        }

        /// <summary>Same-tier re-add (for example ip_give twice) lands here on the existing instance: a deliberate refresh that also restarts the state machine and re-hides.</summary>
        public override void ResetTime()
        {
            base.ResetTime();
            if (Machine == null) return;
            Machine = new InvisibilityStateMachine(Cfg);
            _cooldownPending = true;   // a re-drink of the same tier (after its cooldown ran out) starts a new cooldown
            if (Owner != null) HiddenState.Write(Owner, Tier, true);
        }

        public override void Stop()
        {
            base.Stop();
            Cleanup();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            Cleanup();   // vanilla does not call Stop() on logout
        }

        /// <summary>The single cleanup path. Idempotent.</summary>
        public void Cleanup()
        {
            if (_cleaned) return;
            _cleaned = true;
            var owner = Owner;
            if (owner == null) return;   // also hit for prefab ScriptableObjects at shutdown; stay silent
            HiddenState.Write(owner, 0, false);
            Plugin.Log.LogInfo($"SE_Invisibility T{Tier} cleaned up");
        }
    }
}
