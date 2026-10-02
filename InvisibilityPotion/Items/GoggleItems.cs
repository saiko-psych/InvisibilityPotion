using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

namespace InvisibilityPotion.Items
{
    /// <summary>
    /// The veil goggles (bundle Goggles_T1..3) as helmet items for look checks: armour 2/4/6, no recipe, no effect yet (plan 5).
    /// Data template HelmetLeather; set bonus, equip effect and hair hiding are cleared. Shown on the head through the prefab's
    /// direct child "attach" (VisEquipment.SetHelmetEquipped -> AttachItem on the helmet joint).
    /// </summary>
    public static class GoggleItems
    {
        private const string Template = "HelmetLeather";

        public static string ItemName(int tier) => $"VeilGoggles_T{tier}";

        public static void Register()
        {
            if (!AssetBundles.Loaded) { Plugin.Log.LogWarning("assets: no bundle; goggles not registered"); return; }
            for (var t = 1; t <= 3; t++)
            {
                try { RegisterTier(t); }
                catch (System.Exception e) { Plugin.Log.LogError($"Registering goggles T{t} failed: {e}"); }
            }
        }

        private static void RegisterTier(int t)
        {
            var go = ModelPrefabs.MakeItem($"Goggles_T{t}", ItemName(t), Template);
            var shared = go.GetComponent<ItemDrop>().m_itemData.m_shared;
            shared.m_itemType = ItemDrop.ItemData.ItemType.Helmet;
            shared.m_armor = 2f * t;
            shared.m_armorPerLevel = 0f;
            shared.m_maxQuality = 1;
            shared.m_setName = "";
            shared.m_setSize = 0;
            shared.m_setStatusEffect = null;
            shared.m_equipStatusEffect = null;
            shared.m_armorMaterial = null;
            shared.m_helmetHideHair = ItemDrop.ItemData.HelmetHairType.Default;
            shared.m_helmetHideBeard = ItemDrop.ItemData.HelmetHairType.Default;
            AssetBundles.FixShaders(go, ItemName(t));
            ItemManager.Instance.AddItem(new CustomItem(go, true, new ItemConfig
            {
                Name = $"$item_veilgoggles_t{t}",
                Description = $"$item_veilgoggles_t{t}_description",
                Enabled = false,   // no recipe yet; ip_give goggles <1|2|3>
            }));
            var sprite = RenderManager.Instance.Render(go, RenderManager.IsometricRotation);
            if (sprite != null) shared.m_icons = new[] { sprite };
            Plugin.Log.LogInfo($"assets: goggles {ItemName(t)} registered (helmet, armour {shared.m_armor}, icon {(sprite != null ? "rendered" : "missing")})");
        }
    }
}
