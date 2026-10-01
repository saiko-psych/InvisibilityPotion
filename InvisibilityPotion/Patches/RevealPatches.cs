using HarmonyLib;
using InvisibilityPotion.Config;
using InvisibilityPotion.Effects;

namespace InvisibilityPotion.Patches
{
    /// <summary>All reveal triggers run on the hidden player's own client and only call MarkRevealed (decompile-notes §Reveal triggers).</summary>
    internal static class Reveal
    {
        internal static void Mark(Player p, RevealReason reason)
        {
            if (p == null || p != Player.m_localPlayer) return;
            SE_Invisibility.ActiveOn(p)?.MarkRevealed(reason);
        }
    }

    /// <summary>Damaging a creature. Character.Damage runs on the attacker's machine before the RPC (Character.cs:2232).</summary>
    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    internal static class DamagePatch
    {
        [HarmonyPrefix]
        private static void Prefix(Character __instance, HitData hit)
        {
            if (__instance == null || hit == null) return;
            var attacker = hit.GetAttacker() as Player;
            if (attacker == null || attacker == __instance) return;
            Reveal.Mark(attacker, RevealReason.DamageDealt);
        }
    }

    /// <summary>Drawing a bow (Attack.cs:346, called from Player.UpdateAttackBowDraw).</summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.StartDraw))]
    internal static class StartDrawPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Humanoid character, bool __result)
        {
            if (!__result || !PluginConfig.Global.RevealOnBowDraw) return;
            Reveal.Mark(character as Player, RevealReason.BowDraw);
        }
    }

    /// <summary>Starting a staff cast. Melee swings are not revealed here (they reveal on hit via DamagePatch), so harvesting stays silent.</summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
    internal static class StartAttackPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Humanoid __instance, bool __result)
        {
            if (!__result) return;
            var p = __instance as Player;
            if (p == null) return;
            var weapon = p.GetCurrentWeapon();
            var skill = weapon?.m_shared?.m_skillType;
            if (skill == Skills.SkillType.ElementalMagic || skill == Skills.SkillType.BloodMagic)
                Reveal.Mark(p, RevealReason.StaffCast);
        }
    }

    /// <summary>A blocked hit or parry (Humanoid.cs:1751, runs inside RPC_Damage on the victim's owner).</summary>
    [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
    internal static class BlockAttackPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Humanoid __instance, bool __result)
        {
            if (!__result || !PluginConfig.Global.RevealOnBlock) return;
            Reveal.Mark(__instance as Player, RevealReason.Block);
        }
    }
}
