using InvisibilityPotion.Plants;
using Xunit;
using R = InvisibilityPotion.Plants.PlantPlacement.Reason;

public class PlantPlacementTests
{
    private const float Min = 0.5f;
    private const float Lava = 0.15f;

    [Theory]
    [InlineData(5f, R.None)]
    [InlineData(0.5f, R.None)]
    [InlineData(0.47f, R.None)]   // wild plant: ground offset -0.03 below the checked ground height
    [InlineData(0.41f, R.None)]
    [InlineData(0.39f, R.BelowSeaLevel)]
    [InlineData(0f, R.BelowSeaLevel)]
    [InlineData(-3f, R.BelowSeaLevel)]
    public void Altitude(float altitude, R expected) => Assert.Equal(expected, PlantPlacement.Misplaced(altitude, Min, 0f, Lava, false, false));

    [Theory]
    [InlineData(0f, R.None)]
    [InlineData(0.15f, R.None)]
    [InlineData(0.16f, R.OnLava)]
    [InlineData(1f, R.OnLava)]
    [InlineData(float.NaN, R.None)]
    public void Fern_lava(float mask, R expected) => Assert.Equal(expected, PlantPlacement.Misplaced(3f, Min, mask, Lava, true, false));

    [Fact]
    public void Lava_mask_ignored_for_non_fern() => Assert.Equal(R.None, PlantPlacement.Misplaced(3f, Min, 1f, Lava, false, false));

    [Fact]
    public void Water_wins_over_lava() => Assert.Equal(R.BelowSeaLevel, PlantPlacement.Misplaced(-1f, Min, 1f, Lava, true, false));

    [Theory]
    [InlineData(-5f, 1f, true)]
    [InlineData(-5f, 0f, false)]
    [InlineData(3f, 1f, true)]
    public void Cultivated_never_misplaced(float altitude, float mask, bool fern) =>
        Assert.Equal(R.None, PlantPlacement.Misplaced(altitude, Min, mask, Lava, fern, true));

    [Theory]
    [InlineData(R.None, "ok")]
    [InlineData(R.BelowSeaLevel, "below sea level")]
    [InlineData(R.OnLava, "on lava")]
    public void Describe(R reason, string text) => Assert.Equal(text, PlantPlacement.Describe(reason));
}
