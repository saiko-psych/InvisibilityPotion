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
        private static readonly string[] DefaultRecipes =
        {
            "Honey:10,Thistle:5",
            "Honey:10,Thistle:5,Bloodbag:3",
            "Honey:10,Thistle:5,Bloodbag:3,YmirRemains:1",
        };

        public static void Register()
        {
            for (var t = 1; t <= 3; t++)
            {
                try { RegisterTier(t); }
                catch (System.Exception e) { Plugin.Log.LogError($"Registering tier {t} potions failed: {e}"); }
            }
            try { CopyConsumeEffects(); }
            catch (System.Exception e) { Plugin.Log.LogError($"Copying potion start/stop effects failed: {e}"); }
            // Icons: render the tinted bottles for the items and reuse them for the status effects.
            for (var t = 1; t <= 3; t++)
            {
                try
                {
                    var prefab = PrefabManager.Instance.GetPrefab(MeadName(t));
                    var basePrefab = PrefabManager.Instance.GetPrefab(BaseName(t));
                    if (prefab == null || basePrefab == null)
                    {
                        Plugin.Log.LogWarning($"Tier {t} potion prefabs missing; skipping icons");
                        continue;
                    }
                    var sprite = RenderManager.Instance.Render(prefab, RenderManager.IsometricRotation);
                    if (sprite != null)
                    {
                        prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons = new[] { sprite };
                        // Same instance Jotunn puts into the ObjectDB and the mead's m_consumeStatusEffect.
                        var se = Effects.StatusEffects.Prefab(t);
                        if (se != null) se.m_icon = sprite;
                    }
                    var baseSprite = RenderManager.Instance.Render(basePrefab, RenderManager.IsometricRotation);
                    if (baseSprite != null) basePrefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons = new[] { baseSprite };
                }
                catch (System.Exception e) { Plugin.Log.LogError($"Icons for tier {t} failed: {e}"); }
            }
        }

        private static void RegisterTier(int t)
        {
            var cfg = PluginConfig.Tier(t);
            List<(string, int)> parsed;
            try
            {
                parsed = new List<(string, int)>(RecipeParser.Parse(cfg.Recipe));
            }
            catch (System.FormatException e)
            {
                Plugin.Log.LogError($"Tier {t} recipe '{cfg.Recipe}' is malformed ({e.Message}); using default '{DefaultRecipes[t - 1]}'");
                parsed = new List<(string, int)>(RecipeParser.Parse(DefaultRecipes[t - 1]));
            }
            var requirements = new List<RequirementConfig>();
            foreach (var (item, amount) in parsed)
                requirements.Add(new RequirementConfig { Item = item, Amount = amount });

            var baseItem = new CustomItem(BaseName(t), "MeadBaseHealthMinor", new ItemConfig
            {
                Name = $"$item_meadbaseinvisibility_t{t}",
                Description = $"$item_meadbaseinvisibility_t{t}_description",
                CraftingStation = CraftingStations.MeadKetill,   // "piece_MeadCauldron", the Mead Ketill (piece_cauldron is the cooking cauldron)
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
            shared.m_isDrink = true;   // only selects "$item_drink" over "$item_eat" in ItemDrop.GetHoverText (ItemDrop.cs:1496)
            // The registered prefab instance itself: at OnVanillaPrefabsAvailable the ObjectDB does not contain custom status effects yet.
            var effect = Effects.StatusEffects.Prefab(t);
            if (effect == null)
                Plugin.Log.LogError($"status effect SE_Invisibility_T{t} was not registered; mead registered without consume effect");
            shared.m_consumeStatusEffect = effect;
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

        private const string EffectSourceMead = "MeadHealthMinor";

        /// <summary>
        /// Vanilla meads get their drink sound and particles from the consume status effect: StatusEffect.m_startEffects is created in
        /// Setup (TriggerStartEffects, StatusEffect.cs:128) and m_stopEffects in Stop (StatusEffect.cs:159). Ours start empty, so share the
        /// EffectLists of MeadHealthMinor's effect (read-only use, no copy needed). Not applied to SE_Revealed.
        /// </summary>
        private static void CopyConsumeEffects()
        {
            var prefab = PrefabManager.Instance.GetPrefab(EffectSourceMead);
            var source = prefab != null ? prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_consumeStatusEffect : null;
            if (source == null)
            {
                Plugin.Log.LogWarning($"{EffectSourceMead} or its consume status effect not found; potions keep silent start/stop");
                return;
            }
            var copied = 0;
            for (var t = 1; t <= 3; t++)
            {
                var se = Effects.StatusEffects.Prefab(t);
                if (se == null) continue;
                se.m_startEffects = source.m_startEffects;
                se.m_stopEffects = source.m_stopEffects;
                copied++;
            }
            Plugin.Log.LogInfo($"potion effects: copied start ({source.m_startEffects?.m_effectPrefabs?.Length ?? 0}) and stop ({source.m_stopEffects?.m_effectPrefabs?.Length ?? 0}) " +
                               $"effects from {source.name} to {copied} tier effects");
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
