using HarmonyLib;
using InvisibilityPotion.Net;
using UnityEngine;

namespace InvisibilityPotion.Patches
{
    /// <summary>Tier II/III: enemies neither hear nor see the player. Patched where vanilla checks ghost mode (decompile-notes §Perception). Runs on the monster's owner.</summary>
    [HarmonyPatch]
    internal static class PerceptionPatches
    {
        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanHearTarget), new[] { typeof(Transform), typeof(float), typeof(Character) })]
        [HarmonyPrefix]
        private static bool CanHearTarget_Prefix(Character target, ref bool __result)
        {
            if (!HiddenState.IsIgnoredByEnemies(target)) return true;
            __result = false;
            return false;
        }

        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanSeeTarget), new[] { typeof(Transform), typeof(Vector3), typeof(float), typeof(float), typeof(bool), typeof(bool), typeof(Character) })]
        [HarmonyPrefix]
        private static bool CanSeeTarget_Prefix(Character target, ref bool __result)
        {
            if (!HiddenState.IsIgnoredByEnemies(target)) return true;
            __result = false;
            return false;
        }

        /// <summary>HuntPlayer monsters (raids, events) pick the closest player without any perception check (BaseAI.cs:1419).</summary>
        [HarmonyPatch(typeof(BaseAI), "FindEnemy")]
        [HarmonyPostfix]
        private static void FindEnemy_Postfix(ref Character __result)
        {
            if (__result != null && HiddenState.IsIgnoredByEnemies(__result)) __result = null;
        }
    }
}
