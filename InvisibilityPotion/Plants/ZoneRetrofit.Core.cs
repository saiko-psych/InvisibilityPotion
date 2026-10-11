using System;
using System.Collections.Generic;

namespace InvisibilityPotion.Plants
{
    /// <summary>
    /// Which already generated zone the retrofit (0.4.0, wild plants for zones generated before the mod) looks at next, no game
    /// types. The server calls <see cref="Next"/> once per peer per tick (ZoneSystem.CreateGhostZones runs every 0.1 s per peer):
    /// the nearest unchecked generated zone within the radius, ring by ring (Chebyshev distance, then row-major), so the zones a
    /// player stands in come first. Every returned zone is remembered as checked for the session; <see cref="Unmark"/> gives a
    /// zone back when the terrain was not ready yet.
    /// </summary>
    public sealed class ZoneRetrofitPlanner
    {
        private readonly HashSet<(int x, int y)> _checked = new HashSet<(int x, int y)>();

        public int CheckedCount => _checked.Count;

        public bool IsChecked(int x, int y) => _checked.Contains((x, y));

        public void Reset() => _checked.Clear();

        public void Unmark(int x, int y) => _checked.Remove((x, y));

        /// <summary>
        /// The next zone to check around (<paramref name="cx"/>, <paramref name="cy"/>) within <paramref name="radius"/> zones:
        /// nearest ring first, skipping zones that are not generated (<paramref name="isGenerated"/> false) or already checked.
        /// Returns false when nothing is left in the window. The returned zone is marked checked.
        /// </summary>
        public bool Next(int cx, int cy, int radius, Func<int, int, bool> isGenerated, out int zx, out int zy)
        {
            if (isGenerated == null) throw new ArgumentNullException(nameof(isGenerated));
            for (var ring = 0; ring <= radius; ring++)
            {
                for (var y = cy - ring; y <= cy + ring; y++)
                {
                    for (var x = cx - ring; x <= cx + ring; x++)
                    {
                        if (Math.Max(Math.Abs(x - cx), Math.Abs(y - cy)) != ring) continue;   // the ring's border only
                        if (_checked.Contains((x, y))) continue;
                        if (!isGenerated(x, y)) continue;
                        _checked.Add((x, y));
                        zx = x;
                        zy = y;
                        return true;
                    }
                }
            }
            zx = zy = 0;
            return false;
        }
    }

    /// <summary>
    /// 0.4.1: counts per outcome for the log and ip_retrofit, no game types. The server log only showed zones that got plants, so a
    /// zone that was evaluated and rejected looked the same as one never visited; <see cref="Summary"/> is logged every
    /// <see cref="LogEvery"/> evaluated zones and on demand.
    /// </summary>
    public sealed class RetrofitStats
    {
        public const int LogEvery = 25;
        private readonly Dictionary<RetrofitOutcome, int> _counts = new Dictionary<RetrofitOutcome, int>();

        /// <summary>Zones whose check produced a verdict (everything but Skipped and TerrainNotReady).</summary>
        public int Evaluated { get; private set; }
        public int PlacedZones { get; private set; }
        public int PlacedPlants { get; private set; }

        public int Count(RetrofitOutcome outcome) => _counts.TryGetValue(outcome, out var n) ? n : 0;

        public void Reset()
        {
            _counts.Clear();
            Evaluated = PlacedZones = PlacedPlants = 0;
        }

        /// <summary>Records one check; returns true when a summary line is due (every <see cref="LogEvery"/> evaluated zones).</summary>
        public bool Add(RetrofitOutcome outcome, int placed)
        {
            _counts[outcome] = Count(outcome) + 1;
            if (outcome == RetrofitOutcome.Skipped || outcome == RetrofitOutcome.TerrainNotReady) return false;
            Evaluated++;
            if (outcome == RetrofitOutcome.Placed) { PlacedZones++; PlacedPlants += placed; }
            return Evaluated % LogEvery == 0;
        }

        /// <summary>"N zones evaluated: P got plants (Q plants), H already had plants, R roll missed or wrong biome, S roll hit but no valid spot".</summary>
        public string Summary(bool reportOnly) =>
            $"{Evaluated} zones evaluated: {PlacedZones} {(reportOnly ? "would get" : "got")} plants ({PlacedPlants} plants), " +
            $"{Count(RetrofitOutcome.HasPlants)} already had plants, {Count(RetrofitOutcome.NoRoll)} roll missed or wrong biome, " +
            $"{Count(RetrofitOutcome.NoSpot)} roll hit but no valid spot";
    }

    /// <summary>Outcome of one zone check, for the log and ip_retrofit.</summary>
    public enum RetrofitOutcome
    {
        /// <summary>Retrofit is off, no server, no vegetation registered: nothing done, zone not marked.</summary>
        Skipped,
        /// <summary>The zone already holds a wild Baldr's Tear or Hel's Ember Fern: nothing to do.</summary>
        HasPlants,
        /// <summary>Terrain data not ready yet: the zone is unmarked and tried again later.</summary>
        TerrainNotReady,
        /// <summary>The zone chance roll missed (deterministic: it misses every time): nothing placed.</summary>
        NoRoll,
        /// <summary>Plants were placed.</summary>
        Placed,
        /// <summary>The roll hit but no point passed the placement rules (water, lava, blocked, tilt).</summary>
        NoSpot,
    }
}
