using System.Collections.Generic;
using System.Linq;
using InvisibilityPotion.Config;
using InvisibilityPotion.Goggles;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

namespace InvisibilityPotion.Items
{
    /// <summary>
    /// The veil goggles (bundle Goggles_T1..3) as helmet items: armour 2/4/6, recipes from [Goggles] RecipeT1..3 at the forge (I level 1, II level 3)
    /// and the black forge (III level 2), each upgrade consuming the previous goggles (plan 5 §3.5). No status effect: the level is read
    /// from the helmet slot by Goggles.GogglesLevel.
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
            LogMaterial("FlametalNew");   // S2: which bar is the Ashlands flametal
            LogMaterial("Flametal");
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
            var requirements = Requirements(t);
            ItemManager.Instance.AddItem(new CustomItem(go, true, new ItemConfig
            {
                Name = $"$item_veilgoggles_t{t}",
                Description = $"$item_veilgoggles_t{t}_description",
                Enabled = requirements != null,
                CraftingStation = GoggleLevel.Station(t),
                MinStationLevel = GoggleLevel.StationLevel(t),
                Requirements = requirements ?? new RequirementConfig[0],
            }));
            var sprite = RenderManager.Instance.Render(go, RenderManager.IsometricRotation);
            if (sprite != null) shared.m_icons = new[] { sprite };
            Plugin.Log.LogInfo($"assets: goggles {ItemName(t)} registered (helmet, armour {shared.m_armor}, icon {(sprite != null ? "rendered" : "missing")}, " +
                               $"recipe {(requirements == null ? "none" : string.Join(",", requirements.Select(r => $"{r.Item}:{r.Amount}")))} at {GoggleLevel.Station(t)} level {GoggleLevel.StationLevel(t)})");
        }

        /// <summary>
        /// [Goggles] RecipeTN parsed; a malformed text or an unknown item falls back to the default once; if the default also names an
        /// unknown item the goggles get no recipe (null) and an error is logged.
        /// </summary>
        private static RequirementConfig[] Requirements(int t)
        {
            var text = PluginConfig.GoggleRecipe(t);
            var parsed = TryParse(t, text, out var problem);
            if (parsed == null)
            {
                var fallback = GoggleLevel.DefaultRecipe(t);
                Plugin.Log.LogError($"[Goggles] RecipeT{t} '{text}': {problem}; using default '{fallback}'");
                parsed = TryParse(t, fallback, out problem);
                if (parsed == null)
                {
                    Plugin.Log.LogError($"goggles T{t}: default recipe '{fallback}' unusable ({problem}); registered without a recipe");
                    return null;
                }
            }
            return parsed.Select(e => new RequirementConfig(e.item, e.amount)).ToArray();
        }

        private static List<(string item, int amount)> TryParse(int t, string text, out string problem)
        {
            problem = null;
            List<(string item, int amount)> list;
            try { list = new List<(string, int)>(RecipeParser.Parse(text)); }
            catch (System.FormatException e) { problem = e.Message; return null; }
            if (list.Count == 0) { problem = "empty"; return null; }
            var missing = list.Where(e => !Exists(e.item)).Select(e => e.item).ToList();
            if (missing.Count > 0) { problem = $"unknown item(s) {string.Join(", ", missing)}"; return null; }
            return list;
        }

        private static bool Exists(string item) => PrefabManager.Instance.GetPrefab(item) != null || ItemManager.Instance.GetItem(item) != null;

        private static void LogMaterial(string prefab)
        {
            var go = PrefabManager.Instance.GetPrefab(prefab);
            var shared = go != null ? go.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
            Plugin.Log.LogInfo($"goggles: material {prefab}: {(shared == null ? "not found" : $"m_name {shared.m_name}, type {shared.m_itemType}")}");
        }
    }
}
