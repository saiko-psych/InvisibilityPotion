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

            public override void Run(string[] args)
            {
                var patched = PatchHealth.PatchedTargets(Plugin.HarmonyInstance);
                Console.instance.Print($"{Plugin.PluginName} {Plugin.PluginVersion}");
                Console.instance.Print($"patched: {string.Join(", ", patched)}");
                Console.instance.Print($"missing: {(Plugin.MissingPatches.Count == 0 ? "none" : string.Join(", ", Plugin.MissingPatches))}");
            }
        }
    }
}
#endif
