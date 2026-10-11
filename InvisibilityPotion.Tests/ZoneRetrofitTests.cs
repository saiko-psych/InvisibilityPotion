using System.Collections.Generic;
using InvisibilityPotion.Plants;
using Xunit;

public class ZoneRetrofitTests
{
    [Fact]
    public void Centre_zone_comes_first_then_the_ring_around_it()
    {
        var p = new ZoneRetrofitPlanner();
        Assert.True(p.Next(10, 20, 2, (x, y) => true, out var zx, out var zy));
        Assert.Equal((10, 20), (zx, zy));
        var ring1 = new List<(int, int)>();
        for (var i = 0; i < 8; i++)
        {
            Assert.True(p.Next(10, 20, 2, (x, y) => true, out zx, out zy));
            ring1.Add((zx, zy));
            Assert.Equal(1, System.Math.Max(System.Math.Abs(zx - 10), System.Math.Abs(zy - 20)));
        }
        Assert.Equal(8, new HashSet<(int, int)>(ring1).Count);
        Assert.True(p.Next(10, 20, 2, (x, y) => true, out zx, out zy));
        Assert.Equal(2, System.Math.Max(System.Math.Abs(zx - 10), System.Math.Abs(zy - 20)));
    }

    [Fact]
    public void Ungenerated_zones_are_skipped_and_not_marked()
    {
        var p = new ZoneRetrofitPlanner();
        Assert.True(p.Next(0, 0, 1, (x, y) => x == 1 && y == 0, out var zx, out var zy));
        Assert.Equal((1, 0), (zx, zy));
        Assert.False(p.IsChecked(0, 0));
        Assert.Equal(1, p.CheckedCount);
    }

    [Fact]
    public void A_checked_zone_is_never_returned_again_until_unmarked()
    {
        var p = new ZoneRetrofitPlanner();
        Assert.True(p.Next(0, 0, 0, (x, y) => true, out _, out _));
        Assert.False(p.Next(0, 0, 0, (x, y) => true, out _, out _));
        p.Unmark(0, 0);
        Assert.True(p.Next(0, 0, 0, (x, y) => true, out var zx, out var zy));
        Assert.Equal((0, 0), (zx, zy));
    }

    [Fact]
    public void Window_exhausted_returns_false_after_all_zones()
    {
        var p = new ZoneRetrofitPlanner();
        var n = 0;
        while (p.Next(5, 5, 1, (x, y) => true, out _, out _)) n++;
        Assert.Equal(9, n);
        Assert.Equal(9, p.CheckedCount);
        p.Reset();
        Assert.Equal(0, p.CheckedCount);
    }

    [Fact]
    public void Zones_outside_the_radius_are_not_visited()
    {
        var p = new ZoneRetrofitPlanner();
        var visited = new List<(int, int)>();
        while (p.Next(0, 0, 1, (x, y) => true, out var zx, out var zy)) visited.Add((zx, zy));
        Assert.DoesNotContain((2, 0), visited);
        Assert.DoesNotContain((0, -2), visited);
    }

    [Fact]
    public void Stats_count_outcomes_and_ask_for_a_summary_every_25_evaluated_zones()
    {
        var s = new RetrofitStats();
        Assert.False(s.Add(RetrofitOutcome.TerrainNotReady, 0));   // not evaluated
        Assert.False(s.Add(RetrofitOutcome.Skipped, 0));
        Assert.Equal(0, s.Evaluated);
        var due = 0;
        for (var i = 0; i < 24; i++) if (s.Add(RetrofitOutcome.NoRoll, 0)) due++;
        Assert.Equal(0, due);
        Assert.True(s.Add(RetrofitOutcome.Placed, 3));   // the 25th evaluated zone
        Assert.Equal(25, s.Evaluated);
        Assert.Equal(1, s.PlacedZones);
        Assert.Equal(3, s.PlacedPlants);
        Assert.Equal(24, s.Count(RetrofitOutcome.NoRoll));
        Assert.Equal(1, s.Count(RetrofitOutcome.TerrainNotReady));
        Assert.Equal("25 zones evaluated: 1 got plants (3 plants), 0 already had plants, 24 roll missed or wrong biome, 0 roll hit but no valid spot", s.Summary(false));
        Assert.StartsWith("25 zones evaluated: 1 would get plants", s.Summary(true));
        s.Reset();
        Assert.Equal(0, s.Evaluated);
        Assert.Equal(0, s.Count(RetrofitOutcome.NoRoll));
    }
}
