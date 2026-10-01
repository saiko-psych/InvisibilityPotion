using InvisibilityPotion.Config;
using InvisibilityPotion.Effects;
using Xunit;

public class InvisibilityStateMachineTests
{
    private static TierConfig Tier1() => new TierConfig { Tier = 1, Duration = 60f, RehideDelay = 0f, DebuffDuration = 20f };
    private static TierConfig Tier2() => new TierConfig { Tier = 2, Duration = 120f, RehideDelay = 12f, DebuffDuration = 20f };

    [Fact]
    public void StartsHidden()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        Assert.Equal(InvisibilityStateMachine.InvisibilityPhase.Hidden, sm.Phase);
        Assert.False(sm.PendingReveal);
    }

    [Fact]
    public void Tick_WithoutEvents_OnlyAdvancesElapsed()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        var r = sm.Tick(1f);
        Assert.Equal(1f, sm.Elapsed);
        Assert.False(r.ApplyDebuff || r.EnterHidden || r.EnterRevealed || r.End);
    }

    [Fact]
    public void MarkRevealed_OnlySetsFlag_UntilNextTick()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        sm.MarkRevealed();
        Assert.True(sm.PendingReveal);
        Assert.Equal(InvisibilityStateMachine.InvisibilityPhase.Hidden, sm.Phase);
    }

    [Fact]
    public void Tier1_RevealEndsEffectAndAppliesDebuff()
    {
        var sm = new InvisibilityStateMachine(Tier1());
        sm.MarkRevealed();
        var r = sm.Tick(0.02f);
        Assert.True(r.ApplyDebuff);
        Assert.True(r.End);
        Assert.Equal(InvisibilityStateMachine.InvisibilityPhase.Ended, sm.Phase);
        Assert.False(sm.PendingReveal);
    }

    [Fact]
    public void Tier2_RevealEntersRevealedAndStartsRehideTimer()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        sm.MarkRevealed();
        var r = sm.Tick(0.02f);
        Assert.True(r.ApplyDebuff);
        Assert.True(r.EnterRevealed);
        Assert.False(r.End);
        Assert.Equal(InvisibilityStateMachine.InvisibilityPhase.Revealed, sm.Phase);
        Assert.Equal(12f, sm.RehideTimer, 3);
    }

    [Fact]
    public void Tier2_RehidesAfterDelayWithoutNewReveal()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        sm.MarkRevealed();
        sm.Tick(0.02f);
        var r1 = sm.Tick(11.9f);
        Assert.False(r1.EnterHidden);
        var r2 = sm.Tick(0.2f);
        Assert.True(r2.EnterHidden);
        Assert.Equal(InvisibilityStateMachine.InvisibilityPhase.Hidden, sm.Phase);
    }

    [Fact]
    public void Tier2_RepeatedRevealRestartsTimerAndDebuff()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        sm.MarkRevealed();
        sm.Tick(0.02f);
        sm.Tick(10f);
        sm.MarkRevealed();
        var r = sm.Tick(0.02f);
        Assert.True(r.ApplyDebuff);
        Assert.False(r.EnterRevealed);          // already revealed
        Assert.Equal(12f, sm.RehideTimer, 3);
    }

    [Fact]
    public void DurationKeepsRunningWhileRevealed()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        sm.MarkRevealed();
        sm.Tick(0.02f);
        var r = sm.Tick(119.99f);
        Assert.True(r.End);
        Assert.Equal(InvisibilityStateMachine.InvisibilityPhase.Ended, sm.Phase);
    }

    [Fact]
    public void PendingRevealIsProcessedExactlyOnce()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        sm.MarkRevealed();
        var first = sm.Tick(0.02f);
        var second = sm.Tick(0.02f);
        Assert.True(first.ApplyDebuff);
        Assert.False(second.ApplyDebuff);
    }

    [Fact]
    public void Ended_IgnoresFurtherEvents()
    {
        var sm = new InvisibilityStateMachine(Tier1());
        sm.MarkRevealed();
        sm.Tick(0.02f);
        sm.MarkRevealed();
        var r = sm.Tick(1f);
        Assert.False(r.ApplyDebuff || r.EnterHidden || r.EnterRevealed);
        Assert.True(r.End);
    }
}
