#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Entities;
using Jotunn.Managers;
using InvisibilityPotion.Goggles;
using InvisibilityPotion.Plants;
using UnityEngine;

namespace InvisibilityPotion.Dev
{
    /// <summary>Plan 5 Debug commands (goggles, grow, lichen, vegetation). ip_plants and ip_retrofit live in Commands/AdminCommands.cs since 0.4.1 (Release too). Output goes to the console and Plugin.Log (DevCommands.Say).</summary>
    internal static class PlantCommands
    {
        public static void Register()
        {
            CommandManager.Instance.AddConsoleCommand(new GogglesCommand());
            CommandManager.Instance.AddConsoleCommand(new GrowCommand());
            CommandManager.Instance.AddConsoleCommand(new LichenCommand());
            CommandManager.Instance.AddConsoleCommand(new VegCommand());
        }

        private class GrowCommand : ConsoleCommand
        {
            public override string Name => "ip_grow";
            public override string Help => "ip_grow [stage]: looked-at (or nearest within 10 m) hidden plant: set its stage (default: next; lichen 1..3, ground plants 1 = picked, 2 = ripe) | " +
                                           "on a sapling of this mod: let it grow up on its next update (within 10 s)";

            public override void Run(string[] args)
            {
                var p = Player.m_localPlayer;
                if (p == null) { Say("no local player"); return; }
                var sapling = LookedAtSapling(p);
                if (sapling != null)
                {
                    var nv = sapling.m_nview;
                    if (nv == null || !nv.IsValid()) { Say("sapling has no valid view"); return; }
                    if (!nv.IsOwner()) nv.ClaimOwnership();
                    var back = TimeSpan.FromSeconds(sapling.m_growTimeMax + 60f).Ticks;
                    nv.GetZDO().Set(ZDOVars.s_plantTime, ZNet.instance.GetTime().Ticks - back);
                    Say($"sapling plantTime moved back {sapling.m_growTimeMax / 60f + 1f:F0} min; it grows on its next update (status {sapling.m_status})");
                    return;
                }
                var h = LookedAtOrNearest(p);
                if (h == null) { Say("no hidden plant under the crosshair or within 10 m"); return; }
                var stage = args.Length >= 1 && int.TryParse(args[0], out var s) ? s : h.Stage >= h.MaxStage ? 1 : h.Stage + 1;
                h.DevSetStage(stage);
                Say($"{Prefab(h)}: stage set to {h.Stage}/{h.MaxStage} (base {h.BaseStage}, time now)");
            }

            private static Plant LookedAtSapling(Player p)
            {
                if (CrosshairHit(10f, out var hit))
                {
                    var plant = hit.collider.GetComponentInParent<Plant>();
                    if (plant != null && Cultivation.IsOurSapling(plant.name)) return plant;
                }
                return null;
            }
        }

        private class LichenCommand : ConsoleCommand
        {
            public override string Name => "ip_lichen";
            public override string Help => "ip_lichen <force|off|clear|roll>: on the looked-at eligible tree: force lichen on, force it off, clear the override (roll again), or print the roll";

            public override void Run(string[] args)
            {
                if (args.Length < 1) { Say(Help); return; }
                if (!CrosshairHit(50f, out var hit))
                { Say("look at a tree (nothing under the crosshair within 50 m)"); return; }
                var tree = hit.collider.GetComponentInParent<TreeLichen>();
                if (tree == null)
                {
                    var any = hit.collider.GetComponentInParent<ZNetView>();
                    Say($"{(any != null ? any.name.Replace("(Clone)", "") : hit.collider.name)} is not an eligible tree ({string.Join(", ", TreeLichen.EligibleTrees)})");
                    return;
                }
                switch (args[0].ToLowerInvariant())
                {
                    case "force": tree.DevForce(1); break;
                    case "off": tree.DevForce(-1); break;
                    case "clear": tree.DevForce(0); break;
                    case "roll": break;
                    default: Say(Help); return;
                }
                var wg = WorldGenerator.instance;
                var pos = tree.transform.position;
                Say($"{tree.PrefabName} at ({pos.x:F1}, {pos.z:F1}): seed {(wg != null ? wg.GetSeed().ToString() : "-")}, biome {tree.LastBiome}, " +
                    $"roll {(double.IsNaN(tree.LastRoll) ? "-" : tree.LastRoll.ToString("F4"))} vs chance {Config.PluginConfig.LichenTreeChance}, " +
                    $"force {tree.GetComponent<ZNetView>()?.GetZDO()?.GetInt(TreeLichen.ForceHash, 0)}, decision: {tree.Decision}, lichen {(tree.Lichen != null ? "yes" : "no")}");
            }
        }

        private class VegCommand : ConsoleCommand
        {
            public override string Name => "ip_veg";
            public override string Help => "ip_veg: the registered wild vegetation of the hidden plants (biome, chance per zone, group, scale) as ZoneSystem holds it";

            public override void Run(string[] args)
            {
                var lines = PlantVegetation.Describe().ToList();
                if (lines.Count == 0) Say("no vegetation registered (bundle missing or zone chances 0)");
                foreach (var l in lines) Say(l);
            }
        }

        private static void Say(string line) => DevCommands.Say(line);
        private static bool CrosshairHit(float range, out RaycastHit hit) => Commands.AdminCommands.CrosshairHit(range, out hit);
        private static string Prefab(Component c) => Commands.AdminCommands.Prefab(c);
        private static VeilHarvest LookedAtOrNearest(Player p) => Commands.AdminCommands.LookedAtOrNearest(p);

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
