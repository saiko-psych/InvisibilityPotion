using System;
using HarmonyLib;
using InvisibilityPotion.Plants;

namespace InvisibilityPotion.Patches
{
    /// <summary>
    /// Round N ruling 2: the Huldra's Hair sapling ghost is valid only next to an eligible fir/pine trunk (TreeSapling.Find).
    /// Runs after vanilla decided the status (Player.cs:3782-4102) and only touches a Valid status of our ghost; SetPlacementGhostValid
    /// already ran, so the red highlight is set here directly (Piece.SetInvalidPlacementHeightlight, Piece.cs:462). flashGuardStone
    /// is true only for the call from TryPlacePiece (Player.cs:3032; the per-frame call at :1662 passes false), so the message is
    /// queued exactly when the player tries to place. A valid ghost is moved onto the trunk surface (TreeSapling.SnapGhost); TryPlacePiece
    /// places at the ghost's transform (Player.cs:3082), so the placed sapling carries the snapped position to TreeSapling.Start.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacementGhost))]
    internal static class TreeSaplingPlacementPatch
    {
        private static bool _loggedError;

        [HarmonyPostfix]
        private static void Postfix(Player __instance, bool flashGuardStone)
        {
            try
            {
                var ghost = __instance.m_placementGhost;
                if (ghost == null || !ghost.activeSelf || __instance.m_placementStatus != Player.PlacementStatus.Valid) return;
                if (ghost.GetComponent<TreeSapling>() == null) return;
                var verdict = TreeSapling.Find(ghost.transform.position, out var tree);
                if (verdict == TreeSaplingRule.Verdict.Ok)
                {
                    TreeSapling.SnapGhost(ghost, tree);   // after vanilla's placement: the ghost sticks to the bark
                    return;
                }
                __instance.m_placementStatus = Player.PlacementStatus.Invalid;
                ghost.GetComponent<Piece>()?.SetInvalidPlacementHeightlight(true);
                if (flashGuardStone) TreeSapling.QueueMessage(verdict);
            }
            catch (Exception e)
            {
                if (_loggedError) return;
                _loggedError = true;
                Plugin.Log.LogError($"cultivation: placement check failed: {e}");
            }
        }
    }
}
