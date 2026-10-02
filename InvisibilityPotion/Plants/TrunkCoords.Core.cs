using System;

namespace InvisibilityPotion.Plants
{
    /// <summary>
    /// Trunk-relative coordinates of a Huldra's Hair patch, no game types: height above the tree base (metres, world) and angle
    /// around the trunk axis in degrees [0, 360), 0 = world +Z, 90 = world +X (the direction of Quaternion.Euler(0, angle, 0) *
    /// Vector3.forward). The sapling ghost snaps to (height, angle) and the planted patch on the tree is rebuilt from the same pair
    /// (ZDO keys IP_LichenH / IP_LichenAngle), so both land on the same trunk point.
    /// </summary>
    public static class TrunkCoords
    {
        /// <summary>Lowest and highest patch centre above the tree base, metres.</summary>
        public const float MinHeight = 0.6f;
        public const float MaxHeight = 2.2f;
        /// <summary>Distance the patch sits outward from the trunk surface, metres.</summary>
        public const float SurfaceOffset = 0.025f;
        /// <summary>Stored height when no planted position is set (wild lichen, cleared by ip_lichen).</summary>
        public const float Unset = -1f;

        /// <summary>Height clamped to [<see cref="MinHeight"/>, <see cref="MaxHeight"/>]; NaN gives the minimum.</summary>
        public static float ClampHeight(float h) => float.IsNaN(h) ? MinHeight : Math.Min(MaxHeight, Math.Max(MinHeight, h));

        /// <summary>Angle of the horizontal offset (dx, dz) from the trunk axis; a zero offset gives 0.</summary>
        public static float AngleDegrees(float dx, float dz)
        {
            if (Math.Abs(dx) < 1e-6f && Math.Abs(dz) < 1e-6f) return 0f;
            return Normalize((float)(Math.Atan2(dx, dz) * 180.0 / Math.PI));
        }

        /// <summary>Unit horizontal direction of <paramref name="angleDegrees"/> (x = sin, z = cos).</summary>
        public static void Direction(float angleDegrees, out float x, out float z)
        {
            var a = angleDegrees * Math.PI / 180.0;
            x = (float)Math.Sin(a);
            z = (float)Math.Cos(a);
        }

        /// <summary>Angle in [0, 360); NaN or infinity gives 0.</summary>
        public static float Normalize(float angleDegrees)
        {
            if (float.IsNaN(angleDegrees) || float.IsInfinity(angleDegrees)) return 0f;
            var a = angleDegrees % 360f;
            if (a < 0f) a += 360f;
            return a >= 360f ? 0f : a;
        }

        /// <summary>True when the tree ZDO holds a planted position (height inside the clamp range, angle finite).</summary>
        public static bool IsStored(float height, float angleDegrees) =>
            !float.IsNaN(height) && height >= MinHeight - 0.001f && height <= MaxHeight + 0.001f &&
            !float.IsNaN(angleDegrees) && !float.IsInfinity(angleDegrees);
    }
}
