using System;

namespace InvisibilityPotion.Plants
{
    /// <summary>
    /// Growth stage from timestamps (spec plan 5 §3.2/§3.3), no game types. A plant has stages 1..max; max is ripe (pickable).
    /// The ZDO holds the base stage written at the last change and the time of that change; absent keys (base stage 0) mean
    /// "never picked" = ripe. Every client evaluates the same function, so growth needs no timer and no RPC.
    /// Huldra's Hair: max 3 (S1 sprout, S2, S3 ripe), stageMinutes = LichenStageMinutes. Ground plants: max 2 (1 = picked
    /// model, 2 = ripe), stageMinutes = GroundRegrowMinutes.
    /// </summary>
    public static class HarvestStage
    {
        /// <summary>Stage written by a pick.</summary>
        public const int Picked = 1;

        /// <summary>
        /// Current stage: base stage plus one per full <paramref name="stageMinutes"/> elapsed, capped at <paramref name="maxStage"/>.
        /// Base 0 (no keys) = ripe; a base outside 1..max is clamped; negative or NaN elapsed time (clock change) keeps the base;
        /// stageMinutes &lt;= 0 means instant regrowth (ripe).
        /// </summary>
        public static int At(int baseStage, double elapsedMinutes, double stageMinutes, int maxStage)
        {
            if (maxStage < 1) maxStage = 1;
            if (baseStage <= 0) return maxStage;
            if (baseStage > maxStage) baseStage = maxStage;
            if (double.IsNaN(elapsedMinutes) || elapsedMinutes <= 0) return baseStage;
            if (double.IsNaN(stageMinutes) || stageMinutes <= 0) return maxStage;
            var steps = Math.Floor(elapsedMinutes / stageMinutes);
            if (steps >= maxStage) return maxStage;   // also guards the int cast for huge elapsed times
            var stage = baseStage + (int)steps;
            return stage > maxStage ? maxStage : stage;
        }

        public static bool IsRipe(int stage, int maxStage) => stage >= maxStage;

        /// <summary>Minutes until the next stage (0 when ripe or for invalid input); for ip_plants.</summary>
        public static double MinutesToNext(int baseStage, double elapsedMinutes, double stageMinutes, int maxStage)
        {
            var stage = At(baseStage, elapsedMinutes, stageMinutes, maxStage);
            if (stage >= maxStage || stageMinutes <= 0 || double.IsNaN(stageMinutes)) return 0;
            var elapsed = double.IsNaN(elapsedMinutes) || elapsedMinutes < 0 ? 0 : elapsedMinutes;
            var steps = Math.Floor(elapsed / stageMinutes);
            return (steps + 1) * stageMinutes - elapsed;
        }

        /// <summary>Elapsed minutes between two DateTime tick counts (ZNet.GetTime().Ticks); negative when the clock went back.</summary>
        public static double ElapsedMinutes(long nowTicks, long thenTicks) => (nowTicks - thenTicks) / (double)TimeSpan.TicksPerMinute;
    }
}
