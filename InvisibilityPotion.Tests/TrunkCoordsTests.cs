using System;
using InvisibilityPotion.Plants;
using Xunit;

public class TrunkCoordsTests
{
    [Theory]
    [InlineData(0.1f, TrunkCoords.MinHeight)]
    [InlineData(1.3f, 1.3f)]
    [InlineData(5f, TrunkCoords.MaxHeight)]
    [InlineData(float.NaN, TrunkCoords.MinHeight)]
    public void Height_is_clamped(float h, float expected) => Assert.Equal(expected, TrunkCoords.ClampHeight(h), 4);

    [Theory]
    [InlineData(0f, 1f, 0f)]      // +Z
    [InlineData(1f, 0f, 90f)]     // +X
    [InlineData(0f, -1f, 180f)]
    [InlineData(-1f, 0f, 270f)]
    [InlineData(0f, 0f, 0f)]      // on the axis
    public void Angle_matches_unity_yaw(float dx, float dz, float expected) => Assert.Equal(expected, TrunkCoords.AngleDegrees(dx, dz), 3);

    [Theory]
    [InlineData(0f)]
    [InlineData(37.5f)]
    [InlineData(179.9f)]
    [InlineData(250f)]
    [InlineData(359.5f)]
    public void Direction_and_angle_round_trip(float angle)
    {
        TrunkCoords.Direction(angle, out var x, out var z);
        Assert.Equal(1.0, Math.Sqrt(x * x + z * z), 4);
        Assert.Equal(angle, TrunkCoords.AngleDegrees(x * 0.3f, z * 0.3f), 2);
    }

    [Theory]
    [InlineData(-90f, 270f)]
    [InlineData(720f, 0f)]
    [InlineData(365f, 5f)]
    [InlineData(float.NaN, 0f)]
    public void Normalize_wraps(float input, float expected) => Assert.Equal(expected, TrunkCoords.Normalize(input), 3);

    [Fact]
    public void Stored_only_inside_the_clamp_range()
    {
        Assert.True(TrunkCoords.IsStored(1.0f, 45f));
        Assert.False(TrunkCoords.IsStored(TrunkCoords.Unset, 45f));
        Assert.False(TrunkCoords.IsStored(1.0f, float.NaN));
        Assert.False(TrunkCoords.IsStored(3f, 45f));
    }
}
