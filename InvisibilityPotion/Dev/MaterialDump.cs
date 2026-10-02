#if DEBUG
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.Rendering;

namespace InvisibilityPotion.Dev
{
    /// <summary>
    /// Debug-only `ip_matdump` (items round L): every material of a prefab with its shader and every shader property's current
    /// value (floats, ranges, colours, vectors, textures), keywords and render queue. Purpose: read the wind / sway properties of
    /// vanilla vegetation (e.g. `ip_matdump Bush01`, `shrub_2`, `Pickable_Thistle`; `ip_matdump clutter [substring]` for the grass
    /// clutter of ClutterSystem, which is not a ZNetScene prefab). Output goes to the console and Plugin.Log.
    /// </summary>
    internal static class MaterialDump
    {
        private const int MaxMaterials = 40;

        public static void Register() => CommandManager.Instance.AddConsoleCommand(new MatDumpCommand());

        private class MatDumpCommand : ConsoleCommand
        {
            public override string Name => "ip_matdump";
            public override string Help => "ip_matdump <prefab> | ip_matdump clutter [substring]: every material's shader properties (values), keywords and queue";

            public override void Run(string[] args)
            {
                if (args.Length < 1) { DevCommands.Say(Help); return; }
                if (args[0] == "clutter") { DumpClutter(args.Length > 1 ? args[1] : ""); return; }
                var go = AssetCommands.FindPrefab(args[0]);
                if (go == null) { DevCommands.Say($"ip_matdump: unknown prefab {args[0]}"); return; }
                Dump(go.name, Materials(go));
            }

            public override List<string> CommandOptionList() => ZNetScene.instance?.GetPrefabNames() ?? new List<string>();
        }

        /// <summary>(path, material) for every renderer and InstanceRenderer under the prefab, inactive ones included.</summary>
        private static List<(string where, Material mat)> Materials(GameObject go)
        {
            var list = new List<(string, Material)>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    list.Add(($"{Path(go.transform, r.transform)} [{r.GetType().Name}]", m));
            foreach (var ir in go.GetComponentsInChildren<InstanceRenderer>(true))
                list.Add(($"{Path(go.transform, ir.transform)} [InstanceRenderer]", ir.m_material));
            return list;
        }

        private static void DumpClutter(string filter)
        {
            var cs = ClutterSystem.instance;
            if (cs == null) { DevCommands.Say("ip_matdump clutter: no ClutterSystem (in a world only)"); return; }
            var n = 0;
            foreach (var c in cs.m_clutter)
            {
                if (c?.m_prefab == null) continue;
                if (filter.Length > 0 && c.m_name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    c.m_prefab.name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                Dump($"clutter {c.m_name} ({c.m_prefab.name}, enabled {c.m_enabled}, instanced {c.m_instanced})", Materials(c.m_prefab));
                n++;
            }
            DevCommands.Say($"ip_matdump clutter: {n} clutter entr{(n == 1 ? "y" : "ies")} of {cs.m_clutter.Count}{(filter.Length > 0 ? $" matching '{filter}'" : "")}");
        }

        private static void Dump(string title, List<(string where, Material mat)> mats)
        {
            DevCommands.Say($"ip_matdump {title}: {mats.Count} material slot(s)");
            var seen = new HashSet<Material>();
            foreach (var (where, mat) in mats.Take(MaxMaterials))
            {
                if (mat == null) { DevCommands.Say($"  {where}: null material"); continue; }
                if (!seen.Add(mat)) { DevCommands.Say($"  {where}: {mat.name} (dumped above)"); continue; }
                var sh = mat.shader;
                DevCommands.Say($"  {where}: material {mat.name}, shader {(sh != null ? sh.name : "null")}, queue {mat.renderQueue}, " +
                                $"keywords [{string.Join(" ", mat.shaderKeywords)}]");
                if (sh == null) continue;
                for (var i = 0; i < sh.GetPropertyCount(); i++)
                {
                    var name = sh.GetPropertyName(i);
                    DevCommands.Say($"    {name} ({sh.GetPropertyType(i)}) = {Value(mat, name, sh.GetPropertyType(i))}");
                }
            }
            if (mats.Count > MaxMaterials) DevCommands.Say($"  ... capped at {MaxMaterials} material slots");
            DevCommands.Say($"ip_matdump {title}: end");
        }

        private static string Value(Material m, string name, ShaderPropertyType type)
        {
            if (!m.HasProperty(name)) return "(not on material)";
            switch (type)
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    return m.GetFloat(name).ToString("0.#####", CultureInfo.InvariantCulture);
                case ShaderPropertyType.Int:
                    return m.GetInteger(name).ToString(CultureInfo.InvariantCulture);
                case ShaderPropertyType.Color:
                    var c = m.GetColor(name);
                    return string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###}, {3:0.###})", c.r, c.g, c.b, c.a);
                case ShaderPropertyType.Vector:
                    var v = m.GetVector(name);
                    return string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###}, {3:0.###})", v.x, v.y, v.z, v.w);
                case ShaderPropertyType.Texture:
                    var t = m.GetTexture(name);
                    return t == null ? "none" : $"{t.name} {t.width}x{t.height} scale {m.GetTextureScale(name)} offset {m.GetTextureOffset(name)}";
                default:
                    return "?";
            }
        }

        private static string Path(Transform root, Transform t)
        {
            var parts = new List<string>();
            for (var c = t; c != null && c != root; c = c.parent) parts.Add(c.name);
            parts.Add(root.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
#endif
