using System;
using InvisibilityPotion.Visuals;

namespace InvisibilityPotion.Config
{
    /// <summary>
    /// Which orphaned config keys (in the file, bound by nobody) the startup cleanup may delete, no game types. Only keys that older
    /// layouts of this mod are known to have used: deleting every orphan also deleted a newer version's keys after a downgrade
    /// (with their customised values) and the Debug-only [Dev] keys in a Release build (review 2026-11-04). Unknown keys stay;
    /// BepInEx keeps an orphan in the file untouched.
    /// </summary>
    public static class ConfigCleanup
    {
        /// <summary>True for a key of an older layout: [Fog] FogRate*/Anchor.*, [TierN] FogEnabled/FogDensity, [Veil] Distortion*,
        /// [General] FogRate*/BodyVeilMode, [Fog.TierN] the old outer factor keys (<see cref="FogSettings.ObsoleteKeys"/>).</summary>
        public static bool IsObsolete(string section, string key)
        {
            if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(key)) return false;
            switch (section)
            {
                case "Fog":
                    return key.StartsWith("FogRate", StringComparison.Ordinal) || key.StartsWith("Anchor.", StringComparison.Ordinal);
                case "Tier1":
                case "Tier2":
                case "Tier3":
                    return key == "FogEnabled" || key == "FogDensity";
                case "Veil":
                    return key.StartsWith("Distortion", StringComparison.Ordinal);
                case "General":
                    return key.StartsWith("FogRate", StringComparison.Ordinal) || key == "BodyVeilMode";
                case "Fog.Tier1":
                case "Fog.Tier2":
                case "Fog.Tier3":
                    return Array.IndexOf(FogSettings.ObsoleteKeys, key) >= 0;
                default:
                    return false;
            }
        }
    }
}
