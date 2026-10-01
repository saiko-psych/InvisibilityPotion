#if DEBUG
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using InvisibilityPotion.Visuals;
using Cfg = InvisibilityPotion.Config;
using Fog = InvisibilityPotion.Visuals;

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
            CommandManager.Instance.AddConsoleCommand(new FogCommand());
        }

        internal static void Say(string line)
        {
            Console.instance.Print(line);
            Plugin.Log.LogInfo(line);
        }

        private class VeilCommand : ConsoleCommand
        {
            public override string Name => "ip_veil";
            public override string Help =>
                "ip_veil: print modes | ip_veil <none|cutoff|hide|tint|ghost|distortion> [tier]: override the body mode of your current tier (none = mode Off) | " +
                "ip_veil off [tier|all]: clear the override | ip_veil distortion <strength> [alpha] | ip_veil ghost <alpha> [emission] | ip_veil save";

            public override void Run(string[] args)
            {
                if (args.Length < 1) { Print(); return; }
                var verb = args[0].ToLowerInvariant();
                if (verb == "save")
                {
                    Cfg.PluginConfig.SaveVeilLook(FogVeil.DistortionStrength, FogVeil.FormatColor(FogVeil.DistortionColor), FogVeil.FormatColor(FogVeil.GhostColor), FogVeil.GhostEmission);
                    Say("veil look saved to [Veil] (DistortionStrength, DistortionColor, GhostColor, GhostEmission)");
                    return;
                }
                if (verb == "distortion" && args.Length >= 2)
                {
                    if (!Fog.FloatList.TryParseOne(args[1], out var strength)) { Say(Help); return; }
                    FogVeil.DistortionStrength = strength;
                    if (args.Length >= 3 && Fog.FloatList.TryParseOne(args[2], out var alpha)) { var c = FogVeil.DistortionColor; c.a = alpha; FogVeil.DistortionColor = c; }
                    Changed($"distortion strength {strength}, color {FogVeil.FormatColor(FogVeil.DistortionColor)}");
                    return;
                }
                if (verb == "ghost" && args.Length >= 2)
                {
                    if (!Fog.FloatList.TryParseOne(args[1], out var alpha)) { Say(Help); return; }
                    var c = FogVeil.GhostColor; c.a = alpha; FogVeil.GhostColor = c;
                    if (args.Length >= 3 && Fog.FloatList.TryParseOne(args[2], out var emission)) FogVeil.GhostEmission = Mathf.Max(0f, emission);
                    Changed($"ghost color {FogVeil.FormatColor(FogVeil.GhostColor)}, emission x{FogVeil.GhostEmission}");
                    return;
                }
                if (verb == "off")
                {
                    if (args.Length >= 2 && args[1].ToLowerInvariant() == "all") { ClearAll(); return; }
                    var t = ExplicitOrCurrentTier(args, 1);
                    if (t == 0) { ClearAll(); return; }
                    FogVeil.ModeOverride[t] = null;
                    Changed($"T{t} override cleared; T{t} uses its config mode {FogVeil.ConfiguredMode(t)}");
                    return;
                }
                var modeText = verb == "none" ? "Off" : args[0];
                if (!FogVeil.TryParseMode(modeText, out var mode) || mode == BodyVeilMode.Off && verb != "none") { Say(Help); return; }
                var tier = ExplicitOrCurrentTier(args, 1);
                if (tier == 0) { Say("you are not under an invisibility effect; drink one (ip_give <1|2|3>) or pass a tier: ip_veil <mode> <1|2|3>"); return; }
                FogVeil.ModeOverride[tier] = mode;
                Changed($"T{tier} body mode overridden: {mode} (config: {FogVeil.ConfiguredMode(tier)})");
            }

            private static void ClearAll()
            {
                for (var t = 1; t <= 3; t++) FogVeil.ModeOverride[t] = null;
                Changed("all body mode overrides cleared");
            }

            /// <summary>Tier from args[index] when given, else the local player's current invisibility tier (0 = none).</summary>
            private static int ExplicitOrCurrentTier(string[] args, int index)
            {
                if (args.Length > index && int.TryParse(args[index], out var t) && t >= 1 && t <= 3) return t;
                var p = Player.m_localPlayer;
                if (p == null) return 0;
                var se = Effects.SE_Invisibility.ActiveOn(p);
                if (se != null) return se.Tier;
                return Net.HiddenState.Get(p).tier;
            }

            private static void Changed(string what)
            {
                FogVeil.Bump();
                VeilController.ForceRefreshAll();
                Say($"{what}; re-applied to all veiled players");
            }

            private static void Print()
            {
                for (var t = 1; t <= 3; t++)
                {
                    var cfg = Cfg.PluginConfig.Tier(t);
                    var ov = FogVeil.ModeOverride[t];
                    Say($"T{t}: mode {FogVeil.ModeFor(t)} (config {cfg.BodyVeilMode}{(ov.HasValue ? $", override {ov.Value}" : "")}), fog {(cfg.FogEnabled ? "on" : "off")} density {cfg.FogDensity}");
                }
                Say($"distortion strength {FogVeil.DistortionStrength}, color {FogVeil.FormatColor(FogVeil.DistortionColor)}; ghost color {FogVeil.FormatColor(FogVeil.GhostColor)}, emission x{FogVeil.GhostEmission}");
            }

            public override System.Collections.Generic.List<string> CommandOptionList() =>
                new System.Collections.Generic.List<string> { "none", "cutoff", "hide", "tint", "ghost", "distortion", "off", "save" };
        }

        private class FogCommand : ConsoleCommand
        {
            public override string Name => "ip_fog";
            public override string Help =>
                "ip_fog: print | ip_fog <rate|size|life|speed|alpha|drift> <v> | ip_fog color r g b | ip_fog alphamode <both|material|vertex> | " +
                "ip_fog anchor <name> on|off | ip_fog anchor <name> radius <v> | ip_fog anchor <name> offset x y z | ip_fog save | ip_fog reset";

            public override void Run(string[] args)
            {
                var s = FogVeil.Fog;
                if (args.Length < 1) { Print(s); return; }
                var key = args[0].ToLowerInvariant();
                switch (key)
                {
                    case "save":
                        Cfg.PluginConfig.SaveFog(s);
                        Say("fog saved to [Fog]");
                        return;
                    case "reset":
                        Cfg.PluginConfig.Reload();   // raises Changed: VeilController reloads the look and re-applies it
                        Say("fog reloaded from the config file");
                        Print(FogVeil.Fog);
                        return;
                    case "color":
                        if (args.Length < 4 || !F(args[1], out var r) || !F(args[2], out var g) || !F(args[3], out var b)) { Say(Help); return; }
                        s.R = r; s.G = g; s.B = b;
                        Changed($"fog color {Fog.FloatList.Format(r, g, b)}");
                        return;
                    case "alphamode":
                        if (args.Length < 2 || !Fog.FogSettings.TryParseAlphaMode(args[1], out var mode)) { Say(Help); return; }
                        s.AlphaMode = mode;
                        Changed($"fog alpha mode {mode}");
                        return;
                    case "anchor":
                        Anchor(s, args);
                        return;
                }
                if (args.Length < 2 || !F(args[1], out var v)) { Say(Help); return; }
                switch (key)
                {
                    case "rate": s.Rate = Mathf.Max(0f, v); break;
                    case "size": s.Size = Mathf.Max(0.01f, v); break;
                    case "life": s.Lifetime = Mathf.Max(0.05f, v); break;
                    case "speed": s.Speed = v; break;
                    case "alpha": s.Alpha = Mathf.Clamp01(v); break;
                    case "drift": s.Drift = v; break;
                    default: Say(Help); return;
                }
                Changed($"fog {key} {v}");
            }

            private void Anchor(Fog.FogSettings s, string[] args)
            {
                if (args.Length < 3) { Say(Help); return; }
                var a = s.Anchor(args[1]);
                if (a == null) { Say($"unknown anchor '{args[1]}'; anchors: {string.Join(", ", Fog.FogSettings.AnchorNames)}"); return; }
                var what = args[2].ToLowerInvariant();
                if (Fog.FogAnchor.TryParseSwitch(what, out var on)) a.Enabled = on;
                else if (what == "radius" && args.Length >= 4 && F(args[3], out var radius)) a.Radius = Mathf.Max(0f, radius);
                else if (what == "offset" && args.Length >= 6 && F(args[3], out var x) && F(args[4], out var y) && F(args[5], out var z)) { a.X = x; a.Y = y; a.Z = z; }
                else { Say(Help); return; }
                Changed($"fog anchor {a.Name} = {a.Format()}");
            }

            private static bool F(string text, out float v) => Fog.FloatList.TryParseOne(text, out v);

            private static void Changed(string what)
            {
                FogVeil.Bump();
                VeilController.ForceRefreshAll();
                Say($"{what}; re-applied to all veiled players");
            }

            private static void Print(Fog.FogSettings s)
            {
                Say($"fog: rate {s.Rate}/s per emitter at density 1, size {s.Size} m, life {s.Lifetime} s, speed {s.Speed}, alpha {s.Alpha} (at density {Fog.FogSettings.ReferenceDensity}), " +
                    $"color {Fog.FloatList.Format(s.R, s.G, s.B)}, drift {s.Drift}, alphamode {s.AlphaMode}");
                foreach (var a in s.Anchors) Say($"  anchor {a.Name}: {a.Format()}  (on|off,radius,x,y,z)");
                for (var t = 1; t <= 3; t++)
                {
                    var cfg = Cfg.PluginConfig.Tier(t);
                    Say($"  T{t}: fog {(cfg.FogEnabled ? "on" : "off")}, density {cfg.FogDensity} -> {s.Rate * cfg.FogDensity:0.##}/s per emitter, alpha {s.EffectiveAlpha(cfg.FogDensity):0.###}");
                }
            }

            public override System.Collections.Generic.List<string> CommandOptionList() =>
                new System.Collections.Generic.List<string> { "rate", "size", "life", "speed", "alpha", "color", "drift", "alphamode", "anchor", "save", "reset" };
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
                    var fromPlayers = Net.HiddenState.IsHiddenFromPlayers(p);
                    Say(inv == null
                        ? $"self: no effect; zdo tier={tier} hidden={hidden} hiddenFromPlayers={fromPlayers}"
                        : $"self: T{inv.Tier} phase={inv.Machine.Phase} elapsed={inv.Machine.Elapsed:F1}s rehide={inv.Machine.RehideTimer:F1}s pending={inv.Machine.PendingReveal}; zdo tier={tier} hidden={hidden} hiddenFromPlayers={fromPlayers}");
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
