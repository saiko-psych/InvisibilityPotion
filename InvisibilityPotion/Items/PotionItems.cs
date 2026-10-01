using System.Collections.Generic;
using InvisibilityPotion.Config;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace InvisibilityPotion.Items
{
    /// <summary>Placeholder potions: cloned vanilla meads, tinted per tier. Replaced by custom bottles in plan 4.</summary>
    public static class PotionItems
    {
        private static readonly Color[] Tints = { Color.white, new Color(0.5f, 0.9f, 0.5f), new Color(0.5f, 0.6f, 1f), new Color(0.7f, 0.4f, 0.9f) };

        public static string BaseName(int tier) => $"MeadBaseInvisibility_T{tier}";
        public static string MeadName(int tier) => $"MeadInvisibility_T{tier}";

        /// <summary>Call from PrefabManager.OnVanillaPrefabsAvailable (unsubscribe after the first call).</summary>
        public static void Register()
        {
            for (var t = 1; t <= 3; t++)
            {
                var cfg = PluginConfig.Tier(t);
                var requirements = new List<RequirementConfig>();
                foreach (var (item, amount) in RecipeParser.Parse(cfg.Recipe))
                    requirements.Add(new RequirementConfig { Item = item, Amount = amount });

                var baseItem = new CustomItem(BaseName(t), "MeadBaseHealthMinor", new ItemConfig
                {
                    Name = $"$item_meadbaseinvisibility_t{t}",
                    Description = $"$item_meadbaseinvisibility_t{t}_description",
                    CraftingStation = "piece_cauldron",
                    MinStationLevel = 1,
                    Requirements = requirements.ToArray(),
                });
                Tint(baseItem.ItemPrefab, Tints[t]);
                ItemManager.Instance.AddItem(baseItem);

                var mead = new CustomItem(MeadName(t), "MeadHealthMinor", new ItemConfig
                {
                    Name = $"$item_meadinvisibility_t{t}",
                    Description = $"$item_meadinvisibility_t{t}_description",
                    Enabled = false,   // no crafting recipe; produced by the fermenter
                });
                var shared = mead.ItemDrop.m_itemData.m_shared;
                shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
                shared.m_consumeStatusEffect = ObjectDB.instance.GetStatusEffect(Effects.StatusEffects.NameHash(t));
                shared.m_food = 0f;
                shared.m_foodStamina = 0f;
                shared.m_foodRegen = 0f;
                Tint(mead.ItemPrefab, Tints[t]);
                ItemManager.Instance.AddItem(mead);

                ItemManager.Instance.AddItemConversion(new CustomItemConversion(new FermenterConversionConfig
                {
                    FromItem = BaseName(t),
                    ToItem = MeadName(t),
                    ProducedItems = 4,
                }));
            }
            // Icons: render the tinted bottles for the items and reuse them for the status effects.
            for (var t = 1; t <= 3; t++)
            {
                var prefab = PrefabManager.Instance.GetPrefab(MeadName(t));
                var sprite = RenderManager.Instance.Render(prefab, RenderManager.IsometricRotation);
                if (sprite == null) continue;
                prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons = new[] { sprite };
                var se = ObjectDB.instance.GetStatusEffect(Effects.StatusEffects.NameHash(t));
                if (se != null) se.m_icon = sprite;
                var basePrefab = PrefabManager.Instance.GetPrefab(BaseName(t));
                var baseSprite = RenderManager.Instance.Render(basePrefab, RenderManager.IsometricRotation);
                if (baseSprite != null) basePrefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons = new[] { baseSprite };
            }
        }

        private static void Tint(GameObject prefab, Color tint)
        {
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var copy = new Material(mats[i]);   // never tint the vanilla shared material
                    if (copy.HasProperty("_Color")) copy.color = tint;
                    mats[i] = copy;
                }
                r.sharedMaterials = mats;
            }
        }
    }
}
