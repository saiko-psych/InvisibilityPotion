using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

namespace InvisibilityPotion.Items
{
    /// <summary>
    /// The veil ingredients (bundle Ingredient_T1..3) as material items VeilIngredient_T1..3 for look checks: Huldra's Hair,
    /// Baldr's Tear, Hel's Ember Spore. Data template Thistle (a vanilla material pickup), stack 20, no recipe, icon rendered.
    /// Gathering from the plants and the recipe use are plan 5.
    /// </summary>
    public static class IngredientItems
    {
        private const string Template = "Thistle";
        public const int StackSize = 20;

        public static string ItemName(int tier) => $"VeilIngredient_T{tier}";

        public static void Register()
        {
            if (!AssetBundles.Loaded) { Plugin.Log.LogWarning("assets: no bundle; ingredients not registered"); return; }
            for (var t = 1; t <= 3; t++)
            {
                try { RegisterTier(t); }
                catch (System.Exception e) { Plugin.Log.LogError($"Registering ingredient T{t} failed: {e}"); }
            }
        }

        private static void RegisterTier(int t)
        {
            var go = ModelPrefabs.MakeItem($"Ingredient_T{t}", ItemName(t), Template);
            var shared = go.GetComponent<ItemDrop>().m_itemData.m_shared;
            shared.m_itemType = ItemDrop.ItemData.ItemType.Material;
            shared.m_maxStackSize = StackSize;
            shared.m_maxQuality = 1;
            AssetBundles.FixShaders(go, ItemName(t));
            ItemManager.Instance.AddItem(new CustomItem(go, true, new ItemConfig
            {
                Name = $"$item_veilingredient_t{t}",
                Description = $"$item_veilingredient_t{t}_description",
                Enabled = false,   // no recipe; ip_give ingredient <1|2|3> or ip_spawn VeilIngredient_TN
            }));
            var sprite = RenderManager.Instance.Render(go, RenderManager.IsometricRotation);
            if (sprite != null) shared.m_icons = new[] { sprite };
            Plugin.Log.LogInfo($"assets: ingredient {ItemName(t)} registered (material, stack {shared.m_maxStackSize}, weight {shared.m_weight}, " +
                               $"template {Template}, icon {(sprite != null ? "rendered" : "missing")})");
        }
    }
}
