using InvisibilityPotion.Config;
using InvisibilityPotion.Effects;
using Xunit;

/// <summary>
/// User decision 2026-11-04: [General] RevealOnBlock works independently of RevealOnDamage. A blocked hit reports DamageTaken
/// (SEMan.OnDamaged runs first, Character.cs:2331) and then Block (BlockAttack, same RPC_Damage); the machine counts both and
/// decides on the next Tick. Fall damage stays a normal DamageTaken (user: yes, it reveals).
/// </summary>
public class RevealOnBlockTests
{
    private static InvisibilityStateMachine Machine(bool onDamage, bool onBlock) =>
        new InvisibilityStateMachine(new TierConfig { Tier = 2, Duration = 120f, RehideDelay = 12f, DebuffDuration = 20f })
        {
            RevealOnDamage = onDamage,
            RevealOnBlock = onBlock,
        };

    private static void BlockedHit(InvisibilityStateMachine sm)
    {
        sm.MarkRevealed(RevealReason.DamageTaken);
        sm.MarkRevealed(RevealReason.Block);
    }

    [Fact]
    public void BlockedHit_DoesNotReveal_WhenBlockRevealIsOff_EvenWithDamageRevealOn()
    {
        var sm = Machine(onDamage: true, onBlock: false);
        BlockedHit(sm);
        var r = sm.Tick(0.1f);
        Assert.False(r.EnterRevealed);
        Assert.False(r.ApplyDebuff);
        Assert.Equal(InvisibilityStateMachine.InvisibilityPhase.Hidden, sm.Phase);
        Assert.False(sm.PendingReveal);
    }

    [Fact]
    public void BlockedHit_Reveals_WhenBlockRevealIsOn_EvenWithDamageRevealOff()
    {
        var sm = Machine(onDamage: false, onBlock: true);
        BlockedHit(sm);
        Assert.True(sm.Tick(0.1f).EnterRevealed);
    }

    [Fact]
    public void UnblockedHit_FollowsRevealOnDamageOnly()
    {
        var on = Machine(onDamage: true, onBlock: false);
        on.MarkRevealed(RevealReason.DamageTaken);
        Assert.True(on.Tick(0.1f).EnterRevealed);

        var off = Machine(onDamage: false, onBlock: true);
        off.MarkRevealed(RevealReason.DamageTaken);
        Assert.False(off.Tick(0.1f).EnterRevealed);
    }

    [Fact]
    public void TwoHitsInOneTick_OneBlockedOneNot_UnblockedStillReveals()
    {
        var sm = Machine(onDamage: true, onBlock: false);
        BlockedHit(sm);
        sm.MarkRevealed(RevealReason.DamageTaken);   // a second, unblocked hit before the next tick
        Assert.True(sm.Tick(0.1f).EnterRevealed);
    }

    [Fact]
    public void BlockedHit_DoesNotBlockAnOwnAttackInTheSameTick()
    {
        var sm = Machine(onDamage: false, onBlock: false);
        BlockedHit(sm);
        sm.MarkRevealed(RevealReason.DamageDealt);   // the player hit back in the same frame
        var r = sm.Tick(0.1f);
        Assert.True(r.EnterRevealed);
        Assert.True(r.DrainStamina);
    }

    [Fact]
    public void BothSwitchesOn_IsTheOldBehaviour()
    {
        var sm = Machine(onDamage: true, onBlock: true);
        BlockedHit(sm);
        var r = sm.Tick(0.1f);
        Assert.True(r.EnterRevealed);
        Assert.False(r.DrainStamina);   // blocking and taking damage are not the player's own attack
    }

    [Fact]
    public void Defaults_RevealOnBoth()
    {
        var sm = new InvisibilityStateMachine(new TierConfig { Tier = 2, Duration = 120f, RehideDelay = 12f });
        Assert.True(sm.RevealOnDamage);
        Assert.True(sm.RevealOnBlock);
    }

    [Fact]
    public void FilteredCandidates_DoNotRestartTheRehideTimer()
    {
        var sm = Machine(onDamage: true, onBlock: false);
        sm.MarkRevealed(RevealReason.DamageDealt);
        sm.Tick(0.1f);                       // revealed, rehide timer 12 s
        sm.Tick(5f);                         // 7 s left
        BlockedHit(sm);                      // blocked while revealed, block reveal off
        sm.Tick(0.1f);
        Assert.InRange(sm.RehideTimer, 6.8f, 7f);
    }
}
