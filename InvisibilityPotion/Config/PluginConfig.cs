using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using InvisibilityPotion.Visuals;

namespace InvisibilityPotion.Config
{
    /// <summary>Binds every config entry (server-synced via Jötunn, admin only) and exposes plain TierConfig/GlobalConfig snapshots.</summary>
    public static class PluginConfig
    {
        private static ConfigFile _file;
        private static readonly Dictionary<int, Dictionary<string, ConfigEntryBase>> _tierEntries = new Dictionary<int, Dictionary<string, ConfigEntryBase>>();
        private static readonly Dictionary<string, ConfigEntryBase> _globalEntries = new Dictionary<string, ConfigEntryBase>();
        // Look-only entries ([Fog], [Veil]): local per client, not server-synced, so each player can tune what they see.
        private static readonly Dictionary<string, ConfigEntryBase> _lookEntries = new Dictionary<string, ConfigEntryBase>();
        private const string FogSection = "Fog";
        private const string VeilSection = "Veil";
        private static readonly TierConfig[] _tiers = new TierConfig[4];

        public static GlobalConfig Global { get; private set; } = new GlobalConfig();
        /// <summary>Fog look from the [Fog] section. A fresh copy per Refresh; FogVeil clones it for live tuning.</summary>
        public static FogSettings Fog { get; private set; } = FogSettings.Defaults();
        public static event Action Changed;

        public static TierConfig Tier(int tier)
        {
            if (tier < 1 || tier > 3) throw new ArgumentOutOfRangeException(nameof(tier));
            return _tiers[tier];
        }

        public static void Bind(ConfigFile file)
        {
            _file = file;
            BindTier(1, 60f, 0.25f, 0.25f, false, false, 5f, 0f, "Honey:10,Thistle:5", "Off", true, 0.35f);
            BindTier(2, 120f, 1f, 1f, true, false, 1f, 12f, "Honey:10,Thistle:5,Bloodbag:3", "Ghost", true, 0.7f);
            BindTier(3, 180f, 1f, 1f, true, true, 1f, 8f, "Honey:10,Thistle:5,Bloodbag:3,YmirRemains:1", "Distortion", false, 0f);
            BindGlobal("RevealOnDamage", true, "Taking damage reveals a hidden player");
            BindGlobal("RevealOnBlock", true, "A blocked hit or parry reveals a hidden player");
            BindGlobal("RevealOnBowDraw", true, "Drawing a bow reveals a hidden player");
            BindGlobal("ShowSelfFaintly", true, "The hidden player still sees a faint version of themselves");
            BindGlobal("FogCutoffLight", 0.5f, "Alpha cutoff applied to the player's materials for the light veil (tier I)");
            BindGlobal("FogCutoffDense", 0.8f, "Alpha cutoff for the dense veil (tier II/III)");
            BindGlobal("FogCutoffSelf", 0.3f, "Alpha cutoff the hidden player sees on themselves");
            BindGlobal("AllowPvpInvisibility", true, "Server switch for hiding players from other players (tier III)");
            var fog = FogSettings.Defaults();
            BindLook(FogSection, "FogRate", fog.Rate, "Fog particles per second per body emitter at FogDensity 1 (scaled by the tier's FogDensity)");
            BindLook(FogSection, "FogSize", fog.Size, "Fog particle size in metres (randomised 0.6x..1.25x)");
            BindLook(FogSection, "FogLifetime", fog.Lifetime, "Fog particle lifetime in seconds (randomised 0.8x..1.2x)");
            BindLook(FogSection, "FogSpeed", fog.Speed, "Fog particle start speed in m/s");
            BindLook(FogSection, "FogAlpha", fog.Alpha, $"Fog alpha at FogDensity {FogSettings.ReferenceDensity}; scales linearly with the tier's FogDensity");
            BindLook(FogSection, "FogColor", FloatList.Format(fog.R, fog.G, fog.B), "Fog colour as r,g,b (0..1)");
            BindLook(FogSection, "FogDrift", fog.Drift, "Upward drift of fog particles in m/s");
            BindLook(FogSection, "FogAlphaMode", fog.AlphaMode.ToString(), "Where the fog alpha goes: Both (split between material and particle colour), Material or Vertex");
            foreach (var a in fog.Anchors)
                BindLook(FogSection, "Anchor." + a.Name, a.Format(), $"Fog emitter on the {a.Name} bone: on|off,radius,x,y,z (offset in metres, player space: x right, y up, z forward)");
            BindLook(VeilSection, "DistortionStrength", 0.2f, "Refraction strength of the Distortion body mode (shader property _RefractionIntensity)");
            BindLook(VeilSection, "DistortionColor", "1,1,1,0.15", "Colour of the Distortion body mode as r,g,b,a (shader property _Color)");
            BindLook(VeilSection, "GhostColor", "0.75,0.8,0.9,1", "Colour of the Ghost body mode as r,g,b,a (_Color; the shader is alpha-tested, low alpha may cut the body away)");
            BindLook(VeilSection, "GhostEmission", 0.25f, "Multiplier on the Ghost material's glow (_EmissionColor); 1 = vanilla ghost glow");
            Refresh();
        }

        private static void BindTier(int tier, float duration, float stealth, float noise, bool ignored, bool hiddenFromPlayers, float aggroLoss, float rehide, string recipe,
                                     string bodyMode, bool fogEnabled, float fogDensity)
        {
            var section = $"Tier{tier}";
            var e = new Dictionary<string, ConfigEntryBase>
            {
                ["Duration"] = Bind(section, "Duration", duration, "Effect duration in seconds"),
                ["StealthModifier"] = Bind(section, "StealthModifier", stealth, "Fraction of vanilla visibility while hidden (1 = vanilla, 0.25 = a quarter). Tier I only."),
                ["NoiseModifier"] = Bind(section, "NoiseModifier", noise, "Fraction of vanilla noise while hidden (1 = vanilla). Tier I only."),
                ["IgnoredByEnemies"] = Bind(section, "IgnoredByEnemies", ignored, "Enemies cannot see or hear the player at all"),
                ["HiddenFromPlayers"] = Bind(section, "HiddenFromPlayers", hiddenFromPlayers, "Other players cannot see this player's position, model or nameplate"),
                ["AggroLossTime"] = Bind(section, "AggroLossTime", aggroLoss, "Seconds a chasing enemy keeps searching before it gives up"),
                ["RehideDelay"] = Bind(section, "RehideDelay", rehide, "Seconds without attacking until hidden again; 0 = an attack ends the effect"),
                ["DebuffStaminaRegenMultiplier"] = Bind(section, "DebuffStaminaRegenMultiplier", 0.5f, "Stamina regeneration multiplier after revealing"),
                ["DebuffDuration"] = Bind(section, "DebuffDuration", 20f, "Debuff duration in seconds, restarted on every reveal"),
                ["Cooldown"] = Bind(section, "Cooldown", 0f, "Reserved; not used in plan 2"),
                ["Recipe"] = Bind(section, "Recipe", recipe, "Mead base recipe at the cauldron: Item:Amount,Item:Amount"),
                ["BodyVeilMode"] = Bind(section, "BodyVeilMode", bodyMode, "How the hidden player's body is drawn: Off, Cutoff, Hide, Tint, Ghost or Distortion. The Debug command ip_veil overrides it in memory",
                                        ModeValues(bodyMode)),
                ["FogEnabled"] = Bind(section, "FogEnabled", fogEnabled, "Body-anchored fog around the hidden player"),
                ["FogDensity"] = Bind(section, "FogDensity", fogDensity, "Fog density 0..1; scales the fog rate and alpha"),
            };
            _tierEntries[tier] = e;
        }

        private static void BindGlobal<T>(string key, T value, string description) =>
            _globalEntries[key] = Bind("General", key, value, description);

        private static void BindLook<T>(string section, string key, T value, string description) =>
            _lookEntries[key] = _file.Bind(section, key, value, description);

        private static ConfigEntry<T> Bind<T>(string section, string key, T value, string description, AcceptableValueBase acceptable = null) =>
            _file.Bind(section, key, value, new ConfigDescription(description, acceptable, new ConfigurationManagerAttributes { IsAdminOnly = true }));

        private static readonly string[] BodyVeilModes = { "Off", "Cutoff", "Hide", "Tint", "Ghost", "Distortion" };

        /// <summary>The six mode names with the tier's default first: BepInEx clamps an unlisted value to the first entry, i.e. the default.</summary>
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
                FogEnabled = V<bool>("FogEnabled"),
                FogDensity = Math.Max(0f, Math.Min(1f, V<float>("FogDensity"))),
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
                DistortionStrength = Get<float>(_lookEntries, "DistortionStrength"),
                DistortionColor = Get<string>(_lookEntries, "DistortionColor"),
                GhostColor = Get<string>(_lookEntries, "GhostColor"),
                GhostEmission = Get<float>(_lookEntries, "GhostEmission"),
            };
            var fog = BuildFog();
            for (var t = 1; t <= 3; t++) _tiers[t] = tiers[t];
            Global = global;
            Fog = fog;
            Changed?.Invoke();
        }

        /// <summary>Builds the fog look from [Fog]. A malformed value falls back to its default with a warning.</summary>
        private static FogSettings BuildFog()
        {
            var s = FogSettings.Defaults();
            s.Rate = Math.Max(0f, Get<float>(_lookEntries, "FogRate"));
            s.Size = Math.Max(0.01f, Get<float>(_lookEntries, "FogSize"));
            s.Lifetime = Math.Max(0.05f, Get<float>(_lookEntries, "FogLifetime"));
            s.Speed = Get<float>(_lookEntries, "FogSpeed");
            s.Alpha = Math.Max(0f, Math.Min(1f, Get<float>(_lookEntries, "FogAlpha")));
            s.Drift = Get<float>(_lookEntries, "FogDrift");
            var color = Get<string>(_lookEntries, "FogColor");
            if (FloatList.TryParse(color, 3, out var rgb)) { s.R = rgb[0]; s.G = rgb[1]; s.B = rgb[2]; }
            else Plugin.Log?.LogWarning($"[Fog] FogColor '{color}' is not r,g,b; using default");
            var mode = Get<string>(_lookEntries, "FogAlphaMode");
            if (FogSettings.TryParseAlphaMode(mode, out var alphaMode)) s.AlphaMode = alphaMode;
            else Plugin.Log?.LogWarning($"[Fog] FogAlphaMode '{mode}' is not Both, Material or Vertex; using {s.AlphaMode}");
            for (var i = 0; i < s.Anchors.Count; i++)
            {
                var name = s.Anchors[i].Name;
                var text = Get<string>(_lookEntries, "Anchor." + name);
                if (FogAnchor.TryParse(name, text, out var anchor)) s.Anchors[i] = anchor;
                else Plugin.Log?.LogWarning($"[Fog] Anchor.{name} '{text}' is not on|off,radius,x,y,z; using default");
            }
            return s;
        }

        /// <summary>Writes a fog look into the [Fog] entries and saves the file (ip_fog save). Does not raise Changed.</summary>
        public static void SaveFog(FogSettings s)
        {
            ((ConfigEntry<float>)_lookEntries["FogRate"]).Value = s.Rate;
            ((ConfigEntry<float>)_lookEntries["FogSize"]).Value = s.Size;
            ((ConfigEntry<float>)_lookEntries["FogLifetime"]).Value = s.Lifetime;
            ((ConfigEntry<float>)_lookEntries["FogSpeed"]).Value = s.Speed;
            ((ConfigEntry<float>)_lookEntries["FogAlpha"]).Value = s.Alpha;
            ((ConfigEntry<string>)_lookEntries["FogColor"]).Value = FloatList.Format(s.R, s.G, s.B);
            ((ConfigEntry<float>)_lookEntries["FogDrift"]).Value = s.Drift;
            ((ConfigEntry<string>)_lookEntries["FogAlphaMode"]).Value = s.AlphaMode.ToString();
            foreach (var a in s.Anchors)
                if (_lookEntries.TryGetValue("Anchor." + a.Name, out var e)) ((ConfigEntry<string>)e).Value = a.Format();
            _file.Save();
            Fog = s.Clone();
        }

        /// <summary>Writes the Distortion/Ghost look into [Veil] and saves the file (ip_veil save). Does not raise Changed.</summary>
        public static void SaveVeilLook(float distortionStrength, string distortionColor, string ghostColor, float ghostEmission)
        {
            ((ConfigEntry<float>)_lookEntries["DistortionStrength"]).Value = distortionStrength;
            ((ConfigEntry<string>)_lookEntries["DistortionColor"]).Value = distortionColor;
            ((ConfigEntry<string>)_lookEntries["GhostColor"]).Value = ghostColor;
            ((ConfigEntry<float>)_lookEntries["GhostEmission"]).Value = ghostEmission;
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
