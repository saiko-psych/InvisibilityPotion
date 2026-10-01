using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Jotunn.Utils;

namespace InvisibilityPotion.Config
{
    /// <summary>Binds every config entry (server-synced via Jötunn, admin only) and exposes plain TierConfig/GlobalConfig snapshots.</summary>
    public static class PluginConfig
    {
        private static ConfigFile _file;
        private static readonly Dictionary<int, Dictionary<string, ConfigEntryBase>> _tierEntries = new Dictionary<int, Dictionary<string, ConfigEntryBase>>();
        private static readonly Dictionary<string, ConfigEntryBase> _globalEntries = new Dictionary<string, ConfigEntryBase>();
        private static readonly TierConfig[] _tiers = new TierConfig[4];

        public static GlobalConfig Global { get; private set; } = new GlobalConfig();
        public static event Action Changed;

        public static TierConfig Tier(int tier)
        {
            if (tier < 1 || tier > 3) throw new ArgumentOutOfRangeException(nameof(tier));
            return _tiers[tier];
        }

        public static void Bind(ConfigFile file)
        {
            _file = file;
            BindTier(1, 60f, 0.25f, 0.25f, false, 5f, 0f, "Honey:10,Thistle:5");
            BindTier(2, 120f, 1f, 1f, true, 1f, 12f, "Honey:10,Thistle:5,Bloodbag:3");
            BindTier(3, 180f, 1f, 1f, true, 1f, 8f, "Honey:10,Thistle:5,Bloodbag:3,YmirRemains:1");
            BindGlobal("RevealOnDamage", true, "Taking damage reveals a hidden player");
            BindGlobal("RevealOnBlock", true, "A blocked hit or parry reveals a hidden player");
            BindGlobal("RevealOnBowDraw", true, "Drawing a bow reveals a hidden player");
            BindGlobal("ShowSelfFaintly", true, "The hidden player still sees a faint version of themselves");
            BindGlobal("FogCutoffLight", 0.5f, "Alpha cutoff applied to the player's materials for the light veil (tier I)");
            BindGlobal("FogCutoffDense", 0.8f, "Alpha cutoff for the dense veil (tier II/III)");
            BindGlobal("FogCutoffSelf", 0.3f, "Alpha cutoff the hidden player sees on themselves");
            Refresh();
        }

        private static void BindTier(int tier, float duration, float stealth, float noise, bool ignored, float aggroLoss, float rehide, string recipe)
        {
            var section = $"Tier{tier}";
            var e = new Dictionary<string, ConfigEntryBase>
            {
                ["Duration"] = Bind(section, "Duration", duration, "Effect duration in seconds"),
                ["StealthModifier"] = Bind(section, "StealthModifier", stealth, "Fraction of vanilla visibility while hidden (1 = vanilla, 0.25 = a quarter). Tier I only."),
                ["NoiseModifier"] = Bind(section, "NoiseModifier", noise, "Fraction of vanilla noise while hidden (1 = vanilla). Tier I only."),
                ["IgnoredByEnemies"] = Bind(section, "IgnoredByEnemies", ignored, "Enemies cannot see or hear the player at all"),
                ["AggroLossTime"] = Bind(section, "AggroLossTime", aggroLoss, "Seconds a chasing enemy keeps searching before it gives up"),
                ["RehideDelay"] = Bind(section, "RehideDelay", rehide, "Seconds without attacking until hidden again; 0 = an attack ends the effect"),
                ["DebuffStaminaRegenMultiplier"] = Bind(section, "DebuffStaminaRegenMultiplier", 0.5f, "Stamina regeneration multiplier after revealing"),
                ["DebuffDuration"] = Bind(section, "DebuffDuration", 20f, "Debuff duration in seconds, restarted on every reveal"),
                ["Cooldown"] = Bind(section, "Cooldown", 0f, "Reserved; not used in plan 2"),
                ["Recipe"] = Bind(section, "Recipe", recipe, "Mead base recipe at the cauldron: Item:Amount,Item:Amount"),
            };
            _tierEntries[tier] = e;
        }

        private static void BindGlobal<T>(string key, T value, string description) =>
            _globalEntries[key] = Bind("General", key, value, description);

        private static ConfigEntry<T> Bind<T>(string section, string key, T value, string description) =>
            _file.Bind(section, key, value, new ConfigDescription(description, null, new ConfigurationManagerAttributes { IsAdminOnly = true }));

        private static T Get<T>(Dictionary<string, ConfigEntryBase> entries, string key) => ((ConfigEntry<T>)entries[key]).Value;

        /// <summary>Rebuilds the snapshots from the bound entries and raises Changed. Call after Config.Reload() or a sync.</summary>
        public static void Refresh()
        {
            for (var t = 1; t <= 3; t++)
            {
                var e = _tierEntries[t];
                var tc = new TierConfig
                {
                    Tier = t,
                    Duration = Get<float>(e, "Duration"),
                    StealthModifier = Get<float>(e, "StealthModifier"),
                    NoiseModifier = Get<float>(e, "NoiseModifier"),
                    IgnoredByEnemies = Get<bool>(e, "IgnoredByEnemies"),
                    AggroLossTime = Get<float>(e, "AggroLossTime"),
                    RehideDelay = Get<float>(e, "RehideDelay"),
                    DebuffStaminaRegenMultiplier = Get<float>(e, "DebuffStaminaRegenMultiplier"),
                    DebuffDuration = Get<float>(e, "DebuffDuration"),
                    Cooldown = Get<float>(e, "Cooldown"),
                    Recipe = Get<string>(e, "Recipe"),
                };
                tc.Validate();
                _tiers[t] = tc;
            }
            Global = new GlobalConfig
            {
                RevealOnDamage = Get<bool>(_globalEntries, "RevealOnDamage"),
                RevealOnBlock = Get<bool>(_globalEntries, "RevealOnBlock"),
                RevealOnBowDraw = Get<bool>(_globalEntries, "RevealOnBowDraw"),
                ShowSelfFaintly = Get<bool>(_globalEntries, "ShowSelfFaintly"),
                FogCutoffLight = Get<float>(_globalEntries, "FogCutoffLight"),
                FogCutoffDense = Get<float>(_globalEntries, "FogCutoffDense"),
                FogCutoffSelf = Get<float>(_globalEntries, "FogCutoffSelf"),
            };
            Changed?.Invoke();
        }

        /// <summary>Re-reads the file from disk and refreshes. Used by ip_reload_config.</summary>
        public static void Reload()
        {
            _file.Reload();
            Refresh();
        }
    }
}
