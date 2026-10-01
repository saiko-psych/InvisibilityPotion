using System;
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

    /// <summary>
    /// SetChestEquipped writes the chest textures into m_bodyModel.material (VisEquipment.cs:1003-1047). While a swap mode holds the
    /// body, the original body materials are handed back for the duration of the call so the write is not lost (FogVeil.SuspendBody).
    /// Only when the hash changes; the method returns early otherwise (VisEquipment.cs:1005).
    /// </summary>
    [HarmonyPatch(typeof(VisEquipment), "SetChestEquipped")]
    internal static class SetChestEquippedPatch
    {
        [HarmonyPrefix]
        private static void Prefix(VisEquipment __instance, int hash, out bool __state) =>
            __state = __instance.m_currentChestItemHash != hash && BodyWrite.Suspend(__instance);

        [HarmonyPostfix]
        private static void Postfix(VisEquipment __instance, bool __state)
        {
            if (__state) BodyWrite.Resume(__instance);
        }
    }

    /// <summary>Same as <see cref="SetChestEquippedPatch"/> for the leg textures (VisEquipment.cs:1088-1128).</summary>
    [HarmonyPatch(typeof(VisEquipment), "SetLegEquipped")]
    internal static class SetLegEquippedPatch
    {
        [HarmonyPrefix]
        private static void Prefix(VisEquipment __instance, int hash, out bool __state) =>
            __state = __instance.m_currentLegItemHash != hash && BodyWrite.Suspend(__instance);

        [HarmonyPostfix]
        private static void Postfix(VisEquipment __instance, bool __state)
        {
            if (__state) BodyWrite.Resume(__instance);
        }
    }

    /// <summary>Exception guard: a veil failure must never break vanilla equipment updates.</summary>
    internal static class BodyWrite
    {
        public static bool Suspend(VisEquipment ve)
        {
            try { return VeilController.SuspendBody(ve); }
            catch (Exception e) { Plugin.Log.LogError($"veil: suspending the body swap failed: {e}"); return false; }
        }

        public static void Resume(VisEquipment ve)
        {
            try { VeilController.ResumeBody(ve); }
            catch (Exception e) { Plugin.Log.LogError($"veil: re-applying the body swap failed: {e}"); }
        }
    }
}
