using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace InvisibilityPotion.Items
{
    /// <summary>
    /// The Unity asset bundle <c>ip_assets</c> embedded in the DLL (docs/assets.md). Loaded once in Plugin.Awake; every prefab is
    /// loaded eagerly and kept, so <see cref="Unload"/> (after registration) frees only the bundle file. Materials use JVLmock_
    /// stump shaders that Jötunn swaps for the vanilla shader (fixReference); <see cref="FixShaders"/> is the safety net.
    /// </summary>
    public static class AssetBundles
    {
        public const string BundleName = "ip_assets";
        private const string MockPrefix = "JVLmock_";
        private const string FallbackShader = "Custom/Creature";

        private static AssetBundle _bundle;
        private static readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        /// <summary>Every GameObject built from the bundle (bundle prefabs and the registered clones), for ip_bundle and the shader net.</summary>
        private static readonly List<GameObject> _tracked = new List<GameObject>();

        public static string ResourceName { get; private set; }
        public static IReadOnlyList<string> AssetNames { get; private set; } = Array.Empty<string>();
        public static IEnumerable<GameObject> Prefabs => _prefabs.Values;
        public static IReadOnlyList<GameObject> Tracked => _tracked;

        /// <summary>Windows players load the Windows build of the bundle (d3d11 stump shaders), everything else the Linux one.</summary>
        public static void Load()
        {
            var asm = typeof(AssetBundles).Assembly;
#if DEBUG
            Plugin.Log.LogInfo($"assets: manifest resources: {string.Join(", ", asm.GetManifestResourceNames())}");
#endif
            ResourceName = (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsServer) ? BundleName + ".windows" : BundleName;
            _bundle = LoadFromEmbedded(asm, ResourceName);
            if (_bundle == null)
            {
                Plugin.Log.LogError($"assets: bundle '{ResourceName}' not found or not loadable; items fall back to vanilla clones");
                return;
            }
            AssetNames = _bundle.GetAllAssetNames().OrderBy(n => n, StringComparer.Ordinal).ToArray();
            foreach (var go in _bundle.LoadAllAssets<GameObject>())
            {
                _prefabs[go.name] = go;
                _tracked.Add(go);
            }
            var materials = _bundle.LoadAllAssets<Material>();
            Plugin.Log.LogInfo($"assets: loaded '{ResourceName}' ({AssetNames.Count} assets, {_prefabs.Count} prefabs, {materials.Length} materials)");
            foreach (var name in AssetNames) Plugin.Log.LogInfo($"assets:   {name}");
        }

        /// <summary>
        /// Loads the embedded bundle from a byte copy. Jötunn's AssetUtils.LoadAssetBundleFromResources (2.30.0 and 2.30.2) uses
        /// AssetBundle.LoadFromStream inside a using block: the stream is disposed right after the header is read, and the later
        /// asset reads fail with "ManagedStream object must be readable" plus mismatched-serialization errors (round I crash).
        /// </summary>
        private static AssetBundle LoadFromEmbedded(System.Reflection.Assembly asm, string bundleName)
        {
            string resource = null;
            foreach (var n in asm.GetManifestResourceNames())
                if (n.EndsWith("." + bundleName, StringComparison.Ordinal)) { resource = n; break; }
            if (resource == null) return null;
            using (var stream = asm.GetManifestResourceStream(resource))
            {
                if (stream == null) return null;
                var bytes = new byte[stream.Length];
                var read = 0;
                while (read < bytes.Length)
                {
                    var n = stream.Read(bytes, read, bytes.Length - read);
                    if (n <= 0) break;
                    read += n;
                }
                return AssetBundle.LoadFromMemory(bytes);
            }
        }

        public static bool Loaded => _prefabs.Count > 0;

        /// <summary>The bundle prefab (never registered itself; callers clone it), or null with an error line.</summary>
        public static GameObject Prefab(string name)
        {
            if (_prefabs.TryGetValue(name, out var go) && go != null) return go;
            Plugin.Log.LogError($"assets: prefab '{name}' not in bundle '{ResourceName}'");
            return null;
        }

        public static void Track(GameObject go)
        {
            if (go != null && !_tracked.Contains(go)) _tracked.Add(go);
        }

        /// <summary>Frees the bundle file; the loaded prefabs, meshes and materials stay alive.</summary>
        public static void Unload()
        {
            if (_bundle == null) return;
            _bundle.Unload(false);
            _bundle = null;
            Plugin.Log.LogInfo("assets: bundle unloaded (loaded assets kept)");
        }

        /// <summary>
        /// Safety net for Jötunn's shader mocking: every material on <paramref name="go"/> whose shader is still a JVLmock_ stump gets
        /// the vanilla shader of the same name from PrefabManager.Cache (the lookup Jötunn's MockManager.FixShader uses), else
        /// Custom/Creature with a warning. Returns how many materials were changed. Headless servers skip it (no rendering).
        /// </summary>
        public static int FixShaders(GameObject go, string context)
        {
            if (go == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return 0;
            var changed = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || m.shader == null) continue;
                var name = m.shader.name;
                if (!name.StartsWith(MockPrefix, StringComparison.Ordinal)) continue;
                var target = name.Substring(MockPrefix.Length);
                var shader = PrefabManager.Cache.GetPrefab<Shader>(target);
                if (shader != null)
                {
                    m.shader = shader;
                    Plugin.Log.LogInfo($"assets: {context}: material {m.name}: {name} -> {shader.name} (resolved by the plugin)");
                }
                else
                {
                    var fallback = PrefabManager.Cache.GetPrefab<Shader>(FallbackShader);
                    if (fallback == null)
                    {
                        Plugin.Log.LogError($"assets: {context}: material {m.name}: neither {target} nor {FallbackShader} found; stays {name}");
                        continue;
                    }
                    m.shader = fallback;
                    Plugin.Log.LogWarning($"assets: {context}: material {m.name}: vanilla shader '{target}' not found; using {FallbackShader}");
                }
                changed++;
            }
            return changed;
        }

        /// <summary>Runs <see cref="FixShaders"/> over everything built from the bundle and logs how many stumps were still left.</summary>
        public static void FixAllShaders(string context)
        {
            var changed = 0;
            foreach (var go in _tracked) changed += FixShaders(go, context);
            Plugin.Log.LogInfo($"assets: shader check at {context}: {changed} material(s) still had a JVLmock_ shader");
        }

        /// <summary>Lines for ip_bundle: every asset name, then each tracked object's materials with their current shader.</summary>
        public static IEnumerable<string> Describe()
        {
            yield return $"bundle '{ResourceName}': {(_bundle != null ? "open" : "unloaded")}, {AssetNames.Count} assets, {_prefabs.Count} prefabs, {_tracked.Count} tracked objects";
            foreach (var n in AssetNames) yield return "  asset " + n;
            var seen = new HashSet<Material>();
            foreach (var go in _tracked)
            {
                if (go == null) continue;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || !seen.Add(m)) continue;
                    var s = m.shader;
                    yield return $"  material {m.name} (first on {go.name}/{r.name}): shader {(s == null ? "null" : s.name)}, supported {(s != null && s.isSupported)}, " +
                                 $"queue {m.renderQueue}, color {(m.HasProperty("_Color") ? m.color.ToString() : "-")}";
                }
            }
        }
    }
}
