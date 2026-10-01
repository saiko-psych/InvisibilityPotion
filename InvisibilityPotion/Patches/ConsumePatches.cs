using HarmonyLib;
using InvisibilityPotion.Effects;

namespace InvisibilityPotion.Patches
{
    /// <summary>Refuse a lower or equal tier while a higher one is active, before the bottle is consumed (Player.cs:6026; ConsumeItem ignores Setup's result).</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.CanConsumeItem), new[] { typeof(ItemDrop.ItemData), typeof(bool) })]
    internal static class CanConsumeItemPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!__result || item?.m_shared?.m_consumeStatusEffect == null) return;
            var incoming = item.m_shared.m_consumeStatusEffect as SE_Invisibility;
            if (incoming == null) return;
            var active = SE_Invisibility.ActiveOn(__instance);
            if (active == null || incoming.Tier > active.Tier) return;
            __instance.Message(MessageHud.MessageType.Center, "$ip_msg_lower_tier");
            __result = false;
        }
    }
}
