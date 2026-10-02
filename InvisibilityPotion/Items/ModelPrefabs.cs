using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.Rendering;

namespace InvisibilityPotion.Items
{
    /// <summary>
    /// Turns bundle prefabs (root -> attach -> model, see unity/.../Editor/AssetSetup.cs) into game objects: items get ZNetView,
    /// ZSyncTransform, Rigidbody, a BoxCollider and an ItemDrop whose shared data is copied from a vanilla template; plants get a
    /// ZNetView only. Items are Jötunn clones under the item name (the bundle prefab stays untouched); plants are the bundle prefabs.
    /// </summary>
    public static class ModelPrefabs
    {
        public const string MistAnchorName = "MistAnchor";
        public const string MistObjectName = "ip_mist";

        /// <summary>
        /// Clone of <paramref name="bundlePrefab"/> named <paramref name="itemName"/> with the item components; m_shared is a shallow
        /// copy of <paramref name="templateName"/>'s (callers override name, type, effects). Throws when bundle prefab or template is missing.
        /// </summary>
        public static GameObject MakeItem(string bundlePrefab, string itemName, string templateName)
        {
            var source = AssetBundles.Prefab(bundlePrefab) ?? throw new InvalidOperationException($"bundle prefab {bundlePrefab} missing");
            var template = Template(templateName);
            var templateDrop = template.GetComponent<ItemDrop>();

            var go = PrefabManager.Instance.CreateClonedPrefab(itemName, source)
                     ?? throw new InvalidOperationException($"cloning {bundlePrefab} as {itemName} failed (name taken?)");
            SetLayer(go, template.layer);

            var tnv = template.GetComponent<ZNetView>();
            var nv = go.AddComponent<ZNetView>();
            nv.m_persistent = tnv == null || tnv.m_persistent;
            if (tnv != null)
            {
                nv.m_type = tnv.m_type;
                nv.m_distant = tnv.m_distant;
                nv.m_syncInitialScale = tnv.m_syncInitialScale;
            }

            var tst = template.GetComponent<ZSyncTransform>();
            var st = go.AddComponent<ZSyncTransform>();
            if (tst != null)
            {
                st.m_syncPosition = tst.m_syncPosition;
                st.m_syncRotation = tst.m_syncRotation;
                st.m_syncScale = tst.m_syncScale;
                st.m_syncBodyVelocity = tst.m_syncBodyVelocity;
                st.m_characterParentSync = tst.m_characterParentSync;
            }

            var trb = template.GetComponent<Rigidbody>();
            var rb = go.AddComponent<Rigidbody>();
            if (trb != null)
            {
                rb.mass = trb.mass;
                rb.linearDamping = trb.linearDamping;
                rb.angularDamping = trb.angularDamping;
                rb.useGravity = trb.useGravity;
                rb.isKinematic = trb.isKinematic;
                rb.interpolation = trb.interpolation;
                rb.collisionDetectionMode = trb.collisionDetectionMode;
                rb.constraints = trb.constraints;
            }

            var box = go.AddComponent<BoxCollider>();
            var bounds = LocalMeshBounds(go);
            box.center = bounds.center;
            box.size = Vector3.Max(bounds.size, new Vector3(0.02f, 0.02f, 0.02f));
            var tcol = template.GetComponentInChildren<Collider>(true);
            if (tcol != null) box.sharedMaterial = tcol.sharedMaterial;

            var drop = go.AddComponent<ItemDrop>();
            drop.m_autoPickup = templateDrop.m_autoPickup;
            drop.m_autoDestroy = templateDrop.m_autoDestroy;
            drop.m_itemData.m_shared = CloneShared(templateDrop.m_itemData.m_shared);
            drop.m_itemData.m_dropPrefab = go;

            AssetBundles.Track(go);
            Plugin.Log.LogInfo($"assets: item {itemName} <- {bundlePrefab} (template {template.name}: {ComponentList(template)}); " +
                               $"layer {LayerMask.LayerToName(go.layer)}, rigidbody mass {rb.mass} damping {rb.linearDamping}/{rb.angularDamping}, " +
                               $"box {bounds.size:F3} at {bounds.center:F3}, persistent {nv.m_persistent}");
            return go;
        }

        /// <summary>
        /// The bundle prefab itself (registered under its own name, so no clone: PrefabManager.CreateClonedPrefab refuses a name that
        /// its cache already finds, and the loaded bundle asset carries that name) with a ZNetView, not persistent: look-check
        /// objects vanish with their zone.
        /// </summary>
        public static GameObject MakeProp(string bundlePrefab)
        {
            var go = AssetBundles.Prefab(bundlePrefab) ?? throw new InvalidOperationException($"bundle prefab {bundlePrefab} missing");
            var nv = go.GetComponent<ZNetView>();
            if (nv == null) nv = go.AddComponent<ZNetView>();   // no ?? on Unity objects (fake null)
            nv.m_persistent = false;
            AssetBundles.Track(go);
            return go;
        }

        /// <summary>The vanilla prefab through PrefabManager.Cache, else PrefabManager.GetPrefab; must carry an ItemDrop.</summary>
        public static GameObject Template(string name)
        {
            var t = PrefabManager.Cache.GetPrefab<GameObject>(name);
            if (t == null || t.GetComponent<ItemDrop>() == null) t = PrefabManager.Instance.GetPrefab(name);
            if (t == null || t.GetComponent<ItemDrop>() == null) throw new InvalidOperationException($"vanilla template {name} with ItemDrop not found");
            return t;
        }

        private static readonly MethodInfo MemberwiseCloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>Shallow copy (lists, arrays and effect lists stay shared with the template; callers replace what they change).</summary>
        public static ItemDrop.ItemData.SharedData CloneShared(ItemDrop.ItemData.SharedData src) =>
            (ItemDrop.ItemData.SharedData)MemberwiseCloneMethod.Invoke(src, null);

        /// <summary>Bounds of all enabled MeshRenderers in the root's local space, from mesh bounds (works on inactive prefabs).</summary>
        public static Bounds LocalMeshBounds(GameObject root)
        {
            var toRoot = root.transform.worldToLocalMatrix;
            var first = true;
            var b = new Bounds();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || r == null || !r.enabled) continue;
                var m = toRoot * mf.transform.localToWorldMatrix;
                var mb = mf.sharedMesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(corner);
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        private static void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        public static string ComponentList(GameObject go) =>
            string.Join(", ", go.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name));

        // ---------- mist ----------

        private static Material _mistSource;

        /// <summary>
        /// Small local-space mist inside the bottle on every MistAnchor (the ground object and, inside "attach", the item-stand visual).
        /// Colour from the bundle's disabled mist mesh (bottle_mist_tN), size from that mesh. Material: a copy of vfx_swamp_mist's
        /// particle material, the same source FogVeil borrows (Visuals/FogVeil.cs SwampMistMaterial, copied here to keep Items
        /// independent of Visuals).
        /// </summary>
        public static int AddMist(GameObject item, int tier)
        {
            var mistRenderer = item.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name.StartsWith("mist_", StringComparison.Ordinal));
            var color = mistRenderer != null && mistRenderer.sharedMaterial != null && mistRenderer.sharedMaterial.HasProperty("_Color")
                ? mistRenderer.sharedMaterial.color : new Color(0.7f, 0.75f, 0.8f);
            var radius = 0.03f;
            var mf = mistRenderer != null ? mistRenderer.GetComponent<MeshFilter>() : null;
            if (mf != null && mf.sharedMesh != null)
            {
                var e = mf.sharedMesh.bounds.extents;
                radius = Mathf.Clamp(Mathf.Min(e.x, e.z), 0.01f, 0.06f);
            }
            var source = MistSourceMaterial();
            if (source == null)
            {
                Plugin.Log.LogWarning($"assets: T{tier} mist: vfx_swamp_mist material not found; bottle without mist");
                return 0;
            }
            var mat = new Material(source) { name = $"ip_mist_mat_t{tier}" };   // never mutate the vanilla shared material
            if (mat.HasProperty("_Color")) mat.color = new Color(color.r, color.g, color.b, 0.8f);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);

            var n = 0;
            foreach (var anchor in item.GetComponentsInChildren<Transform>(true).Where(t => t.name == MistAnchorName).ToList())
            {
                var go = new GameObject(MistObjectName);
                go.layer = anchor.gameObject.layer;
                go.transform.SetParent(anchor, false);
                ConfigureMist(go, mat, color, radius);
                n++;
            }
            Plugin.Log.LogInfo($"assets: T{tier} mist on {n} anchor(s): color {color}, radius {radius:F3} m, material {source.name} (shader {source.shader?.name})");
            return n;
        }

        private static void ConfigureMist(GameObject go, Material mat, Color color, float radius)
        {
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.002f, 0.008f);
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.8f, radius * 1.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 1f, 1f, 0.7f);
            main.maxParticles = 16;
            var emission = ps.emission;
            emission.rateOverTime = 4f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius * 0.6f;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = mat;
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.shadowCastingMode = ShadowCastingMode.Off;
            psr.receiveShadows = false;
            psr.lightProbeUsage = LightProbeUsage.Off;
        }

        private static Material MistSourceMaterial()
        {
            if (_mistSource != null) return _mistSource;
            var prefab = PrefabManager.Instance.GetPrefab("vfx_swamp_mist") ?? PrefabManager.Cache.GetPrefab<GameObject>("vfx_swamp_mist");
            if (prefab == null) return null;
            foreach (var psr in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (psr.sharedMaterial == null) continue;
                _mistSource = psr.sharedMaterial;
                return _mistSource;
            }
            return null;
        }

        // ---------- plants ----------

        /// <summary>Every Plant_* prefab of the bundle as a spawnable CustomPrefab (look checks with ip_spawn; no gameplay yet).</summary>
        public static void RegisterPlants()
        {
            var names = AssetBundles.Prefabs.Select(p => p.name).Where(n => n.StartsWith("Plant_", StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal).ToList();
            var ok = new List<string>();
            foreach (var name in names)
            {
                try
                {
                    var go = MakeProp(name);
                    PrefabManager.Instance.AddPrefab(new CustomPrefab(go, true));
                    ok.Add(name);
                }
                catch (Exception e) { Plugin.Log.LogError($"assets: plant {name} failed: {e}"); }
            }
            Plugin.Log.LogInfo($"assets: {ok.Count} plant prefabs registered: {string.Join(", ", ok)}");
        }
    }
}
