using InvisibilityPotion.Plants;
using Xunit;

public class PlantYieldTests
{
    [Theory]
    [InlineData("1-2", 1, 2)]
    [InlineData(" 2 - 4 ", 2, 4)]
    [InlineData("3", 3, 3)]
    [InlineData("4-2", 2, 4)]
    [InlineData("0-0", 1, 1)]
    public void Parses_ranges(string text, int min, int max)
    {
        Assert.True(PlantYield.TryParseRange(text, out var a, out var b));
        Assert.Equal((min, max), (a, b));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("a-b")]
    [InlineData("1-2-3")]
    [InlineData("1.5-2")]
    public void Rejects_malformed(string text) => Assert.False(PlantYield.TryParseRange(text, out _, out _));

    [Fact]
    public void Defaults_are_the_user_decisions()
    {
        Assert.Equal("1-2", PlantYield.DefaultYieldText(1));
        Assert.Equal("1-1", PlantYield.DefaultYieldText(2));
        Assert.Equal("2-4", PlantYield.DefaultYieldText(3));
        for (var t = 1; t <= 3; t++) Assert.True(PlantYield.TryParseRange(PlantYield.DefaultYieldText(t), out _, out _));
    }

    [Theory]
    [InlineData(0.0, 2)]
    [InlineData(0.33, 2)]
    [InlineData(0.34, 3)]
    [InlineData(0.99999, 4)]
    [InlineData(1.0, 4)]
    [InlineData(-1.0, 2)]
    public void Roll_is_uniform_over_the_range(double r, int expected) => Assert.Equal(expected, PlantYield.Roll(2, 4, r));

    [Theory]
    [InlineData(0.8f, 0.0)]
    [InlineData(1.3f, 1.0)]
    [InlineData(1.05f, 0.5)]
    [InlineData(2f, 1.0)]
    [InlineData(0.5f, 0.0)]
    public void Normalized_scale(float s, double expected) => Assert.Equal(expected, PlantYield.NormalizedScale(s, 0.8f, 1.3f), 5);

    [Fact]
    public void Empty_scale_range_is_neutral() => Assert.Equal(0.5, PlantYield.NormalizedScale(1f, 1f, 1f));

    [Theory]
    [InlineData(2, 0.0, 2)]    // 1.5 -> 2 (half away from zero)
    [InlineData(2, 0.5, 2)]
    [InlineData(2, 1.0, 3)]    // 2.5 -> 3
    [InlineData(4, 0.0, 3)]
    [InlineData(4, 1.0, 5)]
    [InlineData(1, 0.0, 1)]    // 0.75 -> 1
    [InlineData(1, 1.0, 1)]    // 1.25 -> 1
    public void Yield_grows_mildly_with_size(int roll, double n, int expected) => Assert.Equal(expected, PlantYield.Scaled(roll, n));

    [Fact]
    public void Scaled_never_below_one() => Assert.Equal(1, PlantYield.Scaled(0, 0));

    [Fact]
    public void Scale_ranges()
    {
        Assert.Equal(0.8f, PlantYield.RollScale(2, 0), 5);
        Assert.Equal(1.3f, PlantYield.RollScale(2, 1), 5);
        Assert.Equal(0.7f, PlantYield.RollScale(3, 0), 5);
        Assert.Equal(1.6f, PlantYield.RollScale(3, 1), 5);
        Assert.Equal(1f, PlantYield.RollScale(1, 0.7), 5);
    }
}
