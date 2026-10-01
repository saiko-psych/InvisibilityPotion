using HarmonyLib;
using InvisibilityPotion.Config;
using InvisibilityPotion.Net;

namespace InvisibilityPotion.Patches
{
    /// <summary>Lose a hidden target after the tier's AggroLossTime instead of vanilla's inlined 30 s (MonsterAI.cs:336). Runs on the monster's owner from ZDO state only.</summary>
    [HarmonyPatch(typeof(MonsterAI), "UpdateTarget")]
    internal static class UpdateTargetPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MonsterAI __instance)
        {
            var target = __instance.m_targetCreature;
            if (target == null) return;
            var tier = HiddenState.HiddenTier(target);
            if (tier == 0) return;
            if (__instance.m_timeSinceSensedTargetCreature <= PluginConfig.Tier(tier).AggroLossTime) return;
            // Same drop vanilla performs at 30 s.
            __instance.SetAlerted(false);
            __instance.m_targetCreature = null;
            __instance.m_targetStatic = null;
            __instance.m_timeSinceAttacking = 0f;
            __instance.m_updateTargetTimer = 5f;
        }
    }

    /// <summary>Tier II/III do not wake sleeping monsters. Vanilla wakes on the closest player in range without perception (MonsterAI.cs:817). Hunting monsters keep vanilla behaviour.</summary>
    [HarmonyPatch(typeof(MonsterAI), "UpdateSleep")]
    internal static class UpdateSleepPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MonsterAI __instance, float dt)
        {
            if (!__instance.IsSleeping() || __instance.HuntPlayer()) return true;
            var range = __instance.m_wakeupRange > __instance.m_maxNoiseWakeupRange ? __instance.m_wakeupRange : __instance.m_maxNoiseWakeupRange;
            if (range <= 0f) return true;
            var closest = Player.GetClosestPlayer(__instance.transform.position, range);
            if (closest == null || !HiddenState.IsIgnoredByEnemies(closest)) return true;
            // Closest player is hidden: skip this tick's wake-up checks but keep the sleep timer running like vanilla.
            __instance.m_sleepTimer += dt;
            return false;
        }
    }
}
