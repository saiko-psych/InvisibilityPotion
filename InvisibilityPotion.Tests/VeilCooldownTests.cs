using InvisibilityPotion.Config;
using Xunit;

public class VeilCooldownTests
{
    [Theory]
    [InlineData(90f, true)]
    [InlineData(0.5f, true)]
    [InlineData(0f, false)]
    [InlineData(-5f, false)]
    [InlineData(float.NaN, false)]
    [InlineData(float.PositiveInfinity, false)]
    public void Starts_OnlyForAPositiveFiniteCooldown(float cooldown, bool expected)
    {
        Assert.Equal(expected, VeilCooldown.Starts(cooldown));
    }

    [Fact]
    public void Refuses_EveryVeilMeadWhileTheSharedCooldownRuns()
    {
        // One shared "Veil Cooldown" blocks all three meads; other items are never touched.
        Assert.True(VeilCooldown.Refuses(incomingIsVeil: true, cooldownRemaining: 12f));
        Assert.False(VeilCooldown.Refuses(incomingIsVeil: true, cooldownRemaining: 0f));
        Assert.False(VeilCooldown.Refuses(incomingIsVeil: true, cooldownRemaining: -1f));
        Assert.False(VeilCooldown.Refuses(incomingIsVeil: false, cooldownRemaining: 12f));
    }

    [Fact]
    public void Defaults_PerTierAndLongerThanTheEffect()
    {
        Assert.Equal(30f, GameplayDefaults.Cooldown(1));
        Assert.Equal(60f, GameplayDefaults.Cooldown(2));
        Assert.Equal(90f, GameplayDefaults.Cooldown(3));
        Assert.Equal(0f, GameplayDefaults.Cooldown(4));
    }

    [Fact]
    public void Revision2_MovesTheReservedCooldownZeroToTheTierDefault()
    {
        Assert.True(GameplayDefaults.Revision >= 3);
        Assert.Equal(30f, GameplayDefaults.Migrate("Cooldown", 1, 0f, 0));
        Assert.Equal(60f, GameplayDefaults.Migrate("Cooldown", 2, 180f, 2));   // revision-2 default moves to revision 3
        Assert.Equal(200f, GameplayDefaults.Migrate("Cooldown", 2, 200f, 2));  // an admin value stays
        Assert.Equal(60f, GameplayDefaults.Migrate("Cooldown", 2, 0f, 1));
        Assert.Equal(90f, GameplayDefaults.Migrate("Cooldown", 3, 0f, 1));
        Assert.Equal(30f, GameplayDefaults.Migrate("Cooldown", 2, 30f, 1));   // a value the admin set stays
        Assert.Equal(0f, GameplayDefaults.Migrate("Cooldown", 2, 0f, 2));     // already migrated: 0 is a deliberate "off"
        // Revision 1 changes are not repeated for a revision 1 file.
        Assert.Equal(0.5f, GameplayDefaults.Migrate("DebuffStaminaRegenMultiplier", 2, 0.5f, 1));
    }
}
