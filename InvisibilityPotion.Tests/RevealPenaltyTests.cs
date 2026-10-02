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
        // A revision-0 file at 0.5 moves through 0.25 (revision 1) to 0.15 (revision 4).
        Assert.Equal(0.15f, GameplayDefaults.Migrate("DebuffStaminaRegenMultiplier", 2, 0.5f, 0));
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

public class VeilBrokenTests
{
    private static TierConfig Tier(int t, float rehide, float debuff) =>
        new TierConfig { Tier = t, Duration = 60f, RehideDelay = rehide, DebuffDuration = debuff };

    [Fact]
    public void DebuffSeconds_ReHidingTiersLastUntilTheVeilReturns()
    {
        // Round R ruling B: Veil Broken ends exactly when the veil comes back.
        Assert.Equal(12f, RevealPenalty.DebuffSeconds(Tier(2, 12f, 15f)));
        Assert.Equal(8f, RevealPenalty.DebuffSeconds(Tier(3, 8f, 15f)));
        Assert.Equal(30f, RevealPenalty.DebuffSeconds(Tier(2, 30f, 15f)));
        // Tier I (the effect ends on reveal) keeps DebuffDuration.
        Assert.Equal(15f, RevealPenalty.DebuffSeconds(Tier(1, 0f, 15f)));
        Assert.Equal(15f, RevealPenalty.DebuffSeconds(Tier(2, -1f, 15f)));
        // DebuffDuration 0 stays the off switch for every tier.
        Assert.Equal(0f, RevealPenalty.DebuffSeconds(Tier(1, 0f, 0f)));
        Assert.Equal(0f, RevealPenalty.DebuffSeconds(Tier(2, 12f, 0f)));
        Assert.Equal(0f, RevealPenalty.DebuffSeconds(null));
    }

    [Fact]
    public void Defaults_HarsherDebuff()
    {
        Assert.Equal(0.15f, GameplayDefaults.DebuffStaminaRegenMultiplier, 5);
        Assert.Equal(-0.2f, GameplayDefaults.DebuffSpeedModifier(1), 5);
        Assert.Equal(-0.3f, GameplayDefaults.DebuffSpeedModifier(2), 5);
        Assert.Equal(-0.3f, GameplayDefaults.DebuffSpeedModifier(3), 5);
        Assert.Equal(0.25f, GameplayDefaults.DebuffEitrRegenMultiplier, 5);
        Assert.Equal(0.5f, GameplayDefaults.DebuffHealthRegenMultiplier, 5);
        var c = new TierConfig();
        Assert.Equal(0f, c.DebuffSpeedModifier);
        Assert.Equal(1f, c.DebuffEitrRegenMultiplier);
        Assert.Equal(1f, c.DebuffHealthRegenMultiplier);
    }

    [Fact]
    public void Revision4_MovesOnlyAnUntouchedStaminaRegenMultiplier()
    {
        Assert.Equal(4, GameplayDefaults.Revision);
        Assert.Equal(0.15f, GameplayDefaults.Migrate("DebuffStaminaRegenMultiplier", 2, 0.25f, 3));
        Assert.Equal(0.15f, GameplayDefaults.Migrate("DebuffStaminaRegenMultiplier", 1, 0.25f, 1));
        Assert.Equal(0.4f, GameplayDefaults.Migrate("DebuffStaminaRegenMultiplier", 2, 0.4f, 3));   // admin value stays
        Assert.Equal(0.25f, GameplayDefaults.Migrate("DebuffStaminaRegenMultiplier", 2, 0.25f, 4)); // already migrated
        // Revision 4 does not touch the cooldown chain.
        Assert.Equal(60f, GameplayDefaults.Migrate("Cooldown", 2, 60f, 3));
    }

    [Theory]
    [InlineData("DebuffStaminaRegenMultiplier", -0.1f)]
    [InlineData("DebuffStaminaRegenMultiplier", 5.1f)]
    [InlineData("DebuffEitrRegenMultiplier", -0.1f)]
    [InlineData("DebuffEitrRegenMultiplier", 6f)]
    [InlineData("DebuffHealthRegenMultiplier", -1f)]
    [InlineData("DebuffHealthRegenMultiplier", float.NaN)]
    [InlineData("DebuffSpeedModifier", -0.95f)]
    [InlineData("DebuffSpeedModifier", 1.5f)]
    public void Validate_RejectsDebuffValuesOutOfRange(string key, float value)
    {
        var c = Tier(2, 12f, 15f);
        switch (key)
        {
            case "DebuffStaminaRegenMultiplier": c.DebuffStaminaRegenMultiplier = value; break;
            case "DebuffEitrRegenMultiplier": c.DebuffEitrRegenMultiplier = value; break;
            case "DebuffHealthRegenMultiplier": c.DebuffHealthRegenMultiplier = value; break;
            case "DebuffSpeedModifier": c.DebuffSpeedModifier = value; break;
        }
        var ex = Assert.Throws<System.ArgumentOutOfRangeException>(() => c.Validate());
        Assert.Equal(key, ex.ParamName);
    }

    [Fact]
    public void Validate_AcceptsTheRangeEnds()
    {
        var c = Tier(2, 12f, 15f);
        c.DebuffStaminaRegenMultiplier = 0f; c.DebuffEitrRegenMultiplier = 5f; c.DebuffHealthRegenMultiplier = 1f; c.DebuffSpeedModifier = -0.9f;
        c.Validate();
        c.DebuffSpeedModifier = 1f;
        c.Validate();
        Assert.Equal(GameplayDefaults.DebuffMultiplierMax, 5f);
        Assert.Equal(GameplayDefaults.DebuffSpeedMin, -0.9f);
        Assert.Equal(GameplayDefaults.DebuffSpeedMax, 1f);
    }
}
