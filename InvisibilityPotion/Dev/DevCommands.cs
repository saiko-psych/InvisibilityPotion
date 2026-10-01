#if DEBUG
using Jotunn.Entities;
using Jotunn.Managers;

namespace InvisibilityPotion.Dev
{
    /// <summary>Debug-only console commands. Open the console with F5 (game started with -console).</summary>
    internal static class DevCommands
    {
        public static void Register()
        {
            CommandManager.Instance.AddConsoleCommand(new StateCommand());
        }

        private class StateCommand : ConsoleCommand
        {
            public override string Name => "ip_state";
            public override string Help => "InvisibilityPotion: print patch health and plugin state";

            private static void Say(string line)
            {
                Console.instance.Print(line);
                Plugin.Log.LogInfo(line);
            }

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
