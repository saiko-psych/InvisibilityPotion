using InvisibilityPotion.Config;
using Xunit;

public class TierRangeTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 0)]
    [InlineData(-1, 0)]
    [InlineData(int.MaxValue, 0)]
    [InlineData(int.MinValue, 0)]
    public void Clamp_KeepsOnlyOneToThree(int input, int expected)
    {
        Assert.Equal(expected, TierRange.Clamp(input));
        Assert.Equal(expected != 0, TierRange.IsValid(input));
    }
}
