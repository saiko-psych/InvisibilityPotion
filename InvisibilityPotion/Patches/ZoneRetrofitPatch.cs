using HarmonyLib;
using InvisibilityPotion.Plants;
using UnityEngine;

namespace InvisibilityPotion.Patches
{
    /// <summary>
    /// 0.4.0: after the server has looked for ungenerated zones around a peer (ZoneSystem.CreateGhostZones, ZoneSystem.cs:1222,
    /// every 0.1 s per peer from Update :1185-1188, server only), <see cref="ZoneRetrofit"/> checks one already generated zone in
    /// the same window for missing wild plants. The original's return value is not used: it only says whether a zone was generated.
    /// </summary>
    [HarmonyPatch(typeof(ZoneSystem), "CreateGhostZones")]
    internal static class ZoneRetrofitPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Vector3 refPoint) => ZoneRetrofit.Run(refPoint);
    }
}
