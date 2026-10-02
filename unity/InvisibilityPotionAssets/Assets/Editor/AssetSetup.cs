using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Builds every material and prefab of the bundle from Assets/Models/*.fbx (written by `make models`), headless:
//   Unity -batchmode -nographics -quit -projectPath unity/InvisibilityPotionAssets -executeMethod AssetSetup.Create
//
// Per FBX:
//   1. Importer: scale 1, no cameras/lights/animation/colliders, read/write off.
//   2. Import once with embedded Standard materials to read the Blender colours (_Color, alpha, emission).
//   3. Create or update one Assets/Materials/<name>.mat per material name (shared across FBX files) with a JVLmock_ stump
//      shader (see ShaderFor), then remap the FBX materials to those .mat files by name and reimport.
//   4. Prefab Assets/Prefabs/<PrefabName>.prefab:
//        items (bottle_, bowl_, goggles_, ingredient_):  root -> attach -> model (FBX hierarchy, anchors as imported)
//        plants:                            root -> model
//      Valheim shows only the direct child named "attach" of an item prefab (VisEquipment.AttachItem, ItemStand.SetVisualItem),
//      placed with an identity local transform at the joint; so the model sits inside it, shifted so that the FBX's own
//      "attach" empty (the grip point) lands on the attach origin. Mist meshes (mist_*) keep their material but get a
//      disabled renderer: the mist is a code-built particle system on MistAnchor.
// Materials and prefabs that no FBX produces any more are deleted. Every step is logged with the prefix [AssetSetup].
public static class AssetSetup
{
    const string ModelDir = "Assets/Models";
    const string MaterialDir = "Assets/Materials";
    const string PrefabDir = "Assets/Prefabs";
    const string TextureDir = "Assets/Textures";
    const string WhitePath = TextureDir + "/ip_white.asset";

    // Blender GLASS_ALPHA / lens alphas (tools/blender/make_bottle.py, make_goggles.py), used only when the FBX import
    // carries no alpha (logged).
    static readonly Dictionary<string, float> AlphaFallback = new Dictionary<string, float>
    {
        ["bottle_glass_t1"] = 0.45f, ["bottle_glass_t2"] = 0.45f, ["bottle_glass_t3"] = 0.45f,
        ["amber_lens"] = 0.6f, ["crystal_lens"] = 0.35f, ["obsidian_lens"] = 0.88f,
    };

    // Blender's FBX exporter writes EmissiveColor (0,0,0) for Principled emission, so the strength is read from the MATS tables
    // of tools/blender/make_*.py: "name": ((r, g, b, a), roughness, metallic, emission[, alpha]). Emission = colour x strength.
    static readonly Regex MatsEntry = new Regex(@"""(\w+)"":\s*\(\(([\d.]+),\s*([\d.]+),\s*([\d.]+),\s*[\d.]+\),\s*[\d.]+,\s*[\d.]+,\s*([\d.]+)");
    static Dictionary<string, Color> _emission;

    static Dictionary<string, Color> EmissionTable()
    {
        if (_emission != null) return _emission;
        _emission = new Dictionary<string, Color>();
        var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "tools", "blender"));
        if (!Directory.Exists(dir)) { Debug.LogWarning("[AssetSetup] " + dir + " missing: no emission"); return _emission; }
        foreach (var file in Directory.GetFiles(dir, "make_*.py").OrderBy(f => f, StringComparer.Ordinal))
        foreach (Match m in MatsEntry.Matches(File.ReadAllText(file)))
        {
            var strength = float.Parse(m.Groups[5].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (strength <= 0f) continue;
            float F(int g) => float.Parse(m.Groups[g].Value, System.Globalization.CultureInfo.InvariantCulture);
            _emission[m.Groups[1].Value] = new Color(F(2) * strength, F(3) * strength, F(4) * strength, 1f);
        }
        Debug.Log("[AssetSetup] emission from " + dir + ": " + string.Join(", ", _emission.Keys.OrderBy(k => k, StringComparer.Ordinal)));
        return _emission;
    }

    // Fix round 2 (ruling 2): Custom/Distortion read as broken on a bottle in game; the bottle glass is opaque Custom/Creature
    // like the vanilla potions (MeadHealthMinor: potion mesh with weapons1, Custom/Creature), its Blender colour lerped
    // GlassLighten toward white. true restores the Distortion glass (alpha and refraction as before) for later experiments.
    const bool UseDistortionGlass = false;
    const float GlassLighten = 0.25f;

    static bool IsGlass(string name) => name.Contains("_glass");

    static readonly string[] FoliageWords = { "strand", "tip", "leaf", "petal", "frond" };

    public static void Create()
    {
        try
        {
            Directory.CreateDirectory(MaterialDir);
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(TextureDir);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var white = WhiteTexture();
            var models = Directory.GetFiles(ModelDir, "*.fbx").Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal).ToList();
            if (models.Count == 0) throw new Exception("No FBX in " + ModelDir + " (run `make models`)");

            var materials = new Dictionary<string, Material>();
            foreach (var path in models) ImportWithMaterials(path, white, materials);

            var prefabs = new List<string>();
            foreach (var path in models) prefabs.Add(BuildPrefab(path));

            RemoveStale(MaterialDir, "*.mat", materials.Keys);
            RemoveStale(PrefabDir, "*.prefab", prefabs);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AssetSetup] Done: " + models.Count + " models, " + materials.Count + " materials, " + prefabs.Count + " prefabs.");
        }
        catch (Exception e)
        {
            Debug.LogError("[AssetSetup] Failed: " + e);
            EditorApplication.Exit(1);
        }
    }

    // ---------- import and materials ----------

    static void ImportWithMaterials(string path, Texture2D white, Dictionary<string, Material> materials)
    {
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) throw new Exception("No ModelImporter for " + path);
        imp.globalScale = 1f;
        imp.useFileScale = true;
        imp.importCameras = false;
        imp.importLights = false;
        imp.importVisibility = false;
        imp.importBlendShapes = false;
        imp.importAnimation = false;
        imp.animationType = ModelImporterAnimationType.None;
        imp.addCollider = false;
        imp.isReadable = false;
        imp.importNormals = ModelImporterNormals.Import;
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
        imp.materialName = ModelImporterMaterialName.BasedOnMaterialName;

        // Pass 1: no remaps, so the embedded materials carry the Blender values.
        foreach (var kv in imp.GetExternalObjectMap())
            if (kv.Key.type == typeof(Material)) imp.RemoveRemap(kv.Key);
        imp.SaveAndReimport();

        var embedded = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().OrderBy(m => m.name, StringComparer.Ordinal).ToList();
        if (embedded.Count == 0) throw new Exception("No materials imported from " + path);
        var names = embedded.Select(m => m.name).ToList();   // the embedded materials are destroyed by the reimport below
        foreach (var src in embedded)
        {
            if (!materials.TryGetValue(src.name, out var mat))
            {
                mat = WriteMaterial(src, white);
                materials[src.name] = mat;
            }
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), mat);
        }
        // Pass 2: the FBX now uses our .mat files.
        imp.SaveAndReimport();
        Debug.Log("[AssetSetup] " + path + ": " + string.Join(", ", names));
    }

    /// <summary>Stump shader for a material name (vanilla name without the JVLmock_ prefix).</summary>
    static string ShaderFor(string name)
    {
        if (IsGlass(name)) return UseDistortionGlass ? "Custom/Distortion" : "Custom/Creature";
        if (name.Contains("lens")) return "Custom/Distortion";
        if (name.StartsWith("bottle_mist", StringComparison.Ordinal)) return "Custom/Creature";   // renderer disabled in the prefab
        var plant = name.StartsWith("huldra_", StringComparison.Ordinal) || name.StartsWith("baldr_", StringComparison.Ordinal) || name.StartsWith("helfern_", StringComparison.Ordinal);
        if (plant && FoliageWords.Any(w => name.Contains(w))) return "Custom/Vegetation";
        return "Custom/Creature";   // wood, metal, leather, cork, brew, stones: Custom/Piece is the alternative (stump exists)
    }

    static Material WriteMaterial(Material src, Texture2D white)
    {
        var vanilla = ShaderFor(src.name);
        var shader = MockShader(vanilla);
        var color = src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
        var mode = src.HasProperty("_Mode") ? src.GetFloat("_Mode") : 0f;
        var alphaSource = "fbx";
        if (vanilla == "Custom/Distortion" && color.a >= 0.999f && AlphaFallback.TryGetValue(src.name, out var fallback))
        {
            color.a = fallback;
            alphaSource = "fallback table";
        }
        else if (vanilla != "Custom/Distortion")
        {
            color.a = 1f;
            alphaSource = "opaque";
        }
        if (IsGlass(src.name) && vanilla != "Custom/Distortion")
        {
            color = Color.Lerp(color, Color.white, GlassLighten);
            color.a = 1f;
            alphaSource = "opaque glass, lightened " + GlassLighten.ToString("0.##") + " toward white";
        }
        var emission = EmissionTable().TryGetValue(src.name, out var e) ? e : Color.black;

        var path = MaterialDir + "/" + src.name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = src.name };
            AssetDatabase.CreateAsset(mat, path);
        }
        else mat.shader = shader;
        mat.SetColor("_Color", color);
        mat.SetTexture("_MainTex", white);
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", emission);
            if (emission.maxColorComponent > 0.001f) mat.EnableKeyword("_EMISSION"); else mat.DisableKeyword("_EMISSION");
        }
        if (vanilla == "Custom/Distortion")
        {
            mat.SetFloat("_RefractionIntensity", 0.02f);
            mat.SetFloat("_Glossiness", 0.9f);
        }
        EditorUtility.SetDirty(mat);
        Debug.Log("[AssetSetup]   material " + src.name + " -> JVLmock_" + vanilla + " color " + Fmt(color) + " (alpha: " + alphaSource +
                  ", fbx _Mode " + mode + ") emission " + Fmt(emission));
        return mat;
    }

    static Shader MockShader(string vanillaName)
    {
        var path = "Assets/Shaders/JVLmock_" + vanillaName.Replace('/', '_') + ".shader";
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
        if (shader == null) throw new Exception("Shader not found: " + path);
        if (shader.name != "JVLmock_" + vanillaName) throw new Exception("Unexpected shader name " + shader.name + " in " + path);
        return shader;
    }

    // White 4x4 albedo so the vanilla shaders never sample a missing _MainTex (their default texture is unknown).
    static Texture2D WhiteTexture()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(WhitePath);
        if (tex != null) return tex;
        tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "ip_white" };
        var px = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply(false, false);
        AssetDatabase.CreateAsset(tex, WhitePath);
        return tex;
    }

    // ---------- prefabs ----------

    static bool IsItem(string file) =>
        file.StartsWith("bottle_", StringComparison.Ordinal) || file.StartsWith("bowl_", StringComparison.Ordinal) || file.StartsWith("goggles_", StringComparison.Ordinal) ||
        file.StartsWith("ingredient_", StringComparison.Ordinal);

    /// <summary>bottle_t1 -> MeadBottle_T1, bowl_t2 -> MeadBowl_T2, goggles_t3 -> Goggles_T3, ingredient_t1 -> Ingredient_T1, plant_t1_flat_a -> Plant_T1_Flat_a, plant_t2_picked -> Plant_T2_picked.</summary>
    public static string PrefabName(string file)
    {
        var parts = file.Split('_');
        var head = parts[0] == "bottle" ? "MeadBottle" : parts[0] == "bowl" ? "MeadBowl" : parts[0] == "goggles" ? "Goggles" :
                   parts[0] == "ingredient" ? "Ingredient" : parts[0] == "plant" ? "Plant" : null;
        if (head == null) throw new Exception("No prefab name rule for " + file);
        var outParts = new List<string> { head };
        for (int i = 1; i < parts.Length; i++)
        {
            var p = parts[i];
            if (p.Length == 2 && (p[0] == 't' || p[0] == 's') && char.IsDigit(p[1])) outParts.Add(p.ToUpperInvariant());
            else if (p == "flat") outParts.Add("Flat");
            else outParts.Add(p);
        }
        return string.Join("_", outParts);
    }

    static string BuildPrefab(string path)
    {
        var file = Path.GetFileNameWithoutExtension(path);
        var name = PrefabName(file);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) throw new Exception("Model not loadable: " + path);

        var root = new GameObject(name);
        var model = (GameObject)UnityEngine.Object.Instantiate(asset);
        model.name = "model";
        var t = model.transform;
        Debug.Log("[AssetSetup] " + name + " <- " + path + ": imported root rot " + t.localEulerAngles + " scale " + t.localScale +
                  ", children " + string.Join(",", Children(t)));

        if (IsItem(file))
        {
            var attach = new GameObject("attach");
            attach.transform.SetParent(root.transform, false);
            t.SetParent(attach.transform, false);
            var grip = FindDeep(t, "attach");
            if (grip == null) Debug.LogWarning("[AssetSetup] " + name + ": no attach empty in " + path + "; grip = model origin");
            else
            {
                var offset = grip.position;   // attach is at the world origin with identity rotation here
                t.localPosition -= offset;
                Debug.Log("[AssetSetup]   grip at " + Fmt(offset) + " m -> model shifted by " + Fmt(-offset));
            }
        }
        else t.SetParent(root.transform, false);

        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.name.StartsWith("mist_", StringComparison.Ordinal)) continue;
            r.enabled = false;
            Debug.Log("[AssetSetup]   " + r.name + ": renderer disabled (code-built mist)");
        }

        var b = MeshBounds(root);
        Debug.Log("[AssetSetup]   bounds size " + Fmt(b.size) + " centre " + Fmt(b.center) + ", anchors " +
                  string.Join(",", new[] { "attach", "MistAnchor", "PickAnchor", "EmberAnchor" }.Where(a => FindDeep(root.transform, a) != null)));

        var prefabPath = PrefabDir + "/" + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out var ok);
        UnityEngine.Object.DestroyImmediate(root);
        if (!ok) throw new Exception("Saving prefab failed: " + prefabPath);
        return name;
    }

    static IEnumerable<string> Children(Transform t)
    {
        foreach (Transform c in t) yield return c.name;
    }

    static Transform FindDeep(Transform t, string name)
    {
        foreach (Transform c in t)
        {
            if (c.name == name) return c;
            var f = FindDeep(c, name);
            if (f != null) return f;
        }
        return null;
    }

    static Bounds MeshBounds(GameObject root)
    {
        var first = true;
        var b = new Bounds();
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!r.enabled) continue;
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    static void RemoveStale(string dir, string pattern, IEnumerable<string> keep)
    {
        var keepSet = new HashSet<string>(keep);
        foreach (var f in Directory.GetFiles(dir, pattern))
        {
            var n = Path.GetFileNameWithoutExtension(f);
            if (keepSet.Contains(n)) continue;
            AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
            Debug.Log("[AssetSetup] removed stale " + f);
        }
    }

    static string Fmt(Color c) => "(" + c.r.ToString("0.###") + ", " + c.g.ToString("0.###") + ", " + c.b.ToString("0.###") + ", " + c.a.ToString("0.###") + ")";
    static string Fmt(Vector3 v) => "(" + v.x.ToString("0.###") + ", " + v.y.ToString("0.###") + ", " + v.z.ToString("0.###") + ")";
}
