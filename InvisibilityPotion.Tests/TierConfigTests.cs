using System;
using InvisibilityPotion.Config;
using Xunit;

public class ModifierMathTests
{
    [Theory]
    [InlineData(1.0f, 0.0f)]
    [InlineData(0.25f, -0.75f)]
    [InlineData(0.0f, -1.0f)]
    [InlineData(1.5f, 0.5f)]
    public void ToAdditive_ConvertsFractionOfVanillaToVanillaDelta(float fraction, float expected)
    {
        Assert.Equal(expected, ModifierMath.ToAdditive(fraction), 5);
    }
}

public class RecipeParserTests
{
    [Fact]
    public void Parse_ReadsItemAmountPairs()
    {
        var r = RecipeParser.Parse("Honey:10,Thistle:5");
        Assert.Equal(2, r.Count);
        Assert.Equal(("Honey", 10), r[0]);
        Assert.Equal(("Thistle", 5), r[1]);
    }

    [Fact]
    public void Parse_TrimsWhitespaceAndIgnoresEmptyEntries()
    {
        var r = RecipeParser.Parse(" Honey : 10 , , Thistle:5 ,");
        Assert.Equal(2, r.Count);
        Assert.Equal(("Honey", 10), r[0]);
    }

    [Theory]
    [InlineData("Honey")]
    [InlineData("Honey:zero")]
    [InlineData("Honey:0")]
    [InlineData(":5")]
    public void Parse_ThrowsOnMalformedEntry(string recipe)
    {
        Assert.Throws<FormatException>(() => RecipeParser.Parse(recipe));
    }

    [Fact]
    public void Parse_EmptyRecipeGivesNoRequirements()
    {
        Assert.Empty(RecipeParser.Parse(""));
        Assert.Empty(RecipeParser.Parse(null));
    }
}

public class TierConfigTests
{
    [Fact]
    public void EndsOnReveal_IsTrueOnlyWhenRehideDelayIsZeroOrLess()
    {
        Assert.True(new TierConfig { Tier = 1, RehideDelay = 0f }.EndsOnReveal);
        Assert.False(new TierConfig { Tier = 2, RehideDelay = 12f }.EndsOnReveal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Validate_RejectsTierOutsideOneToThree(int tier)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TierConfig { Tier = tier, Duration = 1f }.Validate());
    }

    [Fact]
    public void Validate_RejectsNonPositiveDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TierConfig { Tier = 1, Duration = 0f }.Validate());
    }

    [Fact]
    public void Validate_RejectsNegativeAggroLossTime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TierConfig { Tier = 1, Duration = 1f, AggroLossTime = -1f }.Validate());
    }

    [Fact]
    public void Validate_RejectsNegativeDebuffDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TierConfig { Tier = 1, Duration = 1f, DebuffDuration = -1f }.Validate());
    }

    [Fact]
    public void Validate_AcceptsValidConfig()
    {
        new TierConfig { Tier = 2, Duration = 120f, AggroLossTime = 1f, DebuffDuration = 20f }.Validate();
    }
}
