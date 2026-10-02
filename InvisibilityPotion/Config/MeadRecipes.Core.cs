using System;
using System.Collections.Generic;

namespace InvisibilityPotion.Config
{
    /// <summary>
    /// Mead base recipe defaults and their migration (spec plan 5 §3.6), no game types. Plan 5 adds 2 of the tier's veil ingredient
    /// to each base. A stored recipe equal to the old default is replaced by the new default; a customised recipe is kept, and
    /// when it lacks the ingredient the caller logs a warning.
    /// </summary>
    public static class MeadRecipes
    {
        public const int IngredientAmount = 2;

        private static readonly string[] Old =
        {
            null,
            "Honey:10,Thistle:5",
            "Honey:10,Thistle:5,Bloodbag:3",
            "Honey:10,Thistle:5,Bloodbag:3,YmirRemains:1",
        };

        public static string IngredientName(int tier) => $"VeilIngredient_T{tier}";

        public static string OldDefault(int tier) => Old[Check(tier)];

        public static string NewDefault(int tier) => $"{Old[Check(tier)]},{IngredientName(tier)}:{IngredientAmount}";

        public enum Outcome { Unchanged, Migrated, MissingIngredient, Malformed }

        /// <summary>The value to store and what happened. Whitespace around entries does not matter when comparing.</summary>
        public static (string value, Outcome outcome) Migrate(int tier, string stored)
        {
            if (Normalize(stored) == Normalize(OldDefault(tier))) return (NewDefault(tier), Outcome.Migrated);
            IReadOnlyList<(string item, int amount)> parsed;
            try { parsed = RecipeParser.Parse(stored); }
            catch (FormatException) { return (stored, Outcome.Malformed); }
            foreach (var (item, _) in parsed)
                if (string.Equals(item, IngredientName(tier), StringComparison.Ordinal)) return (stored, Outcome.Unchanged);
            return (stored, Outcome.MissingIngredient);
        }

        /// <summary>The recipe without any VeilIngredient_* entry (fallback when the bundle and therefore the ingredients are missing).</summary>
        public static IReadOnlyList<(string item, int amount)> WithoutIngredients(IEnumerable<(string item, int amount)> parsed)
        {
            var result = new List<(string, int)>();
            foreach (var e in parsed)
                if (!e.item.StartsWith("VeilIngredient_", StringComparison.Ordinal)) result.Add(e);
            return result;
        }

        private static string Normalize(string recipe) => (recipe ?? "").Replace(" ", "").Replace("\t", "");

        private static int Check(int tier)
        {
            if (tier < 1 || tier > 3) throw new ArgumentOutOfRangeException(nameof(tier));
            return tier;
        }
    }
}
