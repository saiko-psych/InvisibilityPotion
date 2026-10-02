using InvisibilityPotion.Config;
using InvisibilityPotion.Effects;
using Xunit;

public class RevealPenaltyTests
{
    [Theory]
    [InlineData(RevealReason.DamageDealt, true)]
    [InlineData(RevealReason.BowDraw, true)]
    [InlineData(RevealReason.StaffCast, true)]
    [InlineData(RevealReason.ToolUse, true)]
    [InlineData(RevealReason.DamageTaken, false)]
    [InlineData(RevealReason.Block, false)]
    [InlineData(RevealReason.Command, false)]
    public void IsOwnAttack_OnlyTheHiddenPlayersOwnActions(RevealReason reason, bool expected)
    {
        Assert.Equal(expected, RevealPenalty.IsOwnAttack(reason));
        Assert.Equal(expected, RevealPenalty.DrainsStamina(reason, enabled: true));
        Assert.False(RevealPenalty.DrainsStamina(reason, enabled: false));
    }

    [Theory]
    [InlineData(50f, 1f)]
    [InlineData(50f, 0.5f)]
    [InlineData(173.4f, 2f)]
    [InlineData(0.01f, 1f)]
    public void StaminaToUse_EmptiesTheBarThroughTheWorldStaminaRate(float current, float rate)
    {
        // Player.UseStamina multiplies by Game.m_staminaRate and RPC_UseStamina clamps at 0.
        var v = RevealPenalty.StaminaToUse(current, rate);
        Assert.True(current - v * rate <= 0f);
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(-3f, 1f)]
    [InlineData(50f, 0f)]
    [InlineData(float.NaN, 1f)]
    [InlineData(50f, float.NaN)]
    public void StaminaToUse_NothingWhenEmptyOrNoStaminaUseInTheWorld(float current, float rate)
    {
        Assert.Equal(0f, RevealPenalty.StaminaToUse(current, rate));
    }
}

public class RevealStaminaStateMachineTests
{
    private static TierConfig Tier1() => new TierConfig { Tier = 1, Duration = 60f, RehideDelay = 0f, DebuffDuration = 15f };
    private static TierConfig Tier2() => new TierConfig { Tier = 2, Duration = 120f, RehideDelay = 12f, DebuffDuration = 15f };

    [Fact]
    public void OwnAttackWhileHidden_DrainsOnce()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        sm.MarkRevealed(RevealReason.DamageDealt);
        var r = sm.Tick(0.1f);
        Assert.True(r.EnterRevealed);
        Assert.True(r.DrainStamina);
        // A second hit while already revealed only restarts the timer: no second drain.
        sm.MarkRevealed(RevealReason.DamageDealt);
        r = sm.Tick(0.1f);
        Assert.True(r.ApplyDebuff);
        Assert.False(r.DrainStamina);
    }

    [Fact]
    public void RevealByTakingDamageOrBlocking_DoesNotDrain()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        sm.MarkRevealed(RevealReason.DamageTaken);
        Assert.False(sm.Tick(0.1f).DrainStamina);
        var sm2 = new InvisibilityStateMachine(Tier2());
        sm2.MarkRevealed(RevealReason.Block);
        Assert.False(sm2.Tick(0.1f).DrainStamina);
        var sm3 = new InvisibilityStateMachine(Tier2());
        sm3.MarkRevealed();   // command / default
        Assert.False(sm3.Tick(0.1f).DrainStamina);
    }

    [Fact]
    public void OwnAttackAndDamageInTheSameTick_Drains()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        sm.MarkRevealed(RevealReason.DamageTaken);
        sm.MarkRevealed(RevealReason.ToolUse);
        Assert.True(sm.Tick(0.1f).DrainStamina);
    }

    [Fact]
    public void Tier1_OwnAttackEndsTheEffectAndDrains()
    {
        var sm = new InvisibilityStateMachine(Tier1());
        sm.MarkRevealed(RevealReason.BowDraw);
        var r = sm.Tick(0.1f);
        Assert.True(r.End);
        Assert.True(r.ApplyDebuff);
        Assert.True(r.DrainStamina);
    }

    [Fact]
    public void AfterRehide_AnOwnAttackDrainsAgain()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        sm.MarkRevealed(RevealReason.DamageDealt);
        sm.Tick(0.1f);
        Assert.True(sm.Tick(12.5f).EnterHidden);
        sm.MarkRevealed(RevealReason.StaffCast);
        Assert.True(sm.Tick(0.1f).DrainStamina);
    }
}

public class GameplayDefaultsTests
{
    [Fact]
    public void Revision1_MovesOnlyUntouchedOldDebuffDefaults()
    {
        Assert.True(GameplayDefaults.Revision >= 1);
        Assert.Equal(0.25f, GameplayDefaults.Migrate("DebuffStaminaRegenMultiplier", 2, 0.5f, 0));
        Assert.Equal(15f, GameplayDefaults.Migrate("DebuffDuration", 2, 20f, 0));
        // A value the admin changed stays.
        Assert.Equal(0.7f, GameplayDefaults.Migrate("DebuffStaminaRegenMultiplier", 2, 0.7f, 0));
        Assert.Equal(30f, GameplayDefaults.Migrate("DebuffDuration", 3, 30f, 0));
        // A file already at revision 1 is left alone.
        Assert.Equal(0.5f, GameplayDefaults.Migrate("DebuffStaminaRegenMultiplier", 2, 0.5f, 1));
        // Unknown keys are untouched.
        Assert.Equal(60f, GameplayDefaults.Migrate("Duration", 1, 60f, 0));
    }
}
