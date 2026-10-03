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
