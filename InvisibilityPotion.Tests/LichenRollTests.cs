using System;
using InvisibilityPotion.Plants;
using Xunit;

public class LichenRollTests
{
    [Fact]
    public void Deterministic() => Assert.Equal(LichenRoll.Value(42, 123.45f, -678.9f), LichenRoll.Value(42, 123.45f, -678.9f));

    [Fact]
    public void Seeds_and_salts_differ()
    {
        var a = LichenRoll.Value(1, 100f, 200f);
        Assert.NotEqual(a, LichenRoll.Value(2, 100f, 200f));
        Assert.NotEqual(a, LichenRoll.Value(1, 100f, 200f, "other"));
        Assert.NotEqual(a, LichenRoll.Value(1, 200f, 100f));   // x and z are not interchangeable
    }

    [Fact]
    public void Values_are_in_unit_interval()
    {
        for (var i = 0; i < 1000; i++)
        {
            var v = LichenRoll.Value(i, i * 3.7f, -i * 1.3f);
            Assert.InRange(v, 0.0, 0.9999999999);
        }
    }

    [Fact]
    public void Rate_matches_chance_over_200k_positions()
    {
        // 200k Bernoulli trials at p = 0.025 have sigma 0.00035; the bound is about 3 sigma.
        var hits = 0;
        const int n = 200_000;
        for (var i = 0; i < n; i++)
        {
            var x = (i % 500) * 3.3f - 800f;
            var z = (i / 500) * 2.9f - 600f;
            if (LichenRoll.Has(123456, x, z, 0.025)) hits++;
        }
        Assert.InRange(hits / (double)n, 0.025 - 0.001, 0.025 + 0.001);
    }

    [Fact]
    public void Zero_chance_never_hits() => Assert.False(LichenRoll.Has(1, 0f, 0f, 0));

    [Theory]
    [InlineData(0.04f, 0)]
    [InlineData(0.05f, 1)]     // half up
    [InlineData(-0.05f, 0)]    // half up also below zero: floor(-0.5 + 0.5) = 0
    [InlineData(-0.06f, -1)]
    [InlineData(1234.56f, 12346)]
    public void Quantize_rounds_half_up(float v, int expected) => Assert.Equal(expected, LichenRoll.Quantize(v));

    [Fact]
    public void Cell_centres_are_stable_under_four_centimetre_jitter()
    {
        // A cell is 0.1 m wide: a point within 0.01 m of a cell centre stays in its cell for any jitter up to 0.04 m.
        var rng = new Random(7);
        for (var i = 0; i < 2000; i++)
        {
            var cx = rng.Next(-50000, 50000) * 0.1;
            var cz = rng.Next(-50000, 50000) * 0.1;
            var x = (float)(cx + (rng.NextDouble() - 0.5) * 0.02);
            var z = (float)(cz + (rng.NextDouble() - 0.5) * 0.02);
            var jx = (float)(x + (rng.NextDouble() * 2 - 1) * 0.04);
            var jz = (float)(z + (rng.NextDouble() * 2 - 1) * 0.04);
            Assert.Equal(LichenRoll.Value(99, x, z), LichenRoll.Value(99, jx, jz));
        }
    }

    [Fact]
    public void General_positions_are_stable_under_float_noise_except_at_cell_edges()
    {
        // float32 at 10 km has a resolution of about 1 mm; positions further than 2 mm from an edge never flip.
        var rng = new Random(11);
        var checkedCount = 0;
        for (var i = 0; i < 5000; i++)
        {
            var x = (float)((rng.NextDouble() - 0.5) * 20000);
            var z = (float)((rng.NextDouble() - 0.5) * 20000);
            if (NearEdge(x) || NearEdge(z)) continue;
            checkedCount++;
            var jx = x + (float)((rng.NextDouble() * 2 - 1) * 0.001);
            var jz = z + (float)((rng.NextDouble() * 2 - 1) * 0.001);
            Assert.Equal(LichenRoll.Value(5, x, z), LichenRoll.Value(5, jx, jz));
        }
        Assert.True(checkedCount > 4000);
    }

    private static bool NearEdge(float v)
    {
        var frac = v / 0.1 + 0.5 - Math.Floor(v / 0.1 + 0.5);
        return frac < 0.02 || frac > 0.98;
    }

    [Fact]
    public void Angle_in_range_and_independent_of_presence()
    {
        var a = LichenRoll.AngleDegrees(3, 10f, 20f);
        Assert.InRange(a, 0f, 360f);
        Assert.NotEqual(LichenRoll.Value(3, 10f, 20f) * 360.0, (double)a, 3);
    }

    [Fact]
    public void Plant_yaw_is_deterministic_spread_and_independent_of_variant()
    {
        Assert.Equal(LichenRoll.YawDegrees(12.3f, -45.6f), LichenRoll.YawDegrees(12.3f, -45.6f));
        var buckets = new int[4];
        var rng = new Random(7);
        for (var i = 0; i < 4000; i++)
        {
            var y = LichenRoll.YawDegrees((float)(rng.NextDouble() * 2000 - 1000), (float)(rng.NextDouble() * 2000 - 1000));
            Assert.InRange(y, 0f, 359.9999f);
            buckets[(int)(y / 90f)]++;
        }
        foreach (var b in buckets) Assert.InRange(b, 850, 1150);
        Assert.NotEqual(LichenRoll.Value(0, 10f, 20f, LichenRoll.VariantSalt) * 360.0, (double)LichenRoll.YawDegrees(10f, 20f), 3);
    }
}
