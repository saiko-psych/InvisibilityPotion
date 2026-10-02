using System;
using InvisibilityPotion.Config;
using InvisibilityPotion.Effects;
using Xunit;

public class CarryWeightTests
{
    [Theory]
    [InlineData(300f, 0.75f, 225f)]
    [InlineData(450f, 0.5f, 225f)]   // Megingjord included: the multiplier applies to the full limit
    [InlineData(300f, 1f, 300f)]     // 1 = off
    [InlineData(300f, 0f, 0f)]
    public void Reduced_MultipliesTheFullLimit(float limit, float multiplier, float expected)
    {
        Assert.Equal(expected, CarryWeight.Reduced(limit, multiplier), 3);
    }

    [Theory]
    [InlineData(1.5f, 300f)]   // never a buff
    [InlineData(-0.5f, 0f)]    // never negative
    [InlineData(float.NaN, 300f)]
    public void Reduced_ClampsTheMultiplierToZeroOne(float multiplier, float expected)
    {
        Assert.Equal(expected, CarryWeight.Reduced(300f, multiplier), 3);
    }

    [Fact]
    public void TierConfig_DefaultsToNoReduction()
    {
        Assert.Equal(1f, new TierConfig().CarryWeightMultiplier);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    public void Validate_RejectsCarryWeightMultiplierOutsideZeroOne(float m)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TierConfig { Tier = 1, Duration = 1f, CarryWeightMultiplier = m }.Validate());
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.6f)]
    [InlineData(1f)]
    public void Validate_AcceptsCarryWeightMultiplierInZeroOne(float m)
    {
        new TierConfig { Tier = 1, Duration = 1f, CarryWeightMultiplier = m }.Validate();
    }
}
