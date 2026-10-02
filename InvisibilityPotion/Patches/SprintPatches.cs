using HarmonyLib;
using InvisibilityPotion.Effects;

namespace InvisibilityPotion.Patches
{
    /// <summary>
    /// Round S ruling 3: no sprinting while Veil Broken runs (decompile-notes §Round S). SE_Stats has no sprint field, so a prefix
    /// on Player.CheckRun (protected override, Player.cs:2516; Character.UpdateWalking sets m_running from it every frame,
    /// Character.cs:1606) answers false while SE_Revealed is on the player. Skipping the original also skips the run stamina
    /// drain and the Run skill gain, exactly as for a player who does not sprint. The status effect is only read here, never added.
    /// </summary>
    [HarmonyPatch(typeof(Player), "CheckRun")]
    internal static class VeilBrokenSprintPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Player __instance, ref bool __result)
        {
            var seman = __instance.GetSEMan();
            if (seman == null || !seman.HaveStatusEffect(StatusEffects.RevealedHash)) return true;
            __result = false;
            return false;
        }
    }
}
