namespace InvisibilityPotion.Plants
{
    /// <summary>
    /// Deterministic "this tree carries Huldra's Hair" roll (spec plan 5 §3.2), no game types. The seed is the world seed plus the
    /// tree's position quantized to 0.1 m (trees never move; ZDOIDs change on every load, ZDO.cs:1298), mixed with a salt by
    /// splitmix64. No string.GetHashCode (differs between runtimes) and no UnityEngine.Random (shared global state).
    /// Positions closer than 0.05 m to a cell edge may flip under float noise; tree ZDO positions are stored floats, identical on
    /// every peer and across restarts, so this only matters for positions that are recomputed.
    /// </summary>
    public static class LichenRoll
    {
        public const string Salt = "IP_Lichen";
        public const string AngleSalt = "IP_LichenAngle";
        /// <summary>Salt of the ground plant model variant (VeilHarvest.Variants).</summary>
        public const string VariantSalt = "IP_PlantVariant";
        public const string YawSalt = "IP_PlantYaw";
        public const float CellSize = 0.1f;

        /// <summary>Position in 0.1 m cells, rounding half up (floor(v * 10 + 0.5)); identical for the same float everywhere.</summary>
        public static int Quantize(float v) => (int)System.Math.Floor(v / (double)CellSize + 0.5);

        /// <summary>Uniform value in [0, 1) for (seed, cell of x/z, salt).</summary>
        public static double Value(int seed, float x, float z, string salt = Salt)
        {
            var h = Mix(Fnv1a(salt));
            h = Mix(h ^ (uint)seed);
            h = Mix(h ^ (uint)Quantize(x));
            h = Mix(h ^ ((ulong)(uint)Quantize(z) << 1));
            return (h >> 11) * (1.0 / (1UL << 53));
        }

        public static bool Has(int seed, float x, float z, double chance) => chance > 0 && Value(seed, x, z) < chance;

        /// <summary>Angle around the trunk in degrees [0, 360), independent of the presence roll.</summary>
        public static float AngleDegrees(int seed, float x, float z) => (float)(Value(seed, x, z, AngleSalt) * 360.0);

        /// <summary>
        /// Yaw of a ground plant's models in degrees [0, 360) from its position (round N ruling 4): stored nowhere, every client
        /// derives the same value from the ZDO position. World seed 0 (positions differ anyway), own salt (independent of the variant).
        /// </summary>
        public static float YawDegrees(float x, float z) => (float)(Value(0, x, z, YawSalt) * 360.0);

        /// <summary>splitmix64 finaliser (Steele, Lea, Flood 2014; public domain constants).</summary>
        public static ulong Mix(ulong z)
        {
            z += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>FNV-1a 64 over the UTF-16 code units of <paramref name="s"/> (stable across runtimes).</summary>
        public static ulong Fnv1a(string s)
        {
            var h = 14695981039346656037UL;
            foreach (var c in s ?? "")
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            return h;
        }
    }
}
