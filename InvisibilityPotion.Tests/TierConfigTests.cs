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
    public void HiddenFromPlayers_DefaultsToFalse()
    {
        Assert.False(new TierConfig().HiddenFromPlayers);
    }

    // Review 2026-11-04: NaN/Infinity passed the "<= 0" / "< 0" checks, so an admin-set Duration = NaN made the veil permanent.
    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Validate_RejectsNonFiniteDuration(float v) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TierConfig { Tier = 1, Duration = v }.Validate());

    [Theory]
    [InlineData("AggroLossTime", float.NaN)]
    [InlineData("AggroLossTime", float.PositiveInfinity)]
    [InlineData("DebuffDuration", float.NaN)]
    [InlineData("DebuffDuration", float.PositiveInfinity)]
    [InlineData("RehideDelay", float.NaN)]
    [InlineData("RehideDelay", float.PositiveInfinity)]
    [InlineData("StealthModifier", float.NaN)]
    [InlineData("StealthModifier", -0.5f)]
    [InlineData("NoiseModifier", float.NaN)]
    [InlineData("NoiseModifier", -0.5f)]
    [InlineData("Cooldown", float.NaN)]
    [InlineData("CarryWeightMultiplier", float.NaN)]
    public void Validate_RejectsNonFiniteOrNegativeValues(string field, float v)
    {
        var c = new TierConfig { Tier = 2, Duration = 120f, AggroLossTime = 1f, DebuffDuration = 20f, RehideDelay = 12f };
        typeof(TierConfig).GetField(field).SetValue(c, v);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => c.Validate());
        Assert.Equal(field, ex.ParamName);
    }

    [Fact]
    public void Validate_KeepsZeroRehideDelayAndCooldown()
    {
        // 0 means "an attack ends the effect" / "no cooldown"; both stay valid.
        new TierConfig { Tier = 1, Duration = 60f, RehideDelay = 0f, Cooldown = 0f }.Validate();
    }

    // Review 2026-11-04: one invalid value used to replace the whole tier with its defaults (recipe, cooldown and debuffs included).
    private static TierConfig Defaults2() => new TierConfig { Tier = 2, Duration = 120f, AggroLossTime = 1f, RehideDelay = 12f, DebuffDuration = 15f,
        Cooldown = 60f, CarryWeightMultiplier = 0.6f, Recipe = "Honey:10", DebuffSpeedModifier = -0.5f };

    [Fact]
    public void WithDefaultsForInvalid_ReplacesOnlyTheInvalidField()
    {
        var custom = new TierConfig { Tier = 2, Duration = float.NaN, AggroLossTime = 3f, RehideDelay = 20f, DebuffDuration = 15f,
            Cooldown = 10f, CarryWeightMultiplier = 0.9f, Recipe = "Honey:1", DebuffSpeedModifier = -0.1f };
        var fixedCfg = custom.WithDefaultsForInvalid(Defaults2(), out var replaced);
        Assert.Equal(new[] { "Duration" }, replaced);
        Assert.Equal(120f, fixedCfg.Duration);
        Assert.Equal(20f, fixedCfg.RehideDelay);
        Assert.Equal(10f, fixedCfg.Cooldown);
        Assert.Equal("Honey:1", fixedCfg.Recipe);
        Assert.True(float.IsNaN(custom.Duration));   // the input is not changed
        fixedCfg.Validate();
    }

    [Fact]
    public void WithDefaultsForInvalid_ReplacesEveryInvalidField()
    {
        var custom = Defaults2();
        custom.Duration = -5f;
        custom.CarryWeightMultiplier = 3f;
        custom.StealthModifier = float.PositiveInfinity;
        var fixedCfg = custom.WithDefaultsForInvalid(Defaults2(), out var replaced);
        Assert.Equal(new[] { "Duration", "StealthModifier", "CarryWeightMultiplier" }, replaced);
        fixedCfg.Validate();
    }

    [Fact]
    public void WithDefaultsForInvalid_ValidConfigIsReturnedUnchanged()
    {
        var custom = Defaults2();
        var same = custom.WithDefaultsForInvalid(Defaults2(), out var replaced);
        Assert.Empty(replaced);
        Assert.Equal(custom.Recipe, same.Recipe);
    }

    [Fact]
    public void WithDefaultsForInvalid_InvalidDefaultsThrow()
    {
        var broken = Defaults2();
        broken.Duration = 0f;
        Assert.Throws<ArgumentOutOfRangeException>(() => broken.WithDefaultsForInvalid(broken, out _));
    }

    [Fact]
    public void Validate_AcceptsValidConfig()
    {
        new TierConfig { Tier = 2, Duration = 120f, AggroLossTime = 1f, DebuffDuration = 20f }.Validate();
    }
}

public class GlobalConfigDefaultsTests
{
    [Fact]
    public void RevealOnToolUse_DefaultsToOn()
    {
        Assert.True(new GlobalConfig().RevealOnToolUse);
    }
}
