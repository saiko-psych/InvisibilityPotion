#if DEBUG
using System;

namespace InvisibilityPotion.Dev
{
    /// <summary>
    /// Pure placement of an ip_spawn plant group: members on a jittered ring around the group centre instead of a straight row
    /// (plants round 2026-10-02). No game types, so it can be unit tested.
    /// </summary>
    internal static class SpawnRing
    {
        public const float MinRadius = 1.5f;
        public const float MaxRadius = 4f;
        /// <summary>Angular jitter of each member as a fraction of the even spacing (±).</summary>
        public const double Jitter = 0.35;

        /// <summary>
        /// Horizontal (x, z) offsets from the group centre for <paramref name="count"/> members: one member stays at the centre;
        /// more get evenly spaced angles from a random start, each jittered by ±<see cref="Jitter"/> of the spacing, and a random
        /// radius in [<see cref="MinRadius"/>, <see cref="MaxRadius"/>].
        /// </summary>
        public static (float x, float z)[] Offsets(int count, Random rng)
        {
            if (count <= 0) return new (float, float)[0];
            if (count == 1) return new[] { (0f, 0f) };
            var result = new (float x, float z)[count];
            var step = 2 * Math.PI / count;
            var start = rng.NextDouble() * 2 * Math.PI;
            for (var i = 0; i < count; i++)
            {
                var angle = start + i * step + (rng.NextDouble() * 2 - 1) * Jitter * step;
                var r = MinRadius + (MaxRadius - MinRadius) * rng.NextDouble();
                result[i] = ((float)(Math.Cos(angle) * r), (float)(Math.Sin(angle) * r));
            }
            return result;
        }
    }
}
#endif
