using HarmonyLib;
using InvisibilityPotion.Config;
using InvisibilityPotion.Net;

namespace InvisibilityPotion.Patches
{
    /// <summary>Vanilla applies stealth modifiers only while crouching (Player.cs:6961) and resets the target to 1 when standing (Player.cs:6966). For a hidden tier I player we keep the target at the configured fraction while standing. Runs on the player's owner; the factor reaches monster owners via ZDOVars.s_stealth.</summary>
    [HarmonyPatch(typeof(Player), "UpdateStealth")]
    internal static class UpdateStealthPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance)
        {
            if (__instance.IsCrouching()) return;
            var tier = HiddenState.HiddenTier(__instance);
            if (tier == 0) return;
            var cfg = PluginConfig.Tier(tier);
            if (cfg.IgnoredByEnemies) return;               // tier II/III are handled by the perception patches
            var fraction = cfg.StealthModifier;
            if (__instance.m_stealthFactorTarget > fraction) __instance.m_stealthFactorTarget = fraction;
        }
    }
}
