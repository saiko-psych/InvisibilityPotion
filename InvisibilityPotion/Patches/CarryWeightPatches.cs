using HarmonyLib;
using InvisibilityPotion.Config;
using InvisibilityPotion.Effects;

namespace InvisibilityPotion.Patches
{
    /// <summary>[TierN] CarryWeightMultiplier while an invisibility tier is active (decompile-notes §Carry weight).
    /// A postfix on the final value instead of SE_Stats.m_addMaxCarryWeight or a ModifyMaxCarryWeight override: SEMan sums the effects
    /// in list order (SEMan.cs:403-409), so an effect-side reduction would miss a Megingjord equipped after drinking. Every reader
    /// (inventory weight text, encumbrance, auto pickup, tombstone) calls GetMaxCarryWeight each time, so apply and expiry show at once.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.GetMaxCarryWeight))]
    internal static class MaxCarryWeightPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance, ref float __result)
        {
            var inv = SE_Invisibility.ActiveOn(__instance);
            if (inv == null) return;
            __result = CarryWeight.Reduced(__result, PluginConfig.Tier(inv.Tier).CarryWeightMultiplier);
        }
    }
}
