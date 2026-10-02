using System;
using System.Linq;
using InvisibilityPotion.Dev;
using Xunit;

public class SpawnRingTests
{
    [Fact]
    public void Single_member_stays_at_the_centre() => Assert.Equal(new[] { (0f, 0f) }, SpawnRing.Offsets(1, new Random(1)));

    [Fact]
    public void No_members_no_offsets() => Assert.Empty(SpawnRing.Offsets(0, new Random(1)));

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(12)]
    public void Members_lie_on_the_ring(int count)
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var offsets = SpawnRing.Offsets(count, new Random(seed));
            Assert.Equal(count, offsets.Length);
            foreach (var (x, z) in offsets)
            {
                var r = Math.Sqrt(x * x + z * z);
                Assert.InRange(r, SpawnRing.MinRadius - 1e-4, SpawnRing.MaxRadius + 1e-4);
            }
        }
    }

    [Fact]
    public void Members_spread_round_the_centre_not_in_a_row()
    {
        // jitter is below half the spacing, so neighbouring angles stay at least (1 - 2 * Jitter) of the spacing apart
        const int count = 5;
        var step = 2 * Math.PI / count;
        for (var seed = 0; seed < 50; seed++)
        {
            var angles = SpawnRing.Offsets(count, new Random(seed)).Select(o => Math.Atan2(o.z, o.x)).OrderBy(a => a).ToList();
            angles.Add(angles[0] + 2 * Math.PI);
            for (var i = 0; i < count; i++)
                Assert.True(angles[i + 1] - angles[i] >= (1 - 2 * SpawnRing.Jitter) * step - 1e-9);
        }
    }
}
