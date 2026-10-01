#if DEBUG
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
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
            CommandManager.Instance.AddConsoleCommand(new GiveCommand());
            CommandManager.Instance.AddConsoleCommand(new PrefabsCommand());
            CommandManager.Instance.AddConsoleCommand(new SpawnCommand());
            CommandManager.Instance.AddConsoleCommand(new VeilCommand());
        }

        internal static void Say(string line)
        {
            Console.instance.Print(line);
            Plugin.Log.LogInfo(line);
        }

        private class VeilCommand : ConsoleCommand
        {
            public override string Name => "ip_veil";
            public override string Help => "ip_veil [off|cutoff|hide|tint|ghost|distortion]: switch the body look of veiled players (in memory); no argument prints the current mode";

            public override void Run(string[] args)
            {
                if (args.Length < 1) { Say($"veil body mode: {Visuals.FogVeil.CurrentMode}"); return; }
                if (!Visuals.FogVeil.TryParseMode(args[0], out var mode)) { Say(Help); return; }
                Visuals.FogVeil.CurrentMode = mode;
                Visuals.VeilController.ForceRefreshAll();
                Say($"veil body mode set to {mode}; re-applied to all veiled players");
            }

            public override System.Collections.Generic.List<string> CommandOptionList() =>
                new System.Collections.Generic.List<string> { "off", "cutoff", "hide", "tint", "ghost", "distortion" };
        }

        private class SpawnCommand : ConsoleCommand
        {
            public override string Name => "ip_spawn";
            public override string Help => "ip_spawn <prefab> [count] [level]: spawn creatures 5 m in front of you";

            public override void Run(string[] args)
            {
                var p = Player.m_localPlayer;
                if (p == null || args.Length < 1) { Say(Help); return; }
                var prefab = ZNetScene.instance.GetPrefab(args[0]);
                if (prefab == null) { Say($"unknown prefab {args[0]}"); return; }
                var count = args.Length > 1 && int.TryParse(args[1], out var c) ? c : 1;
                var level = args.Length > 2 && int.TryParse(args[2], out var l) ? l : 1;
                for (var i = 0; i < count; i++)
                {
                    var pos = p.transform.position + p.transform.forward * 5f + Vector3.right * i;
                    var go = Object.Instantiate(prefab, pos, Quaternion.identity);
                    var ch = go.GetComponent<Character>();
                    if (ch != null && level > 1) ch.SetLevel(level);
                }
                Say($"spawned {count} x {args[0]} (level {level})");
            }

            public override System.Collections.Generic.List<string> CommandOptionList() => ZNetScene.instance?.GetPrefabNames() ?? new System.Collections.Generic.List<string>();
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

        private class PrefabsCommand : ConsoleCommand
        {
            public override string Name => "ip_prefabs";
            public override string Help => "ip_prefabs <substring>: list registered prefab names containing the substring (max 40)";

            public override void Run(string[] args)
            {
                if (args.Length < 1) { Say(Help); return; }
                if (ZNetScene.instance == null) { Say("no scene"); return; }
                var n = 0;
                foreach (var go in ZNetScene.instance.m_prefabs)
                {
                    if (go == null || go.name.IndexOf(args[0], System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (n++ >= 40) { Say("... (capped at 40)"); break; }
                    Say(go.name);
                }
                if (n == 0) Say("no match");
            }
        }

        private class GiveCommand : ConsoleCommand
        {
            public override string Name => "ip_give";
            public override string Help => "ip_give <1|2|3>: apply the invisibility tier effect to yourself";

            public override void Run(string[] args)
            {
                var p = Player.m_localPlayer;
                if (p == null) { Say("no local player"); return; }
                if (args.Length < 1 || !int.TryParse(args[0], out var tier) || tier < 1 || tier > 3) { Say(Help); return; }
                var se = p.GetSEMan().AddStatusEffect(Effects.StatusEffects.NameHash(tier), resetTime: true);
                Say(se != null ? $"applied T{tier}" : $"T{tier} not applied (already active or not registered)");
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
                var p = Player.m_localPlayer;
                if (p != null)
                {
                    var inv = Effects.SE_Invisibility.ActiveOn(p);
                    var (tier, hidden) = Net.HiddenState.Get(p);
                    Say(inv == null
                        ? $"self: no effect; zdo tier={tier} hidden={hidden}"
                        : $"self: T{inv.Tier} phase={inv.Machine.Phase} elapsed={inv.Machine.Elapsed:F1}s rehide={inv.Machine.RehideTimer:F1}s pending={inv.Machine.PendingReveal}; zdo tier={tier} hidden={hidden}");
                    var mine = ZDOMan.GetSessionID();
                    foreach (var c in Character.GetAllCharacters())
                    {
                        if (c == null || c.IsPlayer()) continue;
                        if (Vector3.Distance(c.transform.position, p.transform.position) > 30f) continue;
                        var ai = c.GetComponent<MonsterAI>();
                        if (ai == null) continue;
                        var owner = c.m_nview?.GetZDO()?.GetOwner() ?? 0;
                        var target = ai.GetTargetCreature();
                        Say($"  {c.name.Replace("(Clone)", "")}: target={(target == null ? "-" : target.GetHoverName())} unsensed={ai.m_timeSinceSensedTargetCreature:F1}s alerted={ai.IsAlerted()} owner={(owner == mine ? "local" : owner.ToString())}");
                    }
                }
            }
        }
    }
}
#endif
