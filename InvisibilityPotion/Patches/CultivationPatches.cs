using System;
using HarmonyLib;
using InvisibilityPotion.Plants;

namespace InvisibilityPotion.Patches
{
    /// <summary>
    /// 0.3.1: a ground plant grown from one of our saplings gets ZDO key IP_Cultivated = true, so the misplaced-plant check
    /// (<see cref="VeilHarvest"/>) never removes it. Plant.Grow (Plant.cs:181-210) instantiates the grown prefab on the sapling's
    /// owner and returns it (null when the sapling is not healthy); the new ZNetView's ZDO is owned by this peer, and VeilHarvest.Start
    /// runs a frame later, after the key is set.
    /// </summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.Grow))]
    internal static class PlantGrowPatch
    {
        private static bool _loggedError;

        [HarmonyPostfix]
        private static void Postfix(Plant __instance, UnityEngine.GameObject __result)
        {
            try
            {
                if (__result == null || __result.GetComponent<VeilHarvest>() == null) return;
                var nview = __result.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid()) return;
                nview.GetZDO().Set(VeilHarvest.CultivatedHash, true);
                // 0.4.1: one Info line per grown sprout, so a dedicated server log proves cultivation without a client (server test report 2026-10-11, point 5).
                var pos = __result.transform.position;
                var zone = ZoneSystem.GetZone(pos);
                var planted = __instance != null && __instance.m_nview != null && __instance.m_nview.IsValid() ? __instance.m_nview.GetZDO().GetLong(ZDOVars.s_plantTime, 0L) : 0L;
                var minutes = planted > 0 && ZNet.instance != null ? (ZNet.instance.GetTime().Ticks - planted) / (double)TimeSpan.TicksPerMinute : -1;
                Plugin.Log.LogInfo($"cultivation: {(__instance != null ? __instance.name.Replace("(Clone)", "") : "sprout")} grew into {__result.name.Replace("(Clone)", "")} " +
                                   $"at ({pos.x:F0}, {pos.z:F0}) zone ({zone.x},{zone.y}){(minutes >= 0 ? $" after {minutes:F0} min" : "")}");
            }
            catch (Exception e)
            {
                if (_loggedError) return;
                _loggedError = true;
                Plugin.Log.LogWarning($"plants: marking a cultivated plant failed: {e}");
            }
        }
    }
}
