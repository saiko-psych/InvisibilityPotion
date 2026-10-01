using InvisibilityPotion.Items;
using Xunit;

public class PotionVfxTests
{
    [Fact]
    public void Candidates_ConfiguredFirstThenFallbacksWithoutDuplicates()
    {
        Assert.Equal(new[] { "MeadFrostResist", "MeadTasty", "MeadHealthMinor" }, PotionVfx.Candidates(PotionVfx.DefaultSource));
        Assert.Equal(new[] { "MeadStaminaMinor", "MeadFrostResist", "MeadTasty", "MeadHealthMinor" }, PotionVfx.Candidates(" MeadStaminaMinor "));
        Assert.Equal(new[] { "meadtasty", "MeadFrostResist", "MeadHealthMinor" }, PotionVfx.Candidates("meadtasty"));
        Assert.Equal(PotionVfx.Fallbacks, PotionVfx.Candidates(""));
        Assert.Equal(PotionVfx.Fallbacks, PotionVfx.Candidates(null));
    }
}
