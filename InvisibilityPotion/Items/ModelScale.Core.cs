using System;

namespace InvisibilityPotion.Items
{
    /// <summary>
    /// Size factors of the bundle models (fix round 2, ruling 1: the first in-game round showed the bases far smaller than the
    /// vanilla mead bases and the bottles too small). One factor per prefab group, applied to the localScale of the group's
    /// scaled child: "attach" for items (VisEquipment.AttachItem and ItemStand.SetVisualItem keep the attach object's scale, so
    /// dropped, held and item-stand views all grow), "model" for plants (root -> model).
    /// </summary>
    public static class ModelScale
    {
        public const float Bottle = 2.0f;
        public const float Bowl = 3.0f;
        public const float Plant = 1.5f;
        /// <summary>The bark lichen patch (Plant_T1_*) is a flat 0.35 x 0.5 m mat and vanished in the grass at x1.5 (round J).</summary>
        public const float Lichen = 3.0f;
        public const float Goggles = 1f;
        /// <summary>Ingredient pickups (Ingredient_T1..3, items) grow like the plants they come from.</summary>
        public const float Ingredient = 4.5f;

        /// <summary>Factor for a bundle prefab name (MeadBottle_*, MeadBowl_*, Plant_*, Goggles_*, Ingredient_*); 1 for anything else.</summary>
        public static float For(string bundlePrefab)
        {
            var n = bundlePrefab ?? "";
            if (n.StartsWith("MeadBottle_", StringComparison.Ordinal)) return Bottle;
            if (n.StartsWith("MeadBowl_", StringComparison.Ordinal)) return Bowl;
            if (n.StartsWith("Plant_T1", StringComparison.Ordinal)) return Lichen;
            if (n.StartsWith("Plant_", StringComparison.Ordinal)) return Plant;
            if (n.StartsWith("Goggles_", StringComparison.Ordinal)) return Goggles;
            if (n.StartsWith("Ingredient_", StringComparison.Ordinal)) return Ingredient;
            return 1f;
        }

        /// <summary>Name of the direct child whose localScale carries the factor: "model" for plants, "attach" for items.</summary>
        public static string ScaledChild(string bundlePrefab) =>
            (bundlePrefab ?? "").StartsWith("Plant_", StringComparison.Ordinal) ? "model" : "attach";
    }
}
