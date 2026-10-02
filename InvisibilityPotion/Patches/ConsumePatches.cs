using HarmonyLib;
using InvisibilityPotion.Config;
using InvisibilityPotion.Effects;

namespace InvisibilityPotion.Patches
{
    /// <summary>
    /// Refuses a veil mead before the bottle is consumed (Player.cs:6026; ConsumeItem ignores Setup's result): while the shared
    /// Veil Cooldown runs (round Q, vanilla message $msg_cantconsume as for a potion on cooldown, Player.cs:6041), and a lower or
    /// equal tier while a higher one is active.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.CanConsumeItem), new[] { typeof(ItemDrop.ItemData), typeof(bool) })]
    internal static class CanConsumeItemPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!__result || item?.m_shared?.m_consumeStatusEffect == null) return;
            var incoming = item.m_shared.m_consumeStatusEffect as SE_Invisibility;
            if (incoming == null) return;
            var cooldown = __instance.GetSEMan().GetStatusEffect(StatusEffects.CooldownHash);
            if (VeilCooldown.Refuses(true, cooldown != null ? cooldown.GetRemaningTime() : 0f))
            {
                __instance.Message(MessageHud.MessageType.Center, "$msg_cantconsume");
                __result = false;
                return;
            }
            var active = SE_Invisibility.ActiveOn(__instance);
            if (active == null || incoming.Tier > active.Tier) return;
            __instance.Message(MessageHud.MessageType.Center, "$ip_msg_lower_tier");
            __result = false;
        }
    }
}
