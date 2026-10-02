using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using InvisibilityPotion.Visuals;

namespace InvisibilityPotion.Config
{
    /// <summary>Binds every config entry (server-synced via Jötunn, admin only) and exposes plain TierConfig/GlobalConfig snapshots.</summary>
    public static partial class PluginConfig
    {
        private static ConfigFile _file;
        private static readonly Dictionary<int, Dictionary<string, ConfigEntryBase>> _tierEntries = new Dictionary<int, Dictionary<string, ConfigEntryBase>>();
        private static readonly Dictionary<string, ConfigEntryBase> _globalEntries = new Dictionary<string, ConfigEntryBase>();
        // Look-only entries ([Fog], [Fog.TierN], [Veil]): local per client, not server-synced, so each player can tune what they see.
        private static readonly Dictionary<string, ConfigEntryBase> _lookEntries = new Dictionary<string, ConfigEntryBase>();
        private static readonly Dictionary<int, Dictionary<string, ConfigEntryBase>> _fogEntries = new Dictionary<int, Dictionary<string, ConfigEntryBase>>();
        private const string FogSection = "Fog";
        private const string VeilSection = "Veil";
        private static readonly TierConfig[] _tiers = new TierConfig[4];
        private static readonly FogSettings[] _fog = { null, FogSettings.Defaults(1), FogSettings.Defaults(2), FogSettings.Defaults(3) };

        public static GlobalConfig Global { get; private set; } = new GlobalConfig();
        /// <summary>Global fog alpha split ([Fog] FogAlphaMode).</summary>
        public static FogAlphaMode FogAlphaMode { get; private set; } = FogAlphaMode.Both;
        public static event Action Changed;

        /// <summary>[Veil] PotionVfxSource: vanilla mead prefab whose start/stop effects the potions use (read at item registration).</summary>
        public static string PotionVfxSource => _lookEntries.TryGetValue("PotionVfxSource", out var e) ? ((ConfigEntry<string>)e).Value : Items.PotionVfx.DefaultSource;

        /// <summary>The tier's fog/look settings from [Fog.TierN]. A fresh object per Refresh; FogVeil clones it for live tuning.</summary>
        public static FogSettings Fog(int tier)
        {
            if (tier < 1 || tier > 3) throw new ArgumentOutOfRangeException(nameof(tier));
            return _fog[tier];
        }

        public static TierConfig Tier(int tier)
        {
            if (tier < 1 || tier > 3) throw new ArgumentOutOfRangeException(nameof(tier));
            return _tiers[tier];
        }

        public static void Bind(ConfigFile file)
        {
            _file = file;
            BindTier(1, 60f, 0.25f, 0.25f, false, false, 5f, 0f, MeadRecipes.NewDefault(1), LookDefaults.BodyVeilModes[1]);
            BindTier(2, 120f, 1f, 1f, true, false, 1f, 12f, MeadRecipes.NewDefault(2), LookDefaults.BodyVeilModes[2]);
            BindTier(3, 180f, 1f, 1f, true, true, 1f, 8f, MeadRecipes.NewDefault(3), LookDefaults.BodyVeilModes[3]);
            BindGlobal("RevealOnDamage", true, "Taking damage reveals a hidden player");
            BindGlobal("RevealOnBlock", true, "A blocked hit or parry reveals a hidden player");
            BindGlobal("RevealOnBowDraw", true, "Drawing a bow reveals a hidden player");
            BindGlobal("ShowSelfFaintly", true, "The hidden player still sees a faint version of themselves");
            BindGlobal("FogCutoffLight", 0.5f, "Alpha cutoff applied to the player's materials for the light veil (tier I)");
            BindGlobal("FogCutoffDense", 0.8f, "Alpha cutoff for the dense veil (tier II/III)");
            BindGlobal("FogCutoffSelf", 0.3f, "Alpha cutoff the hidden player sees on themselves");
            BindGlobal("AllowPvpInvisibility", true, "Server switch for hiding players from other players (tier III)");
            BindLook(FogSection, "FogAlphaMode", FogAlphaMode.Both.ToString(), "Where the fog alpha goes (all tiers): Both (split between material and particle colour), Material or Vertex");
            for (var t = 1; t <= 3; t++) BindFogTier(t);
            BindLook(VeilSection, "GhostColor", "0.75,0.8,0.9,1", "Colour of the Ghost body mode as r,g,b,a (_Color; the shader is alpha-tested, low alpha may cut the body away)");
            BindLook(VeilSection, "GhostEmission", 0.25f, "Multiplier on the Ghost material's glow (_EmissionColor); 1 = vanilla ghost glow");
            BindLook(VeilSection, "ShadowColor", "0,0,0,0.12", "Colour of the Shadow body mode as r,g,b,a (_Color of a copy of the vanilla ShadowPerson material, transparent; alpha = opacity)");
            BindLook(VeilSection, "SpiritColor", "0.6,0.7,0.8", "Tint of the Spirit body mode as r,g,b (Custom/Fallen Warrior _TintColor, multiplied by SpiritStrength)");
            BindLook(VeilSection, "SpiritStrength", 0.25f, "Intensity of the Spirit body mode (HDR multiplier on SpiritColor); 0 = invisible, vanilla Fallen Warrior is about 6");
            BindLook(VeilSection, "PotionVfxSource", Items.PotionVfx.DefaultSource,
                     "Vanilla mead whose drink and expire effects (sound, particle burst) the potions borrow, e.g. MeadFrostResist (bluish-white), MeadTasty, MeadHealthMinor (red). Read once at startup; falls back to MeadFrostResist, MeadTasty, MeadHealthMinor");
#if DEBUG
            Dev.FogTuningWindow.BindConfig(file);   // before MigrateAndDropOrphans, or the stored window values would be dropped as orphans
#endif
            BindPlants();   // plan 5: [Plants], [Goggles] (Config/PluginConfig.Plants.cs)
            MigrateLookDefaults();
            MigrateMeadRecipes();
            MigrateAndDropOrphans();
            Refresh();
        }

        private static void BindTier(int tier, float duration, float stealth, float noise, bool ignored, bool hiddenFromPlayers, float aggroLoss, float rehide, string recipe,
                                     string bodyMode)
        {
            var section = $"Tier{tier}";
            var e = new Dictionary<string, ConfigEntryBase>
            {
                ["Duration"] = Bind(section, "Duration", duration, "Effect duration in seconds"),
                ["StealthModifier"] = Bind(section, "StealthModifier", stealth, "Fraction of vanilla visibility while hidden (1 = vanilla, 0.25 = a quarter). Tier I only."),
                ["NoiseModifier"] = Bind(section, "NoiseModifier", noise, "Fraction of vanilla noise while hidden (1 = vanilla). Tier I only."),
                ["IgnoredByEnemies"] = Bind(section, "IgnoredByEnemies", ignored, "Enemies cannot see or hear the player at all"),
                ["HiddenFromPlayers"] = Bind(section, "HiddenFromPlayers", hiddenFromPlayers, "Other players cannot see this player's position, model or nameplate"),
                ["AggroLossTime"] = Bind(section, "AggroLossTime", aggroLoss, "Seconds a chasing enemy keeps searching before it gives up. Values above 30 have no effect (vanilla's own limit); lower values make enemies give up sooner"),
                ["RehideDelay"] = Bind(section, "RehideDelay", rehide, "Seconds without attacking until hidden again; 0 = an attack ends the effect"),
                ["DebuffStaminaRegenMultiplier"] = Bind(section, "DebuffStaminaRegenMultiplier", 0.5f, "Stamina regeneration multiplier after revealing"),
                ["DebuffDuration"] = Bind(section, "DebuffDuration", 20f, "Debuff duration in seconds, restarted on every reveal"),
                ["Cooldown"] = Bind(section, "Cooldown", 0f, "Reserved; not used in plan 2"),
                ["Recipe"] = Bind(section, "Recipe", recipe, "Mead base recipe at the cauldron: Item:Amount,Item:Amount. Read once at startup from the local file; applies locally and is not server-controlled yet"),
                ["BodyVeilMode"] = Bind(section, "BodyVeilMode", bodyMode,
                                        $"How the hidden player's body is drawn. Exactly one of (case-sensitive, an unknown value falls back to the default): Off, Cutoff, Hide, Tint, Ghost, Distortion, Shadow, Spirit. Fog and distortion look: [Fog.Tier{tier}]. The Debug command ip_veil overrides it in memory",
                                        ModeValues(bodyMode)),
            };
            _tierEntries[tier] = e;
        }

        /// <summary>Binds every key of [Fog.TierN] with the tier's defaults; floats and bools as typed entries, colours and anchors as text.</summary>
        private static void BindFogTier(int tier)
        {
            var section = $"Fog.Tier{tier}";
            var defaults = FogSettings.Defaults(tier);
            var e = new Dictionary<string, ConfigEntryBase>();
            foreach (var key in FogSettings.Keys)
            {
                var text = defaults.Get(key.Name);
                switch (key.Kind)
                {
                    case FogValueKind.Float:
                        FloatList.TryParseOne(text, out var f);
                        e[key.Name] = _file.Bind(section, key.Name, f, key.Description);
                        break;
                    case FogValueKind.Bool:
                        FogAnchor.TryParseSwitch(text, out var b);
                        e[key.Name] = _file.Bind(section, key.Name, b, key.Description);
                        break;
                    default:
                        e[key.Name] = _file.Bind(section, key.Name, text, key.Description);
                        break;
                }
            }
            _fogEntries[tier] = e;
        }

        /// <summary>
        /// Drops every orphaned key (left over from older layouts: [Fog] FogRate/Anchor.*, [TierN] FogEnabled/FogDensity,
        /// [Veil] Distortion*, [General] FogRate*/BodyVeilMode) so the file only holds bound keys. A file from before the
        /// per-tier fog (it still has [Tier2] FogDensity) gets the new tier II default body mode when it kept the old default Ghost.
        /// </summary>
        private static void MigrateAndDropOrphans()
        {
            var orphans = Orphans();
            if (orphans == null || orphans.Count == 0) return;
            var t2Mode = (ConfigEntry<string>)_tierEntries[2]["BodyVeilMode"];
            if (orphans.ContainsKey(new ConfigDefinition("Tier2", "FogDensity")) && t2Mode.Value == "Ghost")
            {
                t2Mode.Value = "Distortion";
                Plugin.Log?.LogInfo("Config migration: [Tier2] BodyVeilMode Ghost (old default) -> Distortion (new default)");
            }
            var names = new List<string>();
            foreach (var kv in orphans) names.Add($"[{kv.Key.Section}] {kv.Key.Key} = {kv.Value}");
            orphans.Clear();
            _file.Save();
            Plugin.Log?.LogInfo($"Config: removed {names.Count} obsolete keys: {string.Join(", ", names)}");
        }

        /// <summary>Keys in the file that no bound entry claims (BepInEx's private ConfigFile.OrphanedEntries); null when unreadable.</summary>
        private static Dictionary<ConfigDefinition, string> Orphans()
        {
            try
            {
                var prop = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries");
                if (prop == null) Plugin.Log?.LogWarning("Config: ConfigFile.OrphanedEntries not found (BepInEx changed?); old keys stay in the file");
                return prop?.GetValue(_file) as Dictionary<ConfigDefinition, string>;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"Config: cannot read orphaned entries ({ex.Message}); old keys stay in the file");
                return null;
            }
        }

        /// <summary>Revision of the per-tier look defaults (<see cref="LookDefaults.Revision"/>); bump it there when a default the user asked for must reach existing config files.</summary>
        private const int LookDefaultsRevision = LookDefaults.Revision;

        /// <summary>
        /// The tier I and II looks changed on the user's request: revision 1 (round F, task 10f), revision 2 (round G, task 10g:
        /// absolute outer-layer keys, Emission, DynamicColor off), revision 3 (round H, task 10h: tier I = normal body in a thin
        /// fog layer that follows the body, ground fog field for tiers I and II), revision 4 (round I, task 10i: matte fog, tier II
        /// = the user's saved tuning, so a revision 3 file of that user loses nothing), revision 5 (plan 4 fix round 2: FogMaterial soft;
        /// only files below revision 4 get the full reset, a revision 4 file keeps its tuning), revision 6 (plan 4, round J ruling: the
        /// tier II outer layer is a ring around the player; a revision 4/5 file gets only its [Fog.Tier2] Outer* keys reset), revision 7
        /// (plan 4, round L ruling: tier I/II = enveloping body cloud plus a fog volume (OuterShape Volume) and a lighter ground field;
        /// every file below revision 7 gets [Fog.Tier1] and [Fog.Tier2] fully reset, old values logged), revision 8 (plan 4, round M
        /// ruling: subtle mid-grey haze, low alphas, the fog volume left behind in world space; again a full [Fog.Tier1]/[Fog.Tier2]
        /// reset of every older file). Existing files keep their old values, so once per
        /// file ([Fog] LookDefaultsRevision below the current revision) the [Fog.TierN] sections of <see cref="LookDefaults.ResetTiers"/>
        /// are reset to the new defaults (every bound key, including the new Ground* keys), the obsolete outer factor keys are
        /// logged with their values (MigrateAndDropOrphans then removes them), and [Tier1] BodyVeilMode Distortion (the revision 1/2
        /// default) becomes Off for files below revision 3. Tier III is untouched (approved as is). The log lists what happened.
        /// </summary>
        private static void MigrateLookDefaults()
        {
            var rev = _file.Bind(FogSection, "LookDefaultsRevision", 0,
                "Internal: revision of the look defaults applied to this file. Below the plugin's revision, the tiers whose defaults changed are reset once");
            if (rev.Value >= LookDefaultsRevision) return;
            var saveOnSet = _file.SaveOnConfigSet;
            _file.SaveOnConfigSet = false;
            try
            {
                // Log what is about to be overwritten so a user's tuned values can be recovered from the log.
                var differing = 0;
                var resetTiers = LookDefaults.ResetsTiers(rev.Value) ? LookDefaults.ResetTiers : new int[0];
                foreach (var t in resetTiers)
                    foreach (var kv in _fogEntries[t])
                    {
                        if (Equals(kv.Value.BoxedValue, kv.Value.DefaultValue)) continue;
                        differing++;
                        Plugin.Log?.LogInfo($"Config migration: [Fog.Tier{t}] {kv.Key} = {kv.Value.BoxedValue} (default {kv.Value.DefaultValue}) is reset");
                        kv.Value.BoxedValue = kv.Value.DefaultValue;
                    }
                // Revision 6: the [Fog.Tier2] Outer* keys move to the ring defaults (files below FullResetRevision were reset above;
                // since revision 7 (now 8) that is every older file, so this branch only runs if FullResetRevision is lowered again).
                var outerReset = 0;
                if (!LookDefaults.ResetsTiers(rev.Value))
                    foreach (var kv in _fogEntries[2])
                    {
                        if (!LookDefaults.ResetsOuterKey(2, kv.Key, rev.Value) || Equals(kv.Value.BoxedValue, kv.Value.DefaultValue)) continue;
                        outerReset++;
                        Plugin.Log?.LogInfo($"Config migration: [Fog.Tier2] {kv.Key} = {kv.Value.BoxedValue} (default {kv.Value.DefaultValue}) is reset (outer ring)");
                        kv.Value.BoxedValue = kv.Value.DefaultValue;
                    }
                var orphans = Orphans();
                if (orphans != null)
                    for (var t = 1; t <= 3; t++)
                        foreach (var key in FogSettings.ObsoleteKeys)
                            if (orphans.TryGetValue(new ConfigDefinition($"Fog.Tier{t}", key), out var old))
                                Plugin.Log?.LogInfo($"Config migration: [Fog.Tier{t}] {key} = {old} is obsolete (replaced by the absolute OuterRadius/OuterAlpha/OuterRate/OuterSize/OuterLifetime) and removed");
                var t1Mode = (ConfigEntry<string>)_tierEntries[1]["BodyVeilMode"];
                var oldMode = t1Mode.Value;
                var newMode = LookDefaults.MigrateBodyVeilMode(1, oldMode, rev.Value);
                var modeChanged = newMode != oldMode;
                if (modeChanged) t1Mode.Value = newMode;
                // Revision 5: FogMaterial swamp_mist (the old default) becomes soft on every tier; other choices stay.
                var materialsChanged = 0;
                for (var t = 1; t <= 3; t++)
                {
                    var entry = (ConfigEntry<string>)_fogEntries[t]["FogMaterial"];
                    var migrated = LookDefaults.MigrateFogMaterial(entry.Value, rev.Value);
                    if (migrated == entry.Value) continue;
                    Plugin.Log?.LogInfo($"Config migration: [Fog.Tier{t}] FogMaterial {entry.Value} -> {migrated}");
                    entry.Value = migrated;
                    materialsChanged++;
                }
                rev.Value = LookDefaultsRevision;
                Plugin.Log?.LogInfo(differing == 0 && outerReset == 0 && !modeChanged && materialsChanged == 0
                    ? $"Config migration (look defaults revision {LookDefaultsRevision}): nothing to change, the file already holds the defaults"
                    : $"Config migration (look defaults revision {LookDefaultsRevision}): {differing} [Fog.Tier1]/[Fog.Tier2] values reset to the new defaults" +
                      (outerReset > 0 ? $"; {outerReset} [Fog.Tier2] Outer* values reset to the ring defaults" : "") +
                      (modeChanged ? $"; [Tier1] BodyVeilMode {oldMode} -> {newMode}" : "") +
                      (materialsChanged > 0 ? $"; FogMaterial -> soft on {materialsChanged} tier(s)" : ""));
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"Config migration (look defaults) failed: {ex.Message}");
            }
            finally
            {
                _file.SaveOnConfigSet = saveOnSet;
            }
            _file.Save();
        }

        private static void BindGlobal<T>(string key, T value, string description) =>
            _globalEntries[key] = Bind("General", key, value, description);

        private static void BindLook<T>(string section, string key, T value, string description) =>
            _lookEntries[key] = _file.Bind(section, key, value, description);

        private static ConfigEntry<T> Bind<T>(string section, string key, T value, string description, AcceptableValueBase acceptable = null) =>
            _file.Bind(section, key, value, new ConfigDescription(description, acceptable, new ConfigurationManagerAttributes { IsAdminOnly = true }));

        private static readonly string[] BodyVeilModes = { "Off", "Cutoff", "Hide", "Tint", "Ghost", "Distortion", "Shadow", "Spirit" };

        /// <summary>The mode names with the tier's default first: BepInEx clamps an unlisted value to the first entry, i.e. the default.</summary>
        private static AcceptableValueList<string> ModeValues(string tierDefault)
        {
            var list = new List<string> { tierDefault };
            foreach (var m in BodyVeilModes) if (m != tierDefault) list.Add(m);
            return new AcceptableValueList<string>(list.ToArray());
        }

        /// <summary>The tier's default BodyVeilMode (fallback for a malformed value).</summary>
        public static string DefaultBodyVeilMode(int tier) => GetDefault<string>(_tierEntries[tier], "BodyVeilMode");

        private static T Get<T>(Dictionary<string, ConfigEntryBase> entries, string key) => ((ConfigEntry<T>)entries[key]).Value;

        private static T GetDefault<T>(Dictionary<string, ConfigEntryBase> entries, string key) => (T)entries[key].DefaultValue;

        private static TierConfig BuildTier(int t, bool defaults)
        {
            var e = _tierEntries[t];
            T V<T>(string key) => defaults ? GetDefault<T>(e, key) : Get<T>(e, key);
            return new TierConfig
            {
                Tier = t,
                Duration = V<float>("Duration"),
                StealthModifier = V<float>("StealthModifier"),
                NoiseModifier = V<float>("NoiseModifier"),
                IgnoredByEnemies = V<bool>("IgnoredByEnemies"),
                HiddenFromPlayers = V<bool>("HiddenFromPlayers"),
                AggroLossTime = V<float>("AggroLossTime"),
                RehideDelay = V<float>("RehideDelay"),
                DebuffStaminaRegenMultiplier = V<float>("DebuffStaminaRegenMultiplier"),
                DebuffDuration = V<float>("DebuffDuration"),
                Cooldown = V<float>("Cooldown"),
                Recipe = V<string>("Recipe"),
                BodyVeilMode = V<string>("BodyVeilMode"),
            };
        }

        /// <summary>Rebuilds the snapshots from the bound entries and raises Changed. Never throws: an invalid tier falls back to its defaults with a warning.</summary>
        public static void Refresh()
        {
            var tiers = new TierConfig[4];
            for (var t = 1; t <= 3; t++)
            {
                var tc = BuildTier(t, false);
                try
                {
                    tc.Validate();
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    Plugin.Log?.LogWarning($"Invalid config in [Tier{t}] {ex.ParamName} = {ex.ActualValue}: {ex.Message} Using defaults for this tier.");
                    tc = BuildTier(t, true);
                }
                tiers[t] = tc;
            }
            var global = new GlobalConfig
            {
                RevealOnDamage = Get<bool>(_globalEntries, "RevealOnDamage"),
                RevealOnBlock = Get<bool>(_globalEntries, "RevealOnBlock"),
                RevealOnBowDraw = Get<bool>(_globalEntries, "RevealOnBowDraw"),
                ShowSelfFaintly = Get<bool>(_globalEntries, "ShowSelfFaintly"),
                FogCutoffLight = Get<float>(_globalEntries, "FogCutoffLight"),
                FogCutoffDense = Get<float>(_globalEntries, "FogCutoffDense"),
                FogCutoffSelf = Get<float>(_globalEntries, "FogCutoffSelf"),
                AllowPvpInvisibility = Get<bool>(_globalEntries, "AllowPvpInvisibility"),
                GhostColor = Get<string>(_lookEntries, "GhostColor"),
                GhostEmission = Get<float>(_lookEntries, "GhostEmission"),
                ShadowColor = Get<string>(_lookEntries, "ShadowColor"),
                SpiritColor = Get<string>(_lookEntries, "SpiritColor"),
                SpiritStrength = Get<float>(_lookEntries, "SpiritStrength"),
            };
            var fog = new FogSettings[4];
            for (var t = 1; t <= 3; t++) fog[t] = BuildFog(t);
            var modeText = Get<string>(_lookEntries, "FogAlphaMode");
            var alphaMode = FogAlphaMode.Both;
            if (!FogSettings.TryParseAlphaMode(modeText, out alphaMode))
            {
                alphaMode = FogAlphaMode.Both;
                Plugin.Log?.LogWarning($"[Fog] FogAlphaMode '{modeText}' is not Both, Material or Vertex; using Both");
            }
            for (var t = 1; t <= 3; t++) { _tiers[t] = tiers[t]; _fog[t] = fog[t]; }
            Global = global;
            FogAlphaMode = alphaMode;
            Changed?.Invoke();
        }

        /// <summary>Builds a tier's look from [Fog.TierN]. A malformed value falls back to its default with a warning.</summary>
        private static FogSettings BuildFog(int tier)
        {
            var e = _fogEntries[tier];
            var warnings = new List<string>();
            var s = FogSettings.Parse(tier, key => e.TryGetValue(key, out var entry) ? Convert.ToString(entry.BoxedValue, CultureInfo.InvariantCulture) : null, warnings);
            foreach (var w in warnings) Plugin.Log?.LogWarning($"[Fog.Tier{tier}] {w}");
            return s;
        }

        /// <summary>Writes a tier's look into [Fog.TierN] plus the global alpha mode and saves the file once. Does not raise Changed.</summary>
        public static void SaveFog(int tier, FogSettings s, FogAlphaMode alphaMode)
        {
            var e = _fogEntries[tier];
            var saveOnSet = _file.SaveOnConfigSet;
            _file.SaveOnConfigSet = false;   // one write instead of one per key
            try
            {
                foreach (var key in FogSettings.Keys)
                {
                    var text = s.Get(key.Name);
                    if (text == null || !e.TryGetValue(key.Name, out var entry)) continue;
                    switch (key.Kind)
                    {
                        case FogValueKind.Float: if (FloatList.TryParseOne(text, out var f)) ((ConfigEntry<float>)entry).Value = f; break;
                        case FogValueKind.Bool: if (FogAnchor.TryParseSwitch(text, out var b)) ((ConfigEntry<bool>)entry).Value = b; break;
                        default: ((ConfigEntry<string>)entry).Value = text; break;
                    }
                }
                ((ConfigEntry<string>)_lookEntries["FogAlphaMode"]).Value = alphaMode.ToString();
            }
            finally
            {
                _file.SaveOnConfigSet = saveOnSet;
            }
            _file.Save();
            _fog[tier] = s.Clone();
            FogAlphaMode = alphaMode;
        }

        /// <summary>Writes a tier's BodyVeilMode ([TierN], admin-synced: on a client the server's value wins at the next sync). Does not raise Changed.</summary>
        public static void SaveBodyVeilMode(int tier, string mode)
        {
            ((ConfigEntry<string>)_tierEntries[tier]["BodyVeilMode"]).Value = mode;
            _file.Save();
        }

        /// <summary>Writes the global body looks (Ghost, Shadow, Spirit) into [Veil] and saves the file once (ip_veil save, tuning window). Does not raise Changed.</summary>
        public static void SaveVeilLook(string ghostColor, float ghostEmission, string shadowColor, string spiritColor, float spiritStrength)
        {
            var saveOnSet = _file.SaveOnConfigSet;
            _file.SaveOnConfigSet = false;
            try
            {
                ((ConfigEntry<string>)_lookEntries["GhostColor"]).Value = ghostColor;
                ((ConfigEntry<float>)_lookEntries["GhostEmission"]).Value = ghostEmission;
                ((ConfigEntry<string>)_lookEntries["ShadowColor"]).Value = shadowColor;
                ((ConfigEntry<string>)_lookEntries["SpiritColor"]).Value = spiritColor;
                ((ConfigEntry<float>)_lookEntries["SpiritStrength"]).Value = spiritStrength;
            }
            finally
            {
                _file.SaveOnConfigSet = saveOnSet;
            }
            _file.Save();
        }

        /// <summary>Re-reads the file from disk and refreshes. Used by ip_reload_config.</summary>
        public static void Reload()
        {
            _file.Reload();
            Refresh();
        }
    }
}
