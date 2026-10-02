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
            // Damage-over-time ticks (SE_Burning.cs:45, SE_Poison.cs:37) must not re-reveal a re-hidden player.
            switch (hit.m_hitType)
            {
                case HitData.HitType.Burning:
                case HitData.HitType.Freezing:
                case HitData.HitType.Poisoned:
                    return;
            }
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

    /// <summary>Starting a staff cast, or an axe/pickaxe swing when [General] RevealOnToolUse is on (any target: tree, rock, ore, creature, air).
    /// Other melee swings are not revealed here (they reveal on hit via DamagePatch). Hammer, hoe and cultivator never reach StartAttack (place mode
    /// returns early in Player.PlayerAttackInput); they reveal through PlacePiecePatch.</summary>
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
            else if ((skill == Skills.SkillType.Axes || skill == Skills.SkillType.Pickaxes) && PluginConfig.Global.RevealOnToolUse)
                Reveal.Mark(p, RevealReason.ToolUse);
        }
    }

    /// <summary>Building with the hammer, levelling/paving/digging with the hoe (terrain ops are pieces) and planting with the cultivator
    /// (Player.TryPlacePiece -> PlacePiece, Player.cs:3082). doAttack is false only for pieces spawned by a weapon attack (Attack.cs:1509), which are not tool use.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
    internal static class PlacePiecePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance, bool doAttack)
        {
            if (!doAttack || !PluginConfig.Global.RevealOnToolUse) return;
            Reveal.Mark(__instance, RevealReason.ToolUse);
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
