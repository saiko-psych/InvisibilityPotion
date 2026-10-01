using System.Linq;
using InvisibilityPotion.Config;
using InvisibilityPotion.Net;

namespace InvisibilityPotion.Effects
{
    public enum RevealReason { DamageDealt, BowDraw, StaffCast, Block, DamageTaken, Command }

    /// <summary>Owner-side status effect. Hosts the state machine, writes the ZDO state, applies the debuff. Visuals are driven separately from the ZDO by VeilController (Task 9).</summary>
    public class SE_Invisibility : SE_Stats
    {
        public int Tier;                       // set on the prefab by StatusEffects.Register
        public InvisibilityStateMachine Machine { get; private set; }
        private bool _cleaned;

        private TierConfig Cfg => PluginConfig.Tier(Tier);
        private Player Owner => m_character as Player;

        public static SE_Invisibility ActiveOn(Player p)
        {
            if (p == null) return null;
            foreach (var se in p.GetSEMan().GetStatusEffects())
                if (se is SE_Invisibility inv) return inv;
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
            var owner = character as Player;
            if (owner != null)
            {
                // A lower tier that is still active gives way; its Stop() runs Cleanup, so write our state afterwards.
                foreach (var se in owner.GetSEMan().GetStatusEffects().ToArray())
                    if (se is SE_Invisibility other && other != this) owner.GetSEMan().RemoveStatusEffect(other, true);
                HiddenState.Write(owner, Tier, true);
            }
            Plugin.Log.LogInfo($"SE_Invisibility T{Tier} started for {character?.GetHoverName()}");
        }

        public void MarkRevealed(RevealReason reason)
        {
            if (Machine == null) return;
            Machine.MarkRevealed();
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
            var r = Machine.Tick(dt);
            if (r.ApplyDebuff)
            {
                var cfg = Cfg;
                SE_Revealed.NextMultiplier = cfg.DebuffStaminaRegenMultiplier;
                SE_Revealed.NextDuration = cfg.DebuffDuration;
                Owner.GetSEMan().AddStatusEffect(StatusEffects.RevealedHash, resetTime: true);   // add is safe inside SEMan.Update
            }
            if (r.End)
            {
                // Stop() -> Cleanup() writes the final state, so skip the Hidden/Revealed write on the ending tick.
                m_time = m_ttl;   // IsDone on the next SEMan.Update; vanilla then calls Stop()
                return;
            }
            if (r.EnterRevealed) HiddenState.Write(Owner, Tier, false);
            if (r.EnterHidden) HiddenState.Write(Owner, Tier, true);
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
            if (owner != null) HiddenState.Write(owner, 0, false);
            Plugin.Log.LogInfo($"SE_Invisibility T{Tier} cleaned up");
        }
    }
}
