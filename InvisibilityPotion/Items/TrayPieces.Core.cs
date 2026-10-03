using System.Collections.Generic;

namespace InvisibilityPotion.Items
{
    /// <summary>
    /// Pure rules for making our meads and bases placeable with the Serving Tray (0.3.1). Vanilla meads are their own piece: the
    /// item prefab carries ItemDrop + Piece + WearNTear and the tray's piece table lists the item prefab itself
    /// (docs/decompile-notes.md, "Serving tray").
    /// </summary>
    public static class TrayPieceRules
    {
        /// <summary>Vanilla item whose Piece and WearNTear are copied (the only vanilla mead whose components we have logged).</summary>
        public const string PieceTemplate = "MeadHealthMinor";

        /// <summary>Jötunn's PieceTables.ServingTray value; kept here so the order of the lookups is testable.</summary>
        public const string ServingTrayTable = "_FeasterPieceTable";

        /// <summary>
        /// Names to try with PieceManager.GetPieceTable, in order: the table name, then the tray item name (Jötunn maps item
        /// names to their table). After both fail the caller searches every table for the one that lists <see cref="PieceTemplate"/>.
        /// </summary>
        public static readonly string[] TableLookups = { ServingTrayTable, "ServingTray" };

        /// <summary>Our six placeable items: the three meads, then the three bases.</summary>
        public static List<string> ItemNames(System.Func<int, string> meadName, System.Func<int, string> baseName)
        {
            var list = new List<string>();
            for (var t = 1; t <= 3; t++) list.Add(meadName(t));
            for (var t = 1; t <= 3; t++) list.Add(baseName(t));
            return list;
        }

        /// <summary>
        /// Whether a component field is copied from the vanilla template: only what Unity would serialize (public and not
        /// [NonSerialized], or [SerializeField]), never statics, delegates (WearNTear.m_onDestroyed) or const/readonly fields.
        /// </summary>
        public static bool CopyField(bool isStatic, bool isInitOnly, bool isLiteral, bool isPublic, bool hasSerializeField, bool hasNonSerialized, bool isDelegate)
        {
            if (isStatic || isInitOnly || isLiteral || isDelegate || hasNonSerialized) return false;
            return isPublic || hasSerializeField;
        }
    }
}
