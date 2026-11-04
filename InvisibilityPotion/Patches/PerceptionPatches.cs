using HarmonyLib;
using InvisibilityPotion.Net;
using UnityEngine;

namespace InvisibilityPotion.Patches
{
    /// <summary>
    /// Tier II/III: enemies neither hear nor see the player. Patched where vanilla checks ghost mode (decompile-notes §Perception). Runs on the monster's owner.
    /// Priority.Low (docs/compatibility.md): HarmonyX runs every prefix even after one returned false, so running last lets our
    /// false win over another mod's prefix that sets __result; the FindEnemy postfix likewise runs after other postfixes.
    /// </summary>
    [HarmonyPatch]
    internal static class PerceptionPatches
    {
        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanHearTarget), new[] { typeof(Transform), typeof(float), typeof(Character) })]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        private static bool CanHearTarget_Prefix(Character target, ref bool __result)
        {
            if (!HiddenState.IsIgnoredByEnemies(target)) return true;
            __result = false;
            return false;
        }

        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanSeeTarget), new[] { typeof(Transform), typeof(Vector3), typeof(float), typeof(float), typeof(bool), typeof(bool), typeof(Character) })]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        private static bool CanSeeTarget_Prefix(Character target, ref bool __result)
        {
            if (!HiddenState.IsIgnoredByEnemies(target)) return true;
            __result = false;
            return false;
        }

        /// <summary>
        /// HuntPlayer monsters (raids, events) pick the closest player without any perception check (BaseAI.cs:1419). A hidden result is
        /// replaced by the closest player they may target within the same 200 m (review 2026-11-04: returning null let a hidden player
        /// near the base stop a raid from hunting the visible teammates).
        /// </summary>
        [HarmonyPatch(typeof(BaseAI), "FindEnemy")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        private static void FindEnemy_Postfix(BaseAI __instance, ref Character __result)
        {
            if (__result == null || !HiddenState.IsIgnoredByEnemies(__result)) return;
            __result = __instance != null && __instance.HuntPlayer()
                ? HiddenState.ClosestTargetablePlayer(__instance.transform.position, HuntRange)
                : null;
        }

        private const float HuntRange = 200f;   // vanilla's FindEnemy fallback radius, BaseAI.cs:1419
    }
}
