using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Creates the spike assets (S1/S2) without the GUI:
//   Unity -batchmode -nographics -quit -projectPath unity/InvisibilityPotionAssets -executeMethod SpikeSetup.Create
// One 0.5 m cube mesh, one material per mock shader plus one with Unity "Standard",
// and one prefab per material that is tested in-game (root -> "model" child with the renderer).
public static class SpikeSetup
{
    const string MeshPath = "Assets/Meshes/SpikeCube.asset";
    const string MaterialDir = "Assets/Materials";
    const string PrefabDir = "Assets/Prefabs";

    public static void Create()
    {
        try
        {
            Directory.CreateDirectory(MaterialDir);
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(Path.GetDirectoryName(MeshPath));

            var mesh = CreateCubeMesh();

            var creature = CreateMaterial("Spike_Creature", MockShader("Creature"), new Color(0.35f, 0.6f, 0.3f, 1f));
            var distortion = CreateMaterial("Spike_Distortion", MockShader("Distortion"), new Color(0.6f, 0.8f, 1f, 0.35f));
            CreateMaterial("Spike_LitParticles", MockShader("LitParticles"), new Color(0.8f, 0.8f, 0.85f, 0.5f));
            var piece = CreateMaterial("Spike_Piece", MockShader("Piece"), new Color(0.55f, 0.4f, 0.25f, 1f));
            var standard = CreateMaterial("Spike_Standard", Shader.Find("Standard"), new Color(0.8f, 0.25f, 0.2f, 1f));

            CreatePrefab("SpikeCube_Creature", mesh, creature);
            CreatePrefab("SpikeCube_Distortion", mesh, distortion);
            CreatePrefab("SpikeCube_Piece", mesh, piece);
            CreatePrefab("SpikeCube_Standard", mesh, standard);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SpikeSetup] Done: mesh, 5 materials, 4 prefabs.");
        }
        catch (Exception e)
        {
            Debug.LogError("[SpikeSetup] Failed: " + e);
            EditorApplication.Exit(1);
        }
    }

    static Shader MockShader(string vanillaName)
    {
        var path = "Assets/Shaders/JVLmock_Custom_" + vanillaName + ".shader";
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
        if (shader == null) throw new Exception("Shader not found: " + path);
        if (shader.name != "JVLmock_Custom/" + vanillaName) throw new Exception("Unexpected shader name " + shader.name + " in " + path);
        return shader;
    }

    static Material CreateMaterial(string name, Shader shader, Color color)
    {
        if (shader == null) throw new Exception("Shader missing for material " + name);
        var path = MaterialDir + "/" + name + ".mat";
        AssetDatabase.DeleteAsset(path);
        var mat = new Material(shader) { name = name };
        mat.SetColor("_Color", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void CreatePrefab(string name, Mesh mesh, Material mat)
    {
        var root = new GameObject(name);
        var model = new GameObject("model");
        model.transform.SetParent(root.transform, false);
        model.AddComponent<MeshFilter>().sharedMesh = mesh;
        model.AddComponent<MeshRenderer>().sharedMaterial = mat;
        var path = PrefabDir + "/" + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path, out var ok);
        UnityEngine.Object.DestroyImmediate(root);
        if (!ok) throw new Exception("Saving prefab failed: " + path);
    }

    // Own mesh asset instead of the built-in cube, so the bundle carries the mesh itself.
    // 0.5 m edge, pivot at the bottom centre (like the item models later).
    static Mesh CreateCubeMesh()
    {
        AssetDatabase.DeleteAsset(MeshPath);
        const float h = 0.25f;
        var corners = new[]
        {
            new Vector3(-h, 0, -h), new Vector3(h, 0, -h), new Vector3(h, 0, h), new Vector3(-h, 0, h),
            new Vector3(-h, 2 * h, -h), new Vector3(h, 2 * h, -h), new Vector3(h, 2 * h, h), new Vector3(-h, 2 * h, h),
        };
        // Each face as 4 corner indices in counter-clockwise order seen from outside.
        int[][] faces =
        {
            new[] { 0, 1, 2, 3 }, // bottom (-Y)
            new[] { 4, 7, 6, 5 }, // top (+Y)
            new[] { 0, 4, 5, 1 }, // front (-Z)
            new[] { 2, 6, 7, 3 }, // back (+Z)
            new[] { 1, 5, 6, 2 }, // right (+X)
            new[] { 3, 7, 4, 0 }, // left (-X)
        };
        var verts = new Vector3[24];
        var uvs = new Vector2[24];
        var tris = new int[36];
        for (int f = 0; f < 6; f++)
        {
            for (int i = 0; i < 4; i++) verts[f * 4 + i] = corners[faces[f][i]];
            uvs[f * 4 + 0] = new Vector2(0, 0); uvs[f * 4 + 1] = new Vector2(0, 1);
            uvs[f * 4 + 2] = new Vector2(1, 1); uvs[f * 4 + 3] = new Vector2(1, 0);
            // Unity uses clockwise winding for front faces.
            int b = f * 4;
            tris[f * 6 + 0] = b; tris[f * 6 + 1] = b + 3; tris[f * 6 + 2] = b + 2;
            tris[f * 6 + 3] = b; tris[f * 6 + 4] = b + 2; tris[f * 6 + 5] = b + 1;
        }
        var mesh = new Mesh { name = "SpikeCube", vertices = verts, uv = uvs, triangles = tris };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, MeshPath);
        return mesh;
    }
}
