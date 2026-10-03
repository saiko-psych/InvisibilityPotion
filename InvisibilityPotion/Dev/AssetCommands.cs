#if DEBUG
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace InvisibilityPotion.Dev
{
    /// <summary>Debug-only asset inspection commands (plan 4): ip_components, ip_shaderdump, ip_bundle, ip_tray. Output goes to the console and Plugin.Log.</summary>
    internal static class AssetCommands
    {
        private const int MaxLines = 300;

        public static void Register()
        {
            CommandManager.Instance.AddConsoleCommand(new ComponentsCommand());
            CommandManager.Instance.AddConsoleCommand(new ShaderDumpCommand());
            CommandManager.Instance.AddConsoleCommand(new BundleCommand());
            CommandManager.Instance.AddConsoleCommand(new TrayCommand());
        }

        /// <summary>A registered prefab (ZNetScene, then Jötunn's prefabs, then any loaded GameObject of that name).</summary>
        internal static GameObject FindPrefab(string name)
        {
            GameObject go = null;
            if (ZNetScene.instance != null) go = ZNetScene.instance.GetPrefab(name);
            if (go == null) go = PrefabManager.Instance.GetPrefab(name);
            if (go == null) go = PrefabManager.Cache.GetPrefab<GameObject>(name);
            return go;
        }

        private static List<string> PrefabNames() => ZNetScene.instance?.GetPrefabNames() ?? new List<string>();

        private class ComponentsCommand : ConsoleCommand
        {
            public override string Name => "ip_components";
            public override string Help => "ip_components <prefab>: components and the child tree (name, local position/rotation, layer, active, components) of a prefab";

            public override void Run(string[] args)
            {
                if (args.Length < 1) { DevCommands.Say(Help); return; }
                var go = FindPrefab(args[0]);
                if (go == null) { DevCommands.Say($"unknown prefab {args[0]}"); return; }
                var lines = new List<string>();
                Walk(go.transform, 0, lines);
                DevCommands.Say($"ip_components {go.name}: {lines.Count} object(s)");
                foreach (var l in lines.Take(MaxLines)) DevCommands.Say(l);
                if (lines.Count > MaxLines) DevCommands.Say($"... capped at {MaxLines}");
            }

            private static void Walk(Transform t, int depth, List<string> lines)
            {
                var comps = t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(Describe);
                lines.Add($"{new string(' ', depth * 2)}{t.name} pos {t.localPosition:F3} rot {t.localEulerAngles:F0} scale {t.localScale:F2} " +
                          $"layer {LayerMask.LayerToName(t.gameObject.layer)} active {t.gameObject.activeSelf}: {string.Join(", ", comps)}");
                foreach (Transform c in t) Walk(c, depth + 1, lines);
            }

            private static string Describe(Component c)
            {
                switch (c)
                {
                    case ZNetView nv: return $"ZNetView(persistent {nv.m_persistent}, type {nv.m_type}, distant {nv.m_distant})";
                    case Rigidbody rb: return $"Rigidbody(mass {rb.mass}, damping {rb.linearDamping}/{rb.angularDamping}, interp {rb.interpolation}, collision {rb.collisionDetectionMode})";
                    case BoxCollider b: return $"BoxCollider(center {b.center:F3}, size {b.size:F3}, trigger {b.isTrigger})";
                    case CapsuleCollider cc: return $"CapsuleCollider(r {cc.radius:F3}, h {cc.height:F3}, dir {cc.direction})";
                    case MeshCollider mc: return $"MeshCollider(convex {mc.convex}, mesh {(mc.sharedMesh != null ? mc.sharedMesh.name : "-")})";
                    case MeshFilter mf: return $"MeshFilter({(mf.sharedMesh != null ? $"{mf.sharedMesh.name}, {mf.sharedMesh.vertexCount} verts" : "-")})";
                    case Renderer r: return $"{r.GetType().Name}(enabled {r.enabled}, materials {string.Join("/", r.sharedMaterials.Select(m => m == null ? "null" : $"{m.name}[{m.shader?.name}]"))})";
                    case ItemDrop d:
                        var s = d.m_itemData?.m_shared;
                        return s == null ? "ItemDrop(no shared)" : $"ItemDrop({s.m_name}, type {s.m_itemType}, drink {s.m_isDrink}, armor {s.m_armor}, weight {s.m_weight}, stack {s.m_maxStackSize})";
                    default: return c.GetType().Name;
                }
            }

            public override List<string> CommandOptionList() => PrefabNames();
        }

        private class ShaderDumpCommand : ConsoleCommand
        {
            public override string Name => "ip_shaderdump";
            public override string Help => "ip_shaderdump <prefab>: every renderer's materials with shader, render queue, keywords and texture properties (also Jötunn's ShaderHelper.ShaderDump at debug level)";

            public override void Run(string[] args)
            {
                if (args.Length < 1) { DevCommands.Say(Help); return; }
                var go = FindPrefab(args[0]);
                if (go == null) { DevCommands.Say($"unknown prefab {args[0]}"); return; }
                DevCommands.Say($"ip_shaderdump {go.name}");
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) { DevCommands.Say($"  {r.name}: null material"); continue; }
                    var sh = m.shader;
                    var tex = new StringBuilder();
                    foreach (var id in m.GetTexturePropertyNameIDs())
                    {
                        var t = m.GetTexture(id);
                        if (t != null) tex.Append($" {t.name}");
                    }
                    DevCommands.Say($"  {r.name} ({r.GetType().Name}, enabled {r.enabled}): {m.name} shader {(sh == null ? "null" : sh.name)} supported {(sh != null && sh.isSupported)} " +
                                    $"queue {m.renderQueue} keywords [{string.Join(" ", m.shaderKeywords)}] color {(m.HasProperty("_Color") ? m.color.ToString() : "-")} textures [{tex.ToString().Trim()}]");
                }
                ShaderHelper.ShaderDump(go);
            }

            public override List<string> CommandOptionList() => PrefabNames();
        }

        private class BundleCommand : ConsoleCommand
        {
            public override string Name => "ip_bundle";
            public override string Help => "ip_bundle: assets of the ip_assets bundle and each material's current shader (JVLmock_ = not resolved)";

            public override void Run(string[] args)
            {
                foreach (var line in Items.AssetBundles.Describe()) DevCommands.Say(line);
            }
        }

        private class TrayCommand : ConsoleCommand
        {
            public override string Name => "ip_tray";
            public override string Help => "ip_tray [all]: the Serving Tray's piece table (owning item, flags, categories) and its mead entries (prefab, components, " +
                                           "piece name/category/usage, resources); 'all' lists every entry";

            public override void Run(string[] args)
            {
                var all = args.Length > 0 && args[0] == "all";
                var table = Items.TrayPieces.FindTable(out var how);
                if (table == null) { DevCommands.Say("ip_tray: Serving Tray piece table not found (no world loaded?)"); return; }
                var owners = ObjectDB.instance == null ? new List<string>() : ObjectDB.instance.m_items
                    .Where(i => i != null && i.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces == table).Select(i => i.name).ToList();
                DevCommands.Say($"ip_tray: table {table.name} (found by {how}), item(s) [{string.Join(", ", owners)}], {table.m_pieces.Count} entries, " +
                                $"canRemovePieces {table.m_canRemovePieces}, canRemoveFeasts {table.m_canRemoveFeasts}, hideAdvancedMenu {table.m_hideAdvancedMenu}, skill {table.m_skill}, " +
                                $"categories [{string.Join(", ", table.m_categories)}], labels [{string.Join(", ", table.m_categoryLabels)}]");
                foreach (var go in table.m_pieces)
                {
                    if (go == null) { DevCommands.Say("  null entry"); continue; }
                    var p = go.GetComponent<Piece>();
                    var isMead = p != null && p.m_category == Piece.PieceCategory.Meads;
                    if (!all && !isMead && !go.name.Contains("Mead")) continue;
                    var res = p?.m_resources == null ? "-" : string.Join(" + ", p.m_resources.Select(r => $"{(r.m_resItem != null ? r.m_resItem.name : "null")} x{r.m_amount} recover {r.m_recover}"));
                    var drop = go.GetComponent<ItemDrop>();
                    var dropInfo = drop == null ? "no ItemDrop" : $"ItemDrop({drop.m_itemData.m_shared.m_name}, type {drop.m_itemData.m_shared.m_itemType}, drink {drop.m_itemData.m_shared.m_isDrink}, " +
                                                                   $"enableObj {(drop.m_pieceEnableObj != null ? drop.m_pieceEnableObj.name : "-")}, disabledObj {(drop.m_pieceDisabledObj != null ? drop.m_pieceDisabledObj.name : "-")})";
                    var wnt = go.GetComponent<WearNTear>();
                    DevCommands.Say($"  {go.name} layer {LayerMask.LayerToName(go.layer)}: [{Items.ModelPrefabs.ComponentList(go)}] piece '{p?.m_name}' category {p?.m_category} usage {p?.m_usage} " +
                                    $"canBeRemoved {p?.m_canBeRemoved} groundPiece {p?.m_groundPiece} | resources {res} | {dropInfo} | " +
                                    $"WearNTear {(wnt == null ? "-" : $"health {wnt.m_health} material {wnt.m_materialType} supports {wnt.m_supports} fragments {wnt.m_autoCreateFragments} new {(wnt.m_new != null ? wnt.m_new.name : "-")}")}");
                }
            }

            public override List<string> CommandOptionList() => new List<string> { "all" };
        }
    }
}
#endif
