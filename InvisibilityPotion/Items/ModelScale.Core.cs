using System;

namespace InvisibilityPotion.Items
{
    /// <summary>
    /// Size factors of the bundle models, two per group (round L, ruling 3): <see cref="WorldFactor"/> is the size lying on the
    /// ground (dropped item) or standing in the world (plants), <see cref="AttachFactor"/> the size worn, held or hung on an item
    /// stand. Both act on the localScale of the group's scaled child ("attach" for items, "model" for plants):
    /// <list type="bullet">
    /// <item>VisEquipment.AttachItem, ItemStand.SetVisualItem and ArmorStand instantiate only the direct child "attach" and keep
    /// its localScale, so the worn / stand size is the attach scale (the root is never cloned).</item>
    /// <item>A dropped item is the whole prefab, but ItemDrop.SetQuality (from Awake, Load and DropItem) overwrites the root's
    /// localScale with ItemData.GetScale() (1 at quality 1), so a root factor would be lost. The prefab therefore carries the
    /// attach factor, and a small component on the root multiplies its own instance's attach by <see cref="DroppedRatio"/> in
    /// Awake (only world instances run Awake; the attach clones do not carry the root component).</item>
    /// </list>
    /// History: fix round 2 bottles 1.6 / bowls 2.4 / plants 1.5; round K bottles 2.0, bowls 3.0, lichen 3.0, ingredients 4.5.
    /// </summary>
    public static class ModelScale
    {
        public const float BottleWorld = 2.4f;
        public const float BottleAttach = 2.0f;
        public const float Bowl = 3.0f;
        /// <summary>Plants Plant_T2* / Plant_T3* (props, never items).</summary>
        public const float Plant = 2.2f;
        /// <summary>The bark lichen patch (Plant_T1_*): a flat mat that vanishes in the grass when small.</summary>
        public const float Lichen = 4.5f;
        public const float GogglesWorld = 2.2f;
        public const float GogglesAttach = 1f;
        /// <summary>Ingredient pickups (never worn; the attach size only matters on an item stand).</summary>
        public const float Ingredient = 8f;

        /// <summary>Size in the world: dropped item on the ground, or the plant prop. 1 for unknown names.</summary>
        public static float WorldFactor(string bundlePrefab)
        {
            switch (Group(bundlePrefab))
            {
                case "bottle": return BottleWorld;
                case "bowl": return Bowl;
                case "lichen": return Lichen;
                case "plant": return Plant;
                case "goggles": return GogglesWorld;
                case "ingredient": return Ingredient;
                default: return 1f;
            }
        }

        /// <summary>Size worn / held / on an item stand (the prefab's own scaled-child factor). Plants: same as the world factor.</summary>
        public static float AttachFactor(string bundlePrefab)
        {
            switch (Group(bundlePrefab))
            {
                case "bottle": return BottleAttach;
                case "goggles": return GogglesAttach;
                default: return WorldFactor(bundlePrefab);
            }
        }

        /// <summary>World / attach: what a dropped instance multiplies its attach by (1 = nothing to do).</summary>
        public static float DroppedRatio(string bundlePrefab) => WorldFactor(bundlePrefab) / AttachFactor(bundlePrefab);

        /// <summary>Name of the direct child whose localScale carries the factor: "model" for plants, "attach" for items.</summary>
        public static string ScaledChild(string bundlePrefab) =>
            (bundlePrefab ?? "").StartsWith("Plant_", StringComparison.Ordinal) ? "model" : "attach";

        private static string Group(string bundlePrefab)
        {
            var n = bundlePrefab ?? "";
            if (n.StartsWith("MeadBottle_", StringComparison.Ordinal)) return "bottle";
            if (n.StartsWith("MeadBowl_", StringComparison.Ordinal)) return "bowl";
            if (n.StartsWith("Plant_T1", StringComparison.Ordinal)) return "lichen";
            if (n.StartsWith("Plant_", StringComparison.Ordinal)) return "plant";
            if (n.StartsWith("Goggles_", StringComparison.Ordinal)) return "goggles";
            if (n.StartsWith("Ingredient_", StringComparison.Ordinal)) return "ingredient";
            return "";
        }
    }
}
