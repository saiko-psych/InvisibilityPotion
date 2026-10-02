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
    /// <summary>Debug-only asset inspection commands (plan 4): ip_components, ip_shaderdump, ip_bundle. Output goes to the console and Plugin.Log.</summary>
    internal static class AssetCommands
    {
        private const int MaxLines = 300;

        public static void Register()
        {
            CommandManager.Instance.AddConsoleCommand(new ComponentsCommand());
            CommandManager.Instance.AddConsoleCommand(new ShaderDumpCommand());
            CommandManager.Instance.AddConsoleCommand(new BundleCommand());
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
    }
}
#endif
