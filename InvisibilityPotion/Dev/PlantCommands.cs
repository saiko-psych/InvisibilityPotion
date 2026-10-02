#if DEBUG
using System;
using Jotunn.Entities;
using Jotunn.Managers;
using InvisibilityPotion.Goggles;
using UnityEngine;

namespace InvisibilityPotion.Dev
{
    /// <summary>Plan 5 Debug commands (goggles, plants, lichen, vegetation). Output goes to the console and Plugin.Log (DevCommands.Say).</summary>
    internal static class PlantCommands
    {
        public static void Register()
        {
            CommandManager.Instance.AddConsoleCommand(new GogglesCommand());
        }

        private static void Say(string line) => DevCommands.Say(line);

        /// <summary>Extra ip_state lines: goggles level, IP_Goggles on the own ZDO, and the reveal per nearby hidden player.</summary>
        public static void StateLines(Action<string> say)
        {
            var p = Player.m_localPlayer;
            if (p == null) return;
            var zdo = p.m_nview != null && p.m_nview.IsValid() ? p.m_nview.GetZDO() : null;
            say($"goggles: level {GogglesLevel.Local} (helmet {GogglesLevel.FromHelmet}, override {(GogglesLevel.DevOverride.HasValue ? GogglesLevel.DevOverride.Value.ToString() : "off")}), " +
                $"zdo IP_Goggles={GogglesLevel.Read(zdo)}, RevealHiddenPlayers={Config.PluginConfig.RevealHiddenPlayers}, sees hidden players={GogglesLevel.LocalSeesHiddenPlayers}");
            foreach (var other in Player.GetAllPlayers())
            {
                if (other == null || other == p) continue;
                if (!Net.HiddenState.IsHiddenFromPlayers(other)) continue;
                var d = Vector3.Distance(other.transform.position, p.transform.position);
                say($"  hidden player {other.GetPlayerName()} at {d:F0} m (y {other.transform.position.y:F0}): reveal for me={GogglesLevel.LocalSeesHiddenPlayers}");
            }
        }

        private class GogglesCommand : ConsoleCommand
        {
            public override string Name => "ip_goggles";
            public override string Help => "ip_goggles <0|1|2|3>: fake your goggle level (also written to IP_Goggles) | ip_goggles off: read the helmet slot again | ip_goggles: print the level";

            public override void Run(string[] args)
            {
                if (args.Length >= 1)
                {
                    if (args[0].Equals("off", StringComparison.OrdinalIgnoreCase)) GogglesLevel.DevOverride = null;
                    else if (int.TryParse(args[0], out var l) && l >= 0 && l <= GoggleLevel.Max) GogglesLevel.DevOverride = l;
                    else { Say(Help); return; }
                    GogglesLevel.Tick();
                }
                StateLines(Say);
            }
        }
    }
}
#endif
