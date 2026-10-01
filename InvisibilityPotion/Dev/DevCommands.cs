#if DEBUG
using Jotunn.Entities;
using Jotunn.Managers;
using Cfg = InvisibilityPotion.Config;

namespace InvisibilityPotion.Dev
{
    /// <summary>Debug-only console commands. Open the console with F5 (game started with -console).</summary>
    internal static class DevCommands
    {
        public static void Register()
        {
            CommandManager.Instance.AddConsoleCommand(new StateCommand());
            CommandManager.Instance.AddConsoleCommand(new ReloadConfigCommand());
        }

        internal static void Say(string line)
        {
            Console.instance.Print(line);
            Plugin.Log.LogInfo(line);
        }

        private class ReloadConfigCommand : ConsoleCommand
        {
            public override string Name => "ip_reload_config";
            public override string Help => "InvisibilityPotion: re-read the local config file; Duration applies to new effects only. On a client the values are replaced again at the next server sync";

            public override void Run(string[] args)
            {
                try
                {
                    Cfg.PluginConfig.Reload();
                    var t2 = Cfg.PluginConfig.Tier(2);
                    Say($"config reloaded: T1 duration {Cfg.PluginConfig.Tier(1).Duration}s, T2 rehide {t2.RehideDelay}s, T3 aggro-loss {Cfg.PluginConfig.Tier(3).AggroLossTime}s");
                }
                catch (System.Exception e)
                {
                    Say($"config reload failed: {e.Message}");
                }
            }
        }

        private class StateCommand : ConsoleCommand
        {
            public override string Name => "ip_state";
            public override string Help => "InvisibilityPotion: print patch health and plugin state";

            public override void Run(string[] args)
            {
                var patched = PatchHealth.PatchedTargets(Plugin.HarmonyInstance);
                Say($"{Plugin.PluginName} {Plugin.PluginVersion}");
                Say($"patched: {string.Join(", ", patched)}");
                Say($"missing: {(Plugin.MissingPatches.Count == 0 ? "none" : string.Join(", ", Plugin.MissingPatches))}");
            }
        }
    }
}
#endif
