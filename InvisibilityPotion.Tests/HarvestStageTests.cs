using InvisibilityPotion.Plants;
using Xunit;

public class HarvestStageTests
{
    [Fact]
    public void Absent_keys_are_ripe() => Assert.Equal(3, HarvestStage.At(0, 0, 120, 3));

    [Fact]
    public void Pick_writes_stage_one() => Assert.Equal(1, HarvestStage.At(HarvestStage.Picked, 0, 120, 3));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(119.99, 1)]
    [InlineData(120, 2)]
    [InlineData(239.99, 2)]
    [InlineData(240, 3)]
    [InlineData(100000, 3)]
    [InlineData(1e300, 3)]
    public void Lichen_boundaries(double elapsed, int expected) => Assert.Equal(expected, HarvestStage.At(1, elapsed, 120, 3));

    [Fact]
    public void Base_stage_two_needs_one_step() => Assert.Equal(3, HarvestStage.At(2, 120, 120, 3));

    [Fact]
    public void Base_above_max_is_capped() => Assert.Equal(3, HarvestStage.At(7, 0, 120, 3));

    [Theory]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    public void Negative_or_nan_elapsed_keeps_the_base(double elapsed) => Assert.Equal(1, HarvestStage.At(1, elapsed, 120, 3));

    [Fact]
    public void Zero_stage_minutes_regrow_at_once() => Assert.Equal(3, HarvestStage.At(1, 1, 0, 3));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(239, 1)]
    [InlineData(240, 2)]
    [InlineData(5000, 2)]
    public void Ground_two_stage_mode(double elapsed, int expected) => Assert.Equal(expected, HarvestStage.At(1, elapsed, 240, 2));

    [Fact]
    public void Ripe_check() 
    {
        Assert.True(HarvestStage.IsRipe(2, 2));
        Assert.False(HarvestStage.IsRipe(1, 2));
    }

    [Fact]
    public void Minutes_to_next()
    {
        Assert.Equal(120, HarvestStage.MinutesToNext(1, 0, 120, 3), 6);
        Assert.Equal(20, HarvestStage.MinutesToNext(1, 220, 120, 3), 6);
        Assert.Equal(0, HarvestStage.MinutesToNext(0, 0, 120, 3));
    }

    [Fact]
    public void Elapsed_minutes_from_ticks() => Assert.Equal(90, HarvestStage.ElapsedMinutes(System.TimeSpan.TicksPerMinute * 100, System.TimeSpan.TicksPerMinute * 10), 6);
}
