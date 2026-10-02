using System.Collections.Generic;
using InvisibilityPotion.Config;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace InvisibilityPotion.Items
{
    /// <summary>
    /// The veil meads (bundle prefabs MeadBottle_T1..3 with a code-built mist) and their bases (MeadBowl_T1..3), built by
    /// <see cref="ModelPrefabs.MakeItem"/> with the vanilla MeadHealthMinor / MeadBaseHealthMinor as data template. Without the
    /// bundle they fall back to plain clones of those vanilla items, so saved items never vanish.
    /// </summary>
    public static class PotionItems
    {
        private const string MeadTemplate = "MeadHealthMinor";
        private const string BaseTemplate = "MeadBaseHealthMinor";

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
            // Icons: render the bottles for the items and reuse them for the status effects.
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

            var baseItem = NewItem(BaseName(t), $"MeadBowl_T{t}", BaseTemplate, t, mist: false, new ItemConfig
            {
                Name = $"$item_meadbaseinvisibility_t{t}",
                Description = $"$item_meadbaseinvisibility_t{t}_description",
                CraftingStation = CraftingStations.MeadKetill,   // "piece_MeadCauldron", the Mead Ketill (piece_cauldron is the cooking cauldron)
                MinStationLevel = 1,
                Requirements = requirements.ToArray(),
            });
            ItemManager.Instance.AddItem(baseItem);

            var mead = NewItem(MeadName(t), $"MeadBottle_T{t}", MeadTemplate, t, mist: true, new ItemConfig
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
            ItemManager.Instance.AddItem(mead);

            ItemManager.Instance.AddItemConversion(new CustomItemConversion(new FermenterConversionConfig
            {
                FromItem = BaseName(t),
                ToItem = MeadName(t),
                ProducedItems = 4,
            }));
        }

        /// <summary>
        /// Vanilla meads get their drink sound and particles from the consume status effect: StatusEffect.m_startEffects is created in
        /// Setup (TriggerStartEffects, StatusEffect.cs:128) and m_stopEffects in Stop (StatusEffect.cs:159). Ours start empty, so share the
        /// EffectLists of a vanilla mead's effect (read-only use, no copy needed). The mead is [Veil] PotionVfxSource (default
        /// MeadFrostResist, a bluish-white burst instead of the red health one), else MeadFrostResist, MeadTasty, MeadHealthMinor.
        /// Not applied to SE_Revealed.
        /// </summary>
        private static void CopyConsumeEffects()
        {
            StatusEffect source = null;
            var configured = PluginConfig.PotionVfxSource;
            foreach (var name in PotionVfx.Candidates(configured))
            {
                var prefab = PrefabManager.Instance.GetPrefab(name);
                source = prefab != null ? prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_consumeStatusEffect : null;
                if (source != null)
                {
                    if (!string.Equals(name, configured?.Trim(), System.StringComparison.OrdinalIgnoreCase))
                        Plugin.Log.LogWarning($"potion effects: PotionVfxSource '{configured}' has no consume status effect; using {name}");
                    break;
                }
            }
            if (source == null)
            {
                Plugin.Log.LogWarning($"potion effects: none of {string.Join(", ", PotionVfx.Candidates(configured))} has a consume status effect; potions keep silent start/stop");
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

        /// <summary>Item from the bundle model (mist on finished meads, shaders resolved before the icon render); vanilla clone as fallback.</summary>
        private static CustomItem NewItem(string name, string bundlePrefab, string template, int tier, bool mist, ItemConfig config)
        {
            if (AssetBundles.Loaded)
            {
                try
                {
                    var go = ModelPrefabs.MakeItem(bundlePrefab, name, template);
                    if (mist) ModelPrefabs.AddMist(go, tier);
                    // Icons are rendered right after registration, before Jötunn's own shader fix runs (ObjectDB registration).
                    AssetBundles.FixShaders(go, name);
                    return new CustomItem(go, true, config);
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogError($"{name}: building from {bundlePrefab} failed, falling back to a clone of {template}: {e}");
                }
            }
            return new CustomItem(name, template, config);
        }
    }
}
