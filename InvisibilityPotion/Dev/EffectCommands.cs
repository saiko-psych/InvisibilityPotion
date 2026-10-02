#if DEBUG
using Jotunn.Entities;
using Jotunn.Managers;

namespace InvisibilityPotion.Dev
{
    /// <summary>Debug-only status effect commands (round L): ip_end. Output goes to the console and Plugin.Log.</summary>
    internal static class EffectCommands
    {
        public static void Register()
        {
            CommandManager.Instance.AddConsoleCommand(new EndCommand());
        }

        /// <summary>
        /// ip_end: removes every InvisibilityPotion status effect (SE_Invisibility_T1..T3 and SE_IP_Revealed) from the local player
        /// through SEMan.RemoveStatusEffect(int nameHash, bool quiet) (decompile-notes: public bool, SEMan.cs:219; calls Stop() and
        /// removes the effect at once). A console command runs outside SEMan.Update and OnDamaged, so the removal is safe, and the
        /// tier effect's Stop() is the one cleanup path (CLAUDE.md).
        /// </summary>
        private class EndCommand : ConsoleCommand
        {
            public override string Name => "ip_end";
            public override string Help => "ip_end: remove every InvisibilityPotion status effect (tiers 1-3 and Revealed) from yourself";

            public override void Run(string[] args)
            {
                var p = Player.m_localPlayer;
                if (p == null) { DevCommands.Say("no local player"); return; }
                var seman = p.GetSEMan();
                var removed = 0;
                for (var t = 1; t <= 3; t++)
                    if (seman.RemoveStatusEffect(Effects.StatusEffects.NameHash(t), quiet: false)) removed++;
                if (seman.RemoveStatusEffect(Effects.StatusEffects.RevealedHash, quiet: false)) removed++;
                DevCommands.Say(removed > 0 ? $"veil ended ({removed} effect(s) removed)" : "veil ended (no InvisibilityPotion effect was active)");
            }
        }
    }
}
#endif
