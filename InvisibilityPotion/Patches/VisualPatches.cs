using HarmonyLib;
using InvisibilityPotion.Visuals;

namespace InvisibilityPotion.Patches
{
    /// <summary>UpdateLodgroup runs only when equipment actually changed (VisEquipment.cs:831); new renderers need the veil again.</summary>
    [HarmonyPatch(typeof(VisEquipment), "UpdateLodgroup")]
    internal static class UpdateLodgroupPatch
    {
        [HarmonyPostfix]
        private static void Postfix(VisEquipment __instance)
        {
            var p = __instance.GetComponent<Player>();
            if (p != null) VeilController.ForceRefresh(p);
        }
    }
}
