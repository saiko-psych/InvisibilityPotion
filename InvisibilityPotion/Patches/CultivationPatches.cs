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
        private static void Postfix(UnityEngine.GameObject __result)
        {
            try
            {
                if (__result == null || __result.GetComponent<VeilHarvest>() == null) return;
                var nview = __result.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid()) return;
                nview.GetZDO().Set(VeilHarvest.CultivatedHash, true);
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
