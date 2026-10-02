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
    /// <summary>Plan 5 Debug commands (goggles, plants, lichen, vegetation). Output goes to the console and Plugin.Log (DevCommands.Say).</summary>
    internal static class PlantCommands
    {
        public static void Register()
        {
            CommandManager.Instance.AddConsoleCommand(new GogglesCommand());
            CommandManager.Instance.AddConsoleCommand(new PlantsCommand());
            CommandManager.Instance.AddConsoleCommand(new GrowCommand());
            CommandManager.Instance.AddConsoleCommand(new LichenCommand());
            CommandManager.Instance.AddConsoleCommand(new VegCommand());
        }

        private static Minimap.PinData _pin;

        /// <summary>Compass direction from <paramref name="from"/> to <paramref name="to"/> (z = north, x = east, as on the Valheim map).</summary>
        private static string Compass(Vector3 from, Vector3 to)
        {
            var d = to - from;
            if (new Vector2(d.x, d.z).sqrMagnitude < 0.01f) return "here";
            var deg = (Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 360f) % 360f;
            string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            return $"{names[(int)Mathf.Round(deg / 45f) % 8]} ({deg:F0}°)";
        }

        private static string Prefab(Component c) => c == null ? "?" : (c.GetComponentInParent<ZNetView>()?.gameObject.name ?? c.name).Replace("(Clone)", "");

        private static string Owner(ZNetView nv)
        {
            if (nv == null || !nv.IsValid()) return "-";
            var o = nv.GetZDO().GetOwner();
            return o == ZDOMan.GetSessionID() ? "local" : o.ToString();
        }

        /// <summary>The VeilHarvest under the crosshair (any collider of the plant or its tree), else the nearest within 10 m.</summary>
        private static VeilHarvest LookedAtOrNearest(Player p)
        {
            var cam = GameCamera.instance;
            if (cam != null && Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 50f, ~0, QueryTriggerInteraction.Collide))
            {
                var h = hit.collider.GetComponentInParent<VeilHarvest>();
                if (h == null) h = hit.collider.GetComponentInParent<TreeLichen>()?.Lichen?.GetComponent<VeilHarvest>();
                if (h != null) return h;
            }
            return VeilHarvest.All.Where(h => h != null).OrderBy(h => Vector3.Distance(h.transform.position, p.transform.position))
                .FirstOrDefault(h => Vector3.Distance(h.transform.position, p.transform.position) <= 10f);
        }

        private class PlantsCommand : ConsoleCommand
        {
            public override string Name => "ip_plants";
            public override string Help => "ip_plants [radius=64]: every hidden plant (lichen, Baldr's Tear, Hel's Ember Fern, cultivated) in range with distance, direction, stage and keys, plus lichen tree counts | " +
                                           "ip_plants nearest: map pin on the nearest plant (any range) | ip_plants unpin";

            public override void Run(string[] args)
            {
                var p = Player.m_localPlayer;
                if (p == null) { Say("no local player"); return; }
                var me = p.transform.position;
                if (args.Length >= 1 && args[0].Equals("unpin", StringComparison.OrdinalIgnoreCase)) { Unpin(); Say("pin removed"); return; }
                if (args.Length >= 1 && args[0].Equals("nearest", StringComparison.OrdinalIgnoreCase))
                {
                    var n = VeilHarvest.All.Where(h => h != null).OrderBy(h => Vector3.Distance(h.transform.position, me)).FirstOrDefault();
                    if (n == null) { Say("no hidden plant in the loaded zones"); return; }
                    Unpin();
                    var pos = n.transform.position;
                    if (Minimap.instance != null) _pin = Minimap.instance.AddPin(pos, Minimap.PinType.Icon3, $"IP {Prefab(n)}", false, false);
                    Say($"nearest: {Line(n, me)}; map pin {(_pin != null ? "set (Icon3, not saved)" : "not set (no minimap)")}");
                    return;
                }
                var radius = args.Length >= 1 && float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 64f;
                var list = VeilHarvest.All.Where(h => h != null && Vector3.Distance(h.transform.position, me) <= radius)
                    .OrderBy(h => Vector3.Distance(h.transform.position, me)).ToList();
                Say($"hidden plants within {radius:F0} m: {list.Count} (loaded: {VeilHarvest.All.Count}); goggles level {GogglesLevel.Local}");
                foreach (var h in list.Take(40)) Say("  " + Line(h, me));
                if (list.Count > 40) Say("  ... (capped at 40)");
                foreach (var g in list.GroupBy(Prefab)) Say($"  {g.Key}: {g.Count()}");
                var trees = TreeLichen.All.Where(t => t != null && Vector3.Distance(t.transform.position, me) <= radius).ToList();
                var withLichen = trees.Count(t => t.Lichen != null);
                Say($"lichen trees within {radius:F0} m: {withLichen} of {trees.Count} eligible trees" +
                    (trees.Count > 0 ? $" ({100.0 * withLichen / trees.Count:F1} %, chance {Config.PluginConfig.LichenTreeChance}); " +
                                       $"biomes {string.Join(", ", trees.GroupBy(t => t.LastBiome).Select(b => $"{b.Key} {b.Count()}"))}" : ""));
                var saplings = UnityEngine.Object.FindObjectsByType<Plant>(FindObjectsSortMode.None)
                    .Where(pl => pl != null && pl.name.StartsWith(Cultivation.SaplingName, StringComparison.Ordinal) && Vector3.Distance(pl.transform.position, me) <= radius).ToList();
                foreach (var s in saplings)
                    Say($"  sapling at {Vector3.Distance(s.transform.position, me):F1} m {Compass(me, s.transform.position)}: status {s.m_status}, owner {Owner(s.m_nview)}");
            }

            private static void Unpin()
            {
                if (_pin != null && Minimap.instance != null) Minimap.instance.RemovePin(_pin);
                _pin = null;
            }
        }

        private static string Line(VeilHarvest h, Vector3 me)
        {
            var pos = h.transform.position;
            var sight = h.GetComponent<VeilSight>();
            var minutes = HarvestStage.MinutesToNext(h.BaseStage, ZNet.instance != null ? HarvestStage.ElapsedMinutes(ZNet.instance.GetTime().Ticks, h.BaseTicks) : 0, h.StageMinutes, h.MaxStage);
            return $"{Prefab(h)}{(h.OnTree ? " (lichen)" : "")} at {Vector3.Distance(pos, me):F1} m {Compass(me, pos)} ({pos.x:F0}, {pos.y:F0}, {pos.z:F0}): " +
                   $"stage {h.Stage}/{h.MaxStage}{(h.IsRipe ? " ripe" : $" next in {minutes:F0} min")}, base {h.BaseStage} @ {(h.BaseTicks == 0 ? "-" : new DateTime(h.BaseTicks).ToString("HH:mm"))}, " +
                   $"needs goggles {h.Tier}, revealed {(sight != null && sight.Revealed)}, scale {h.PlantScale:F2}{(h.VariantIndex >= 0 ? $", variant {"abc"[h.VariantIndex]}" : "")}, owner {Owner(h.View)}";
        }

        private class GrowCommand : ConsoleCommand
        {
            public override string Name => "ip_grow";
            public override string Help => "ip_grow [stage]: looked-at (or nearest within 10 m) hidden plant: set its stage (default: next; lichen 1..3, ground plants 1 = picked, 2 = ripe) | " +
                                           "on a Huldra sapling: let it grow up on its next update (within 10 s)";

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
                var cam = GameCamera.instance;
                if (cam != null && Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 10f, ~0, QueryTriggerInteraction.Collide))
                {
                    var plant = hit.collider.GetComponentInParent<Plant>();
                    if (plant != null && plant.name.StartsWith(Cultivation.SaplingName, StringComparison.Ordinal)) return plant;
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
                var cam = GameCamera.instance;
                if (cam == null || !Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 50f, ~0, QueryTriggerInteraction.Collide))
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
