using Jotunn.Managers;

namespace InvisibilityPotion.Items
{
    /// <summary>Everything from the bundle besides the meads: goggles, ingredients, plants; then frees the bundle file and arms the shader check.</summary>
    public static class ModelItems
    {
        /// <summary>Call from PrefabManager.OnVanillaPrefabsAvailable after PotionItems.Register.</summary>
        public static void Register()
        {
            try { GoggleItems.Register(); }
            catch (System.Exception e) { Plugin.Log.LogError($"Goggle registration failed: {e}"); }
            try { IngredientItems.Register(); }
            catch (System.Exception e) { Plugin.Log.LogError($"Ingredient registration failed: {e}"); }
            try { ModelPrefabs.RegisterPlants(); }
            catch (System.Exception e) { Plugin.Log.LogError($"Plant registration failed: {e}"); }
            try { Plants.PlantPrefabs.Register(); }   // plan 5: needs the scaled plant models, before Unload
            catch (System.Exception e) { Plugin.Log.LogError($"Hidden plant registration failed: {e}"); }
            AssetBundles.Unload();
            // After Jötunn has put the custom prefabs into ZNetScene (and fixed their mock references): log and resolve what is left.
            PrefabManager.OnPrefabsRegistered += () => AssetBundles.FixAllShaders("OnPrefabsRegistered");
        }
    }
}
