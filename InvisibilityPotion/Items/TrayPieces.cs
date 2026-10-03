using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jotunn.Managers;
using UnityEngine;

namespace InvisibilityPotion.Items
{
    /// <summary>
    /// Serving Tray support (0.3.1). Vanilla meads are their own build piece: the item prefab carries ItemDrop + Piece +
    /// WearNTear, the tray's piece table lists the item prefab, <c>Player.PlacePiece</c> instantiates it and calls
    /// <c>ItemDrop.MakePiece</c> (no rigidbody, ZDO flag <c>s_piece</c> restores it on load), "use" drinks it through
    /// <c>ItemDrop.Eat</c>, alt-use picks it up, and removing it with the tray drops <c>Piece.m_resources</c>
    /// (docs/decompile-notes.md, "Serving tray"). We do the same on our own item prefabs: the placed object is the item itself,
    /// so it shows our bottle/bowl at the dropped (world) size and pickup returns our item.
    /// Not a Jötunn CustomPiece: PieceManager.AddPiece registers the prefab a second time (PrefabManager.AddPrefab refuses the
    /// name our CustomItem already holds), so the prefab goes into the table through PieceManager.RegisterPieceInPieceTable.
    /// </summary>
    public static class TrayPieces
    {
        private static readonly List<GameObject> Prepared = new List<GameObject>();
        private static bool _subscribed;

        /// <summary>
        /// Adds Piece and WearNTear (copied from <see cref="TrayPieceRules.PieceTemplate"/>) to <paramref name="item"/> unless it
        /// has them (the bundle-less fallback clone of MeadHealthMinor does), and points name, icon and the single requirement at
        /// our item. Call after the item icon is rendered. Returns false (logged) when the template lacks the components.
        /// </summary>
        public static bool Prepare(GameObject item)
        {
            var drop = item != null ? item.GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                Plugin.Log.LogWarning($"tray: {item?.name ?? "null"} has no ItemDrop; not placeable");
                return false;
            }
            var template = ModelPrefabs.Template(TrayPieceRules.PieceTemplate);
            var tPiece = template.GetComponent<Piece>();
            var tWnt = template.GetComponent<WearNTear>();
            if (tPiece == null || tWnt == null)
            {
                Plugin.Log.LogWarning($"tray: template {template.name} has no Piece/WearNTear ({ModelPrefabs.ComponentList(template)}); {item.name} not placeable");
                return false;
            }

            var piece = item.GetComponent<Piece>();
            var pieceCopied = 0;
            if (piece == null)
            {
                piece = item.AddComponent<Piece>();
                pieceCopied = CopySerializedFields(tPiece, piece, template.transform);
            }
            var wnt = item.GetComponent<WearNTear>();
            var wntCopied = 0;
            if (wnt == null)
            {
                wnt = item.AddComponent<WearNTear>();
                wntCopied = CopySerializedFields(tWnt, wnt, template.transform);
            }

            var shared = drop.m_itemData.m_shared;
            piece.m_name = shared.m_name;
            piece.m_description = shared.m_description;
            if (shared.m_icons != null && shared.m_icons.Length > 0 && shared.m_icons[0] != null) piece.m_icon = shared.m_icons[0];
            piece.m_category = tPiece.m_category;
            var tReq = tPiece.m_resources != null && tPiece.m_resources.Length > 0 ? tPiece.m_resources[0] : null;
            piece.m_resources = new[]
            {
                new Piece.Requirement
                {
                    m_resItem = drop,
                    m_amount = 1,
                    m_amountPerLevel = tReq?.m_amountPerLevel ?? 1,
                    m_recover = tReq?.m_recover ?? true,
                },
            };
            if (!Prepared.Contains(item)) Prepared.Add(item);
            Plugin.Log.LogInfo($"tray: {item.name} prepared as piece '{piece.m_name}', category {piece.m_category}, usage {piece.m_usage}, " +
                               $"resources {drop.name} x1 recover {piece.m_resources[0].m_recover} (template {template.name}: " +
                               $"{string.Join("+", tPiece.m_resources?.Select(r => $"{(r.m_resItem != null ? r.m_resItem.name : "null")} x{r.m_amount} recover {r.m_recover}") ?? Enumerable.Empty<string>())}); " +
                               $"fields copied: Piece {pieceCopied}, WearNTear {wntCopied}; components {ModelPrefabs.ComponentList(item)}");
            return true;
        }

        /// <summary>Adds the prepared prefabs to the tray's table on every ObjectDB.Awake in the main scene (Jötunn's OnPiecesRegistered).</summary>
        public static void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            PieceManager.OnPiecesRegistered += RegisterInTray;
        }

        private static void RegisterInTray()
        {
            if (Prepared.Count == 0) return;
            try
            {
                var table = FindTable(out var how);
                if (table == null)
                {
                    Plugin.Log.LogWarning($"tray: Serving Tray piece table not found (tried {string.Join(", ", TrayPieceRules.TableLookups)} and a search for " +
                                          $"{TrayPieceRules.PieceTemplate}); veil meads and bases cannot be placed with the tray");
                    return;
                }
                var listed = 0;
                foreach (var prefab in Prepared)
                {
                    if (prefab == null) continue;
                    if (table.m_pieces.Contains(prefab)) { listed++; continue; }
                    try
                    {
                        PieceManager.Instance.RegisterPieceInPieceTable(prefab, table.name);
                        var piece = prefab.GetComponent<Piece>();
                        Plugin.Log.LogInfo($"tray: registered {prefab.name} ('{piece.m_name}', category {piece.m_category}) in {table.name} (found by {how})");
                    }
                    catch (Exception e) { Plugin.Log.LogWarning($"tray: adding {prefab.name} to {table.name} failed: {e.Message}"); }
                }
                if (listed > 0) Plugin.Log.LogInfo($"tray: {listed} piece(s) already listed in {table.name}");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"tray: registration failed, meads not placeable with the tray: {e}"); }
        }

        /// <summary>The Serving Tray's table: by Jötunn name, by tray item name, else the table that lists the vanilla mead template.</summary>
        public static PieceTable FindTable(out string how)
        {
            var template = PrefabManager.Instance.GetPrefab(TrayPieceRules.PieceTemplate);
            foreach (var name in TrayPieceRules.TableLookups)
            {
                var t = PieceManager.Instance.GetPieceTable(name);
                if (t == null) continue;
                how = name;
                if (template != null && !t.m_pieces.Contains(template))
                    Plugin.Log.LogWarning($"tray: table {t.name} (from '{name}') does not list {TrayPieceRules.PieceTemplate}; using it anyway");
                return t;
            }
            if (template != null)
            {
                foreach (var t in PieceManager.Instance.GetPieceTables())
                {
                    if (t == null || !t.m_pieces.Contains(template)) continue;
                    how = $"search for {TrayPieceRules.PieceTemplate}";
                    return t;
                }
            }
            how = null;
            return null;
        }

        /// <summary>
        /// Copies the fields Unity would serialize (<see cref="TrayPieceRules.CopyField"/>) from <paramref name="src"/> to
        /// <paramref name="dst"/>. References into the template's own hierarchy (WearNTear.m_new/m_worn/m_broken,
        /// m_fragmentRoots, Piece.m_comfortObject, ...) stay at their default, or our prefab would switch the vanilla mead's
        /// children; prefab and asset references (effect lists, sprites) are shared as in vanilla.
        /// </summary>
        private static int CopySerializedFields(Component src, Component dst, Transform templateRoot)
        {
            var copied = 0;
            var skipped = new List<string>();
            for (var type = src.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
            {
                foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!TrayPieceRules.CopyField(f.IsStatic, f.IsInitOnly, f.IsLiteral, f.IsPublic, f.IsDefined(typeof(SerializeField), false),
                                                  f.IsNotSerialized, typeof(Delegate).IsAssignableFrom(f.FieldType)))
                        continue;
                    var value = f.GetValue(src);
                    if (PointsInto(value, templateRoot)) { skipped.Add(f.Name); continue; }
                    f.SetValue(dst, value);
                    copied++;
                }
            }
            if (skipped.Count > 0)
                Plugin.Log.LogInfo($"tray: {src.GetType().Name} fields left at default (template children): {string.Join(", ", skipped)}");
            return copied;
        }

        private static bool PointsInto(object value, Transform root)
        {
            switch (value)
            {
                case null: return false;
                case GameObject go: return go != null && go.transform.IsChildOf(root);
                case Component c: return c != null && c.transform.IsChildOf(root);
                case string _: return false;
                case IEnumerable list:
                    foreach (var e in list)
                        if (PointsInto(e, root)) return true;
                    return false;
                default: return false;
            }
        }
    }
}
