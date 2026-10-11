using System;
using System.Linq;
using Jotunn.Entities;
using Jotunn.Managers;
using InvisibilityPotion.Goggles;
using InvisibilityPotion.Plants;
using UnityEngine;

namespace InvisibilityPotion.Commands
{
    /// <summary>
    /// 0.4.1: the two diagnostic commands server admins asked for in Release builds (user 2026-10-11): ip_plants and ip_retrofit.
    /// They print nothing a player could not see with goggles and change nothing except ip_retrofit here/reset, so they are gated
    /// by admin status (Jötunn's SynchronizationManager.PlayerIsAdmin; the host and a local game always pass). Output goes to the
    /// console and Plugin.Log, as every command of this mod does. The Debug-only commands (Dev/PlantCommands.cs) share the helpers.
    /// </summary>
    public static class AdminCommands
    {
        public static void Register()
        {
            CommandManager.Instance.AddConsoleCommand(new PlantsCommand());
            CommandManager.Instance.AddConsoleCommand(new RetrofitCommand());
        }

        internal static void Say(string line)
        {
            Console.instance?.Print(line);
            Plugin.Log.LogInfo(line);
        }

        /// <summary>Local game, the host, or a client Jötunn knows as admin of the server.</summary>
        internal static bool IsAdmin()
        {
            if (ZNet.instance == null || ZNet.instance.IsServer()) return true;
            try { return SynchronizationManager.Instance.PlayerIsAdmin; }
            catch { return false; }
        }

        internal static bool DenyUnlessAdmin(string command)
        {
            if (IsAdmin()) return false;
            Say($"{command}: admins only on a dedicated server");
            return true;
        }

        private static Minimap.PinData _pin;

        /// <summary>Compass direction from <paramref name="from"/> to <paramref name="to"/> (z = north, x = east, as on the Valheim map).</summary>
        internal static string Compass(Vector3 from, Vector3 to)
        {
            var d = to - from;
            if (new Vector2(d.x, d.z).sqrMagnitude < 0.01f) return "here";
            var deg = (Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 360f) % 360f;
            string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            return $"{names[(int)Mathf.Round(deg / 45f) % 8]} ({deg:F0}°)";
        }

        internal static string Prefab(Component c) => c == null ? "?" : (c.GetComponentInParent<ZNetView>()?.gameObject.name ?? c.name).Replace("(Clone)", "");

        internal static string Owner(ZNetView nv)
        {
            if (nv == null || !nv.IsValid()) return "-";
            var o = nv.GetZDO().GetOwner();
            return o == ZDOMan.GetSessionID() ? "local" : o.ToString();
        }

        /// <summary>
        /// First non-trigger collider under the crosshair that does not belong to the local player (RaycastAll sorted by distance;
        /// the camera ray starts behind the player in third person).
        /// </summary>
        internal static bool CrosshairHit(float range, out RaycastHit hit)
        {
            hit = default;
            var cam = GameCamera.instance;
            if (cam == null) return false;
            var me = Player.m_localPlayer;
            var hits = Physics.RaycastAll(cam.transform.position, cam.transform.forward, range, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                if (me != null && h.collider.transform.IsChildOf(me.transform)) continue;
                hit = h;
                return true;
            }
            return false;
        }

        /// <summary>The VeilHarvest under the crosshair (any collider of the plant or its tree), else the nearest within 10 m.</summary>
        internal static VeilHarvest LookedAtOrNearest(Player p)
        {
            if (CrosshairHit(50f, out var hit))
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
            public override string Help => "ip_plants [radius=64]: every hidden plant (lichen, Baldr's Tear, Hel's Ember Fern, cultivated) in range with distance, direction, stage and keys, plus lichen tree counts and saplings | " +
                                           "ip_plants nearest: map pin on the nearest plant (any range) | ip_plants unpin (admins only on a dedicated server)";

            public override void Run(string[] args)
            {
                if (DenyUnlessAdmin(Name)) return;
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
                var misplaced = list.Count(h => h.TryGetPlacement(out var why) && why != PlantPlacement.Reason.None);
                Say($"  misplaced wild plants in range: {misplaced}; removed by this peer since start: {VeilHarvest.RemovedCount} " +
                    $"([Plants] RemoveMisplacedWildPlants {Config.PluginConfig.RemoveMisplacedWildPlants})");
                var trees = TreeLichen.All.Where(t => t != null && Vector3.Distance(t.transform.position, me) <= radius).ToList();
                var withLichen = trees.Count(t => t.Lichen != null);
                Say($"lichen trees within {radius:F0} m: {withLichen} of {trees.Count} eligible trees" +
                    (trees.Count > 0 ? $" ({100.0 * withLichen / trees.Count:F1} %, chance {Config.PluginConfig.LichenTreeChance}); " +
                                       $"biomes {string.Join(", ", trees.GroupBy(t => t.LastBiome).Select(b => $"{b.Key} {b.Count()}"))}" : ""));
                var saplings = UnityEngine.Object.FindObjectsByType<Plant>(FindObjectsSortMode.None)
                    .Where(pl => pl != null && Cultivation.IsOurSapling(pl.name) && Vector3.Distance(pl.transform.position, me) <= radius).ToList();
                foreach (var s in saplings)
                    Say($"  sapling {s.name.Replace("(Clone)", "")} at {Vector3.Distance(s.transform.position, me):F1} m {Compass(me, s.transform.position)}: " +
                        $"status {s.m_status}, grow time {s.m_growTime / 60f:F0} min, owner {Owner(s.m_nview)}");
            }

            private static void Unpin()
            {
                if (_pin != null && Minimap.instance != null) Minimap.instance.RemovePin(_pin);
                _pin = null;
            }
        }

        internal static string Line(VeilHarvest h, Vector3 me)
        {
            var pos = h.transform.position;
            var sight = h.GetComponent<VeilSight>();
            var minutes = HarvestStage.MinutesToNext(h.BaseStage, ZNet.instance != null ? HarvestStage.ElapsedMinutes(ZNet.instance.GetTime().Ticks, h.BaseTicks) : 0, h.StageMinutes, h.MaxStage);
            return $"{Prefab(h)}{(h.OnTree ? " (lichen)" : "")} at {Vector3.Distance(pos, me):F1} m {Compass(me, pos)} ({pos.x:F0}, {pos.y:F0}, {pos.z:F0}): " +
                   $"stage {h.Stage}/{h.MaxStage}{(h.IsRipe ? " ripe" : $" next in {minutes:F0} min")}, base {h.BaseStage} @ {(h.BaseTicks == 0 ? "-" : new DateTime(h.BaseTicks).ToString("HH:mm"))}, " +
                   $"{Placement(h)}needs goggles {h.Tier}, revealed {(sight != null && sight.Revealed)}, scale {h.PlantScale:F2}{(h.VariantIndex >= 0 ? $", variant {"abc"[h.VariantIndex]}" : "")}{(h.OnTree ? "" : $", yaw {h.Yaw:F0}")}, owner {Owner(h.View)}";
        }

        /// <summary>"misplaced (below sea level), " / "cultivated, " / "" for ip_plants.</summary>
        private static string Placement(VeilHarvest h)
        {
            if (h.OnTree || h.View == null || !h.View.IsValid()) return "";
            var cultivated = h.View.GetZDO().GetBool(VeilHarvest.CultivatedHash) ? "cultivated, " : "";
            if (!h.TryGetPlacement(out var why)) return cultivated;
            return why == PlantPlacement.Reason.None ? cultivated : $"misplaced ({PlantPlacement.Describe(why)}), ";
        }

        /// <summary>ip_retrofit: state and counters of the zone retrofit; "here" runs it for the player's zone now (ignores the checked list); "reset" clears the checked list and counters.</summary>
        private class RetrofitCommand : ConsoleCommand
        {
            public override string Name => "ip_retrofit";
            public override string Help => "ip_retrofit [here|reset]: wild plants for zones generated before the mod. No argument: state and counters; here = run for the current zone now (server/host only); " +
                                           "reset = forget which zones were checked this session (admins only on a dedicated server)";

            public override void Run(string[] args)
            {
                if (DenyUnlessAdmin(Name)) return;
                var arg = args.Length > 0 ? args[0].ToLowerInvariant() : "";
                if (arg == "reset") { ZoneRetrofit.Reset(); Say("retrofit: checked list and counters cleared"); return; }
                if (arg == "here")
                {
                    var p = Player.m_localPlayer;
                    if (p == null) { Say("no local player"); return; }
                    Say(ZoneRetrofit.RunNow(ZoneSystem.GetZone(p.transform.position)));
                    return;
                }
                foreach (var l in ZoneRetrofit.Describe()) Say(l);
            }
        }
    }
}
