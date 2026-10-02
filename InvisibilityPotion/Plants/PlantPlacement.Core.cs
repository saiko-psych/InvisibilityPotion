namespace InvisibilityPotion.Plants
{
    /// <summary>
    /// Self-check of wild ground plants placed before the dry-land rules (0.3.1): the same limits PlaceVegetation applies to new
    /// zones (PlantVegetation.MinAltitude, fern lava mask PlantVegetation.FernMaxLavaMask), applied to
    /// plants that already stand in old zones. No game types.
    /// </summary>
    public static class PlantPlacement
    {
        public enum Reason
        {
            None,
            BelowSeaLevel,
            OnLava,
        }

        /// <summary>
        /// Slack below the minimum altitude: a wild plant sits m_groundOffset (-0.03 m) below the ground height that PlaceVegetation
        /// checked, and the heightmap can differ from the terrain raycast by a few centimetres.
        /// </summary>
        public const float AltitudeTolerance = 0.1f;

        /// <summary>
        /// Why a ground plant is misplaced, or <see cref="Reason.None"/>. <paramref name="altitude"/> is the root's y minus 30 (sea
        /// level); <paramref name="lavaMask"/> the paint-mask alpha at the root (NaN = unknown, never lava). Cultivated plants are never
        /// misplaced: the player chose the spot.
        /// </summary>
        public static Reason Misplaced(float altitude, float minAltitude, float lavaMask, float maxLava, bool isFern, bool cultivated)
        {
            if (cultivated) return Reason.None;
            if (altitude < minAltitude - AltitudeTolerance) return Reason.BelowSeaLevel;
            if (isFern && lavaMask > maxLava) return Reason.OnLava;   // NaN compares false
            return Reason.None;
        }

        public static string Describe(Reason reason) =>
            reason == Reason.BelowSeaLevel ? "below sea level" : reason == Reason.OnLava ? "on lava" : "ok";
    }
}
