using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using InvisibilityPotion.Goggles;
using InvisibilityPotion.Plants;

namespace InvisibilityPotion.Config
{
    /// <summary>
    /// Plan 5 config: [Plants] (hidden plants, Huldra's Hair on trees, wild Baldr's Tear / Hel's Ember Fern) and [Goggles].
    /// Server-synced, admin only, like the [TierN] entries. Recipes and zone chances are read once at registration (restart);
    /// stage/regrow minutes, tree chance, reveal distance, yields and RevealHiddenPlayers are read live (the server value wins).
    /// </summary>
    public static partial class PluginConfig
    {
        private const string PlantsSection = "Plants";
        private const string GogglesSection = "Goggles";
        private static readonly Dictionary<string, ConfigEntryBase> _plantEntries = new Dictionary<string, ConfigEntryBase>();

        private static void BindPlants()
        {
            _plantEntries["LichenTreeChance"] = Bind(PlantsSection, "LichenTreeChance", 0.025f,
                "Chance that an eligible Black Forest fir/pine carries Huldra's Hair (deterministic per tree position and world seed)", new AcceptableValueRange<float>(0f, 1f));
            _plantEntries["LichenStageMinutes"] = Bind(PlantsSection, "LichenStageMinutes", 120f,
                "Minutes of world time (vanilla thistle: 240) per growth stage of Huldra's Hair (S1 -> S2 -> S3); a pick resets to S1, so regrowth takes twice this", new AcceptableValueRange<float>(0f, 100000f));
            _plantEntries["LichenRevealDistance"] = Bind(PlantsSection, "LichenRevealDistance", 40f,
                "Metres within which goggles show the lichen on a tree (the tree's distant LOD does not carry it); 0 = no limit", new AcceptableValueRange<float>(0f, 1000f));
            _plantEntries["HuldraCultivable"] = Bind(PlantsSection, "HuldraCultivable", true,
                "Huldra's Hair can be planted with the Cultivator on the trunk of a fir or pine (within 1 m; costs 1 Huldra's Hair). Read at startup");
            _plantEntries["CultivateMinutes"] = Bind(PlantsSection, "CultivateMinutes", 240f,
                "Minutes of world time (vanilla thistle: 240) until a planted Baldr's Tear or Hel's Ember Fern sprout is grown (Cultivator, cultivated ground in its own biome). Read at startup", new AcceptableValueRange<float>(1f, 100000f));
            _plantEntries["BaldrZoneChance"] = Bind(PlantsSection, "BaldrZoneChance", PlantDefaults.BaldrZoneChance,
                "Chance per newly generated Mountains zone to get a Baldr's Tear group (1-2 plants within 4 m, variants mixed; 0.2 = 1 zone in 5). Plants only on dry land at least 0.5 m above sea level. Read at startup; only new zones", new AcceptableValueRange<float>(0f, 0.99f));
            _plantEntries["HelFernZoneChance"] = Bind(PlantsSection, "HelFernZoneChance", PlantDefaults.HelFernZoneChance,
                "Chance per newly generated Ashlands zone to get a Hel's Ember Fern group (3-6 plants within 6 m, variants mixed; 0.1 = 1 zone in 10). Plants only on solid ground at least 0.5 m above sea level and off lava. Read at startup; only new zones", new AcceptableValueRange<float>(0f, 0.99f));
            _plantEntries["GroundRegrowMinutes"] = Bind(PlantsSection, "GroundRegrowMinutes", 240f,
                "Minutes of world time (vanilla thistle: 240) until a picked Baldr's Tear or Hel's Ember Fern is ripe again", new AcceptableValueRange<float>(0f, 100000f));
            for (var t = 1; t <= 3; t++)
                _plantEntries[$"YieldT{t}"] = Bind(PlantsSection, $"YieldT{t}", PlantYield.DefaultYieldText(t),
                    $"Items per pick of the tier {t} plant as min-max, rolled per pick" + (t == 1 ? "" : "; bigger plants give up to 25 % more, smaller up to 25 % less"));
            for (var t = 1; t <= 3; t++)
                _plantEntries[$"RecipeT{t}"] = Bind(GogglesSection, $"RecipeT{t}", GoggleLevel.DefaultRecipe(t),
                    $"Recipe of the tier {t} veil goggles at the {GoggleLevel.Station(t)}: Item:Amount,Item:Amount. Read once at startup");
            _plantEntries["RevealHiddenPlayers"] = Bind(GogglesSection, "RevealHiddenPlayers", true,
                "Level III goggles show players hidden by a tier III mead (nameplate, veiled body, real position; never a map pin)");
            MigratePlantDefaults();
        }

        /// <summary>
        /// Once per file ([General] PlantDefaultsRevision below <see cref="PlantDefaults.Revision"/>) a [Plants] value still at an old
        /// default moves to the new one (<see cref="PlantDefaults.Migrate"/>); customised values stay. Same pattern as the gameplay
        /// defaults migration.
        /// </summary>
        private static void MigratePlantDefaults()
        {
            try
            {
                var rev = _file.Bind("General", "PlantDefaultsRevision", 0,
                    "Internal: revision of the [Plants] defaults applied to this file. Below the plugin's revision, values still at an old default move to the new default once");
                if (rev.Value >= PlantDefaults.Revision) return;
                var changed = new List<string>();
                foreach (var key in PlantDefaults.Keys)
                {
                    if (!(_plantEntries.TryGetValue(key, out var e) && e is ConfigEntry<float> entry)) continue;
                    var migrated = PlantDefaults.Migrate(key, entry.Value, rev.Value);
                    if (migrated.Equals(entry.Value)) continue;
                    changed.Add($"[Plants] {key} {entry.Value.ToString(CultureInfo.InvariantCulture)} -> {migrated.ToString(CultureInfo.InvariantCulture)}");
                    entry.Value = migrated;
                }
                rev.Value = PlantDefaults.Revision;
                Plugin.Log?.LogInfo(changed.Count == 0
                    ? $"Config migration (plant defaults revision {PlantDefaults.Revision}): nothing to change"
                    : $"Config migration (plant defaults revision {PlantDefaults.Revision}): {string.Join(", ", changed)}");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"Config migration (plant defaults) failed: {ex.Message}");
            }
        }

        private static T PlantValue<T>(string key, T fallback) =>
            _plantEntries.TryGetValue(key, out var e) && e is ConfigEntry<T> typed ? typed.Value : fallback;

        public static float LichenTreeChance => PlantValue("LichenTreeChance", 0.025f);
        public static float LichenStageMinutes => PlantValue("LichenStageMinutes", 120f);
        public static float LichenRevealDistance => PlantValue("LichenRevealDistance", 40f);
        public static bool HuldraCultivable => PlantValue("HuldraCultivable", true);
        public static float CultivateMinutes => PlantValue("CultivateMinutes", 240f);
        public static float BaldrZoneChance => PlantValue("BaldrZoneChance", PlantDefaults.BaldrZoneChance);
        public static float HelFernZoneChance => PlantValue("HelFernZoneChance", PlantDefaults.HelFernZoneChance);
        public static float GroundRegrowMinutes => PlantValue("GroundRegrowMinutes", 240f);
        /// <summary>[Plants] YieldTN as (min, max); a malformed value falls back to the default with one warning per text.</summary>
        public static (int min, int max) Yield(int tier)
        {
            var text = PlantValue($"YieldT{tier}", PlantYield.DefaultYieldText(tier));
            if (PlantYield.TryParseRange(text, out var min, out var max)) return (min, max);
            if (_badYields.Add(text)) Plugin.Log?.LogWarning($"[Plants] YieldT{tier} '{text}' is not min-max; using {PlantYield.DefaultYieldText(tier)}");
            return PlantYield.DefaultYields[tier];
        }

        private static readonly HashSet<string> _badYields = new HashSet<string>();
        public static string GoggleRecipe(int tier) => PlantValue($"RecipeT{tier}", GoggleLevel.DefaultRecipe(tier));
        public static bool RevealHiddenPlayers => PlantValue("RevealHiddenPlayers", true);

        /// <summary>
        /// Plan 5 adds 2 veil ingredients to every mead base. Once per file ([General] RecipeDefaultsRevision below 1) a stored
        /// [TierN] Recipe equal to the old default becomes the new default; customised recipes are kept. On every start a recipe that
        /// lacks its tier's ingredient is named in a warning.
        /// </summary>
        private const int RecipeDefaultsRevision = 1;

        private static void MigrateMeadRecipes()
        {
            try
            {
                var rev = _file.Bind("General", "RecipeDefaultsRevision", 0,
                    "Internal: revision of the mead recipe defaults applied to this file. Below the plugin's revision, recipes still at an old default are updated once");
                for (var t = 1; t <= 3; t++)
                {
                    var entry = (ConfigEntry<string>)_tierEntries[t]["Recipe"];
                    var (value, outcome) = MeadRecipes.Migrate(t, entry.Value);
                    if (outcome == MeadRecipes.Outcome.Migrated && rev.Value < RecipeDefaultsRevision)
                    {
                        Plugin.Log?.LogInfo($"Config migration: [Tier{t}] Recipe {entry.Value} (old default) -> {value}");
                        entry.Value = value;
                    }
                    else if (outcome == MeadRecipes.Outcome.MissingIngredient)
                        Plugin.Log?.LogWarning($"[Tier{t}] Recipe '{entry.Value}' is customised and has no {MeadRecipes.IngredientName(t)}; kept as is (default: {MeadRecipes.NewDefault(t)})");
                }
                if (rev.Value < RecipeDefaultsRevision) rev.Value = RecipeDefaultsRevision;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"Config migration (mead recipes) failed: {ex.Message}");
            }
        }
    }
}
