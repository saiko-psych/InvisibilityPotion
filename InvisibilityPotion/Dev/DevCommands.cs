#if DEBUG
using System.Linq;
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
            CommandManager.Instance.AddConsoleCommand(new FogUiCommand());
            AssetCommands.Register();
            MeshExport.Register();
        }

        internal static void Say(string line)
        {
            Console.instance.Print(line);
            Plugin.Log.LogInfo(line);
        }

        /// <summary>The local player's current invisibility tier (effect first, then ZDO), 0 = none.</summary>
        internal static int CurrentTier()
        {
            var p = Player.m_localPlayer;
            if (p == null) return 0;
            var se = Effects.SE_Invisibility.ActiveOn(p);
            if (se != null) return se.Tier;
            return Net.HiddenState.Get(p).tier;
        }

        /// <summary>Tier from args[index] when given, else the local player's current invisibility tier (0 = none).</summary>
        private static int ExplicitOrCurrentTier(string[] args, int index)
        {
            if (args.Length > index && int.TryParse(args[index], out var t) && t >= 1 && t <= 3) return t;
            return CurrentTier();
        }

        private static void LookChanged(string what)
        {
            VeilController.LookChanged();
            Say($"{what}; re-applied to all veiled players");
        }

        private class VeilCommand : ConsoleCommand
        {
            public override string Name => "ip_veil";
            public override string Help =>
                "ip_veil: print modes | ip_veil <none|cutoff|hide|tint|ghost|distortion|shadow|spirit> [tier]: override the body mode of your current (or the given) tier, none = mode Off | " +
                "ip_veil off [tier|all]: clear the override | ip_veil look distortion <strength 0..5> [alpha 0..1] [wave, -1 = borrowed] [tier] | ip_veil look ghost <alpha 0..1> [emission 0..5] | " +
                "ip_veil look shadow <alpha 0..1> | ip_veil look spirit <strength 0..10> [r g b] | " +
                "ip_veil save: writes ghost/shadow/spirit to [Veil] and every tier's look (incl. distortion) to [Fog.TierN]";

            public override void Run(string[] args)
            {
                if (args.Length < 1) { Print(); return; }
                var verb = args[0].ToLowerInvariant();
                if (verb == "save")
                {
                    FogVeil.SaveGlobalLook();
                    for (var t = 1; t <= 3; t++) Cfg.PluginConfig.SaveFog(t, FogVeil.Fog[t], FogVeil.AlphaMode);
                    Say("veil look saved: [Veil] Ghost/Shadow/Spirit, [Fog.Tier1..3] (distortion and fog)");
                    return;
                }
                if (verb == "look") { Look(args); return; }
                if (verb == "off")
                {
                    if (args.Length >= 2 && args[1].ToLowerInvariant() == "all") { ClearAll(); return; }
                    var t = ExplicitOrCurrentTier(args, 1);
                    if (t == 0) { ClearAll(); return; }
                    FogVeil.ModeOverride[t] = null;
                    LookChanged($"T{t} override cleared; T{t} uses its config mode {FogVeil.ConfiguredMode(t)}");
                    return;
                }
                var modeText = verb == "none" ? "Off" : args[0];
                if (!FogVeil.TryParseMode(modeText, out var mode) || mode == BodyVeilMode.Off && verb != "none") { Say(Help); return; }
                var tier = ExplicitOrCurrentTier(args, 1);
                if (tier == 0) { Say("you are not under an invisibility effect; drink one (ip_give <1|2|3>) or pass a tier: ip_veil <mode> <1|2|3>"); return; }
                FogVeil.ModeOverride[tier] = mode;
                LookChanged($"T{tier} body mode overridden: {mode} (config: {FogVeil.ConfiguredMode(tier)})");
            }

            private void Look(string[] args)
            {
                var target = args.Length >= 2 ? args[1].ToLowerInvariant() : "";
                if (args.Length < 3 || !Fog.FloatList.TryParseOne(args[2], out var first)) { Say(Help); return; }
                var hasSecond = args.Length >= 4 && Fog.FloatList.TryParseOne(args[3], out _);
                Fog.FloatList.TryParseOne(args.Length >= 4 ? args[3] : "", out var second);
                if (target == "distortion")
                {
                    var tier = ExplicitOrCurrentTier(args, 5);
                    if (tier == 0) { Say("no current tier; pass one: ip_veil look distortion <strength> <alpha> <wave> <1|2|3>"); return; }
                    var s = FogVeil.Fog[tier];
                    s.DistortionStrength = Mathf.Clamp(first, 0f, 5f);
                    if (hasSecond) s.DA = Mathf.Clamp01(second);
                    if (args.Length >= 5) s.TrySet("DistortionWave", args[4]);   // malformed: keeps the current wave
                    LookChanged($"T{tier} distortion strength {s.DistortionStrength}, color {s.Get("DistortionColor")}, wave {WaveText(s)}");
                }
                else if (target == "shadow")
                {
                    var c = FogVeil.ShadowColor; c.a = Mathf.Clamp01(first); FogVeil.ShadowColor = c;
                    LookChanged($"shadow color {FogVeil.FormatColor(FogVeil.ShadowColor)}");
                }
                else if (target == "spirit")
                {
                    FogVeil.SpiritStrength = Mathf.Clamp(first, 0f, 10f);
                    if (args.Length >= 6 && Fog.FloatList.TryParseOne(args[3], out var r) && Fog.FloatList.TryParseOne(args[4], out var g) && Fog.FloatList.TryParseOne(args[5], out var b))
                        FogVeil.SpiritColor = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
                    LookChanged($"spirit tint {FogVeil.FormatRgb(FogVeil.SpiritColor)} x{FogVeil.SpiritStrength}");
                }
                else if (target == "ghost")
                {
                    var c = FogVeil.GhostColor; c.a = Mathf.Clamp01(first); FogVeil.GhostColor = c;
                    if (hasSecond) FogVeil.GhostEmission = Mathf.Clamp(second, 0f, 5f);
                    LookChanged($"ghost color {FogVeil.FormatColor(FogVeil.GhostColor)}, emission x{FogVeil.GhostEmission}");
                }
                else Say(Help);
            }

            internal static string WaveText(Fog.FogSettings s) =>
                s.DistortionWave >= 0f ? Fog.FloatList.Format(s.DistortionWave)
                : float.IsNaN(FogVeil.BorrowedDistortionWave) ? "borrowed (not loaded yet)" : $"borrowed {Fog.FloatList.Format(FogVeil.BorrowedDistortionWave)}";

            private static void ClearAll()
            {
                for (var t = 1; t <= 3; t++) FogVeil.ModeOverride[t] = null;
                LookChanged("all body mode overrides cleared");
            }

            private static void Print()
            {
                for (var t = 1; t <= 3; t++)
                {
                    var cfg = Cfg.PluginConfig.Tier(t);
                    var ov = FogVeil.ModeOverride[t];
                    var s = FogVeil.Fog[t];
                    Say($"T{t}: mode {FogVeil.ModeFor(t)} (config {cfg.BodyVeilMode}{(ov.HasValue ? $", override {ov.Value}" : "")}), fog {(s.Enabled ? "on" : "off")}, " +
                        $"distortion strength {Fog.FloatList.Format(s.DistortionStrength)} color {s.Get("DistortionColor")} wave {WaveText(s)}, " +
                        $"fog material {s.FogMaterial}, emitter {s.EmitterMode}");
                }
                Say($"look ghost: color {FogVeil.FormatColor(FogVeil.GhostColor)}, emission x{FogVeil.GhostEmission} (ip_veil look ghost <alpha> [emission])");
                Say($"look shadow: color {FogVeil.FormatColor(FogVeil.ShadowColor)} (ip_veil look shadow <alpha>); spirit: tint {FogVeil.FormatRgb(FogVeil.SpiritColor)} x{FogVeil.SpiritStrength} (ip_veil look spirit <strength> [r g b])");
            }

            public override System.Collections.Generic.List<string> CommandOptionList() =>
                new System.Collections.Generic.List<string> { "none", "cutoff", "hide", "tint", "ghost", "distortion", "shadow", "spirit", "off", "look", "save" };
        }

        private class FogCommand : ConsoleCommand
        {
            public override string Name => "ip_fog";
            public override string Help =>
                "ip_fog [t1|t2|t3] ...: tier defaults to your current one (else 1) | ip_fog [tN]: print | ip_fog [tN] <key> <value>, keys: enabled, rate, size, life, speed, alpha, " +
                "dynamic, emission (0..1), spreadx, spready, spreadz, drift, trail (on|off), outer (on|off), outerradius (m), outeralpha, outerrate (/s per anchor), outersize (m), " +
                "outerspready, outerlife (s), outertrail (on|off), outerflat (on|off: quads parallel to the ground), ground (on|off), groundrate (/s), grounddistance (/m), groundsize (m), groundgrow (x), groundlife (s), " +
                "groundalpha, groundradius (m), groundheight (m), grounddrift (m/s), " +
                "outeranchors (comma list, e.g. Chest,Hips,Head), material (soft|swamp_mist|ghost_smoke|wraith_smoke|slowwispysmoke), " +
                "emitter (bones|mesh), meshoffset, meshrate (0 = rate x anchors), wave (-1 = borrowed) | ip_fog [tN] color r g b | " +
                "ip_fog alphamode <both|material|vertex> | ip_fog [tN] anchor <name> on|off | radius <v> | offset x y z | ip_fog [tN] save | ip_fog reset | " +
                "ip_fog dump: log every fog emitter of your veil (state, counts, material, bounds, positions; without a veil: the stray scan) | " +
                "ip_fog strays: log every veil particle system in the scene (parents, state, particles, age, tracked or not) | ip_fogui: tuning window";

            private static readonly System.Collections.Generic.Dictionary<string, string> Aliases = new System.Collections.Generic.Dictionary<string, string>
            {
                ["life"] = "Lifetime", ["dynamic"] = "DynamicColor", ["outer"] = "OuterEnabled", ["outerradius"] = "OuterRadius",
                ["outeralpha"] = "OuterAlpha", ["outerrate"] = "OuterRate", ["outersize"] = "OuterSize",
                ["material"] = "FogMaterial", ["emitter"] = "FogEmitterMode", ["wave"] = "DistortionWave",
                ["outerlife"] = "OuterLifetime", ["outerspready"] = "OuterSpreadY", ["outertrail"] = "OuterTrail", ["outeranchors"] = "OuterAnchors",
                ["outerflat"] = "OuterHorizontal", ["ground"] = "GroundEnabled", ["grounddistance"] = "GroundRateDistance", ["groundlife"] = "GroundLifetime",
            };

            public override void Run(string[] args)
            {
                var tier = CurrentTier();
                if (args.Length >= 1 && args[0].Length == 2 && (args[0][0] == 't' || args[0][0] == 'T') && args[0][1] >= '1' && args[0][1] <= '3')
                {
                    tier = args[0][1] - '0';
                    args = args.Skip(1).ToArray();
                }
                if (tier == 0) tier = 1;
                var s = FogVeil.Fog[tier];
                if (args.Length < 1) { Print(tier); return; }
                var key = args[0].ToLowerInvariant();
                switch (key)
                {
                    case "save":
                        Cfg.PluginConfig.SaveFog(tier, s, FogVeil.AlphaMode);
                        Say($"T{tier} look saved to [Fog.Tier{tier}]");
                        return;
                    case "reset":
                        Cfg.PluginConfig.Reload();   // raises Changed: VeilController reloads the look and re-applies it
                        Say("look reloaded from the config file");
                        Print(tier);
                        return;
                    case "color":
                        if (args.Length < 4 || !s.TrySet("Color", $"{args[1]},{args[2]},{args[3]}")) { Say(Help); return; }
                        LookChanged($"T{tier} fog color {s.Get("Color")}");
                        return;
                    case "alphamode":
                        if (args.Length < 2 || !Fog.FogSettings.TryParseAlphaMode(args[1], out var mode)) { Say(Help); return; }
                        FogVeil.AlphaMode = mode;
                        LookChanged($"fog alpha mode {mode} (all tiers)");
                        return;
                    case "anchor":
                        Anchor(tier, s, args);
                        return;
                    case "dump":
                        Dump();
                        return;
                    case "strays":
                        Strays();
                        return;
                }
                if (args.Length < 2) { Say(Help); return; }
                var name = Aliases.TryGetValue(key, out var alias) ? alias : FindKey(key);
                if (name == null || name == "Color" || name == "DistortionColor" || name.StartsWith(Fog.FogSettings.AnchorPrefix) || !s.TrySet(name, args[1])) { Say(Help); return; }
                LookChanged($"T{tier} fog {name} {s.Get(name)}");
            }

            /// <summary>ip_fog dump: the local player's fog emitters, line by line, to the console and the log.</summary>
            internal static void Dump()
            {
                var p = Player.m_localPlayer;
                if (p == null) { Say("no local player"); return; }
                var lines = new System.Collections.Generic.List<string>();
                VeilController.DumpFog(p, lines);
                if (lines.Count == 0)
                {
                    Say("fog dump: you have no veil (drink a potion or ip_give <1|2|3>); running the stray scan instead");
                    Strays();
                    return;
                }
                Say($"fog dump at {Time.time:F1} s:");
                foreach (var l in lines) Say(l);
            }

            /// <summary>ip_fog strays: every veil particle system in the scene, to the console and the log.</summary>
            internal static void Strays()
            {
                var lines = new System.Collections.Generic.List<string>();
                VeilController.ScanStrays(lines);
                if (lines.Count == 0) { Say("fog strays: no veil controller"); return; }
                foreach (var l in lines) Say(l);
            }

            private static string FindKey(string key)
            {
                foreach (var k in Fog.FogSettings.Keys)
                    if (string.Equals(k.Name, key, System.StringComparison.OrdinalIgnoreCase)) return k.Name;
                return null;
            }

            private void Anchor(int tier, Fog.FogSettings s, string[] args)
            {
                if (args.Length < 3) { Say(Help); return; }
                var a = s.Anchor(args[1]);
                if (a == null) { Say($"unknown anchor '{args[1]}'; anchors: {string.Join(", ", Fog.FogSettings.AnchorNames)}"); return; }
                var what = args[2].ToLowerInvariant();
                if (Fog.FogAnchor.TryParseSwitch(what, out var on)) a.Enabled = on;
                else if (what == "radius" && args.Length >= 4 && F(args[3], out var radius)) a.Radius = Mathf.Max(0f, radius);
                else if (what == "offset" && args.Length >= 6 && F(args[3], out var x) && F(args[4], out var y) && F(args[5], out var z)) { a.X = x; a.Y = y; a.Z = z; }
                else { Say(Help); return; }
                LookChanged($"T{tier} fog anchor {a.Name} = {a.Format()}");
            }

            private static bool F(string text, out float v) => Fog.FloatList.TryParseOne(text, out v);

            private static void Print(int tier)
            {
                var s = FogVeil.Fog[tier];
                Say($"T{tier} fog {(s.Enabled ? "on" : "off")}: rate {s.Rate}/s per emitter, size {s.Size} m, life {s.Lifetime} s, speed {s.Speed}, alpha {s.Alpha}, " +
                    $"color {s.Get("Color")} (dynamic {s.DynamicColor}, emission {s.Emission}), spread {s.SpreadX}/{s.SpreadY}/{s.SpreadZ}, drift {s.Drift}, alphamode {FogVeil.AlphaMode}, " +
                    $"material {s.FogMaterial}, emitter {s.EmitterMode} (mesh offset {s.MeshOffset}, mesh rate {s.MeshRate}), {s.InnerTrailMode}");
                Say($"  outer {(s.OuterEnabled ? "on" : "off")} on {s.Get("OuterAnchors")}: radius {s.OuterRadius} m, alpha {s.OuterAlpha}, rate {s.OuterRate}/s per anchor, " +
                    $"size {s.OuterSize} m, life {s.OuterLifetime} s, spread y {s.OuterSpreadY}, {s.OuterTrailMode}, {(s.OuterHorizontal ? "flat" : "camera-facing")}; " +
                    $"~{s.LiveParticlesInner:0} live particles per inner emitter{(s.ExceedsParticleBudget ? $" (over {Fog.FogSettings.ParticleWarnThreshold})" : "")}");
                Say($"  ground {(s.GroundEnabled ? "on" : "off")}: rate {s.GroundRate}/s + {s.GroundRateDistance}/m, size {s.GroundSize} m x{s.GroundGrow} grow, life {s.GroundLifetime} s, " +
                    $"alpha {s.GroundAlpha}, radius {s.GroundRadius} m, height {s.GroundHeight} m, drift {s.GroundDrift} m/s; ~{s.LiveParticlesGround:0} live particles while running");
                foreach (var a in s.Anchors) Say($"  anchor {a.Name}: {a.Format()}  (on|off,radius,x,y,z)");
            }

            public override System.Collections.Generic.List<string> CommandOptionList() =>
                new System.Collections.Generic.List<string> { "t1", "t2", "t3", "enabled", "rate", "size", "life", "speed", "alpha", "color", "dynamic", "emission", "spreadx", "spready", "spreadz",
                    "drift", "trail", "outer", "outerradius", "outeralpha", "outerrate", "outersize", "outerspready", "outerlife", "outertrail", "outerflat", "outeranchors",
                    "ground", "groundrate", "grounddistance", "groundsize", "groundgrow", "groundlife", "groundalpha", "groundradius", "groundheight", "grounddrift", "dump", "strays",
                    "material", "emitter", "meshoffset", "meshrate", "wave", "alphamode", "anchor", "save", "reset" };
        }

        private class FogUiCommand : ConsoleCommand
        {
            public override string Name => "ip_fogui";
            public override string Help => "ip_fogui: toggle the veil tuning window (F7 inside it switches the mouse between window and game)";

            public override void Run(string[] args)
            {
                var open = FogTuningWindow.Toggle();
                Say(open ? "fog tuning window open" : "fog tuning window closed");
            }
        }

        private class SpawnCommand : ConsoleCommand
        {
            public override string Name => "ip_spawn";
            public override string Help => "ip_spawn <prefab> [count] [level]: spawn any prefab (creature, item, Plant_*, VeilGoggles_*) on the ground 5 m in front of you, facing you, 1 m apart";

            public override void Run(string[] args)
            {
                var p = Player.m_localPlayer;
                if (p == null || args.Length < 1) { Say(Help); return; }
                var prefab = ZNetScene.instance.GetPrefab(args[0]);
                if (prefab == null) { Say($"unknown prefab {args[0]}"); return; }
                var count = args.Length > 1 && int.TryParse(args[1], out var c) ? c : 1;
                var level = args.Length > 2 && int.TryParse(args[2], out var l) ? l : 1;
                var physics = prefab.GetComponent<Rigidbody>() != null;
                for (var i = 0; i < count; i++)
                {
                    var pos = p.transform.position + p.transform.forward * 5f + p.transform.right * i;
                    // Terrain height only when it is near the player: in a dungeon or a building the terrain lies far below the floor.
                    // Physics objects (a Rigidbody on the root: items, creatures) drop from 0.3 m; plants and props sit exactly on the ground.
                    if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(pos, out var ground) && Mathf.Abs(ground - p.transform.position.y) < 3f)
                        pos.y = ground + (physics ? 0.3f : 0f);
                    var facing = Vector3.ProjectOnPlane(-p.transform.forward, Vector3.up);
                    var go = Object.Instantiate(prefab, pos, facing.sqrMagnitude > 0.001f ? Quaternion.LookRotation(facing) : Quaternion.identity);
                    var ch = go.GetComponent<Character>();
                    if (ch != null && level > 1) ch.SetLevel(level);
                }
                Say($"spawned {count} x {args[0]} (level {level}, {(physics ? "rigidbody: 0.3 m above the ground" : "no rigidbody: on the ground")})");
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
            public override string Help => "ip_give <1|2|3>: apply the invisibility tier effect to yourself | ip_give goggles <1|2|3>: put the veil goggles of that tier into your inventory";

            public override void Run(string[] args)
            {
                var p = Player.m_localPlayer;
                if (p == null) { Say("no local player"); return; }
                if (args.Length >= 2 && args[0].ToLowerInvariant() == "goggles")
                {
                    if (!int.TryParse(args[1], out var gt) || gt < 1 || gt > 3) { Say(Help); return; }
                    var name = Items.GoggleItems.ItemName(gt);
                    var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(name) : null;
                    if (prefab == null) { Say($"{name} is not registered (see the assets: lines in the log)"); return; }
                    Say(p.GetInventory().AddItem(prefab, 1) ? $"added {name}" : $"{name} not added (inventory full?)");
                    return;
                }
                if (args.Length < 1 || !int.TryParse(args[0], out var tier) || tier < 1 || tier > 3) { Say(Help); return; }
                var se = p.GetSEMan().AddStatusEffect(Effects.StatusEffects.NameHash(tier), resetTime: true);
                Say(se != null ? $"applied T{tier}" : $"T{tier} not applied (already active or not registered)");
            }
        }

        private class StateCommand : ConsoleCommand
        {
            public override string Name => "ip_state";
            public override string Help => "InvisibilityPotion: print patch health, plugin state, nearby AI and (while veiled) the fog dump of your veil (see ip_fog dump)";

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
                    var fog = new System.Collections.Generic.List<string>();
                    VeilController.DumpFog(p, fog);
                    foreach (var l in fog) Say(l);
                }
            }
        }
    }
}
#endif
