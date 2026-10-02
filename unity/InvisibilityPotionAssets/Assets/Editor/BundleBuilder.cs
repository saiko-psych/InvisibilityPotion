using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Headless asset bundle build:
//   Unity -batchmode -nographics -quit -projectPath unity/InvisibilityPotionAssets \
//     -executeMethod BundleBuilder.Build -target linux|windows -logFile <log>
// Output: Build/Bundles/<target>/ip_assets. Exits with code 1 on failure.
public static class BundleBuilder
{
    const string BundleName = "ip_assets";
    static readonly string[] BundleFolders = { "Assets/Prefabs", "Assets/Materials" };

    public static void Build()
    {
        try
        {
            var targetName = ReadTarget();
            BuildTarget target;
            switch (targetName)
            {
                case "linux": target = BuildTarget.StandaloneLinux64; break;
                case "windows": target = BuildTarget.StandaloneWindows64; break;
                default: throw new ArgumentException("Unknown -target '" + targetName + "' (expected linux or windows)");
            }

            AssignBundleNames();

            var outDir = Path.Combine("Build", "Bundles", targetName);
            Directory.CreateDirectory(outDir);
            var manifest = BuildPipeline.BuildAssetBundles(outDir,
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode,
                target);
            if (manifest == null) throw new Exception("BuildAssetBundles returned no manifest");

            var bundlePath = Path.GetFullPath(Path.Combine(outDir, BundleName));
            if (!File.Exists(bundlePath)) throw new Exception("Bundle not written: " + bundlePath);
            Debug.Log("[BundleBuilder] Built " + target + ": " + bundlePath + " (" + new FileInfo(bundlePath).Length + " bytes)");
            foreach (var asset in AssetDatabase.GetAssetPathsFromAssetBundle(BundleName))
                Debug.Log("[BundleBuilder]   " + asset);
        }
        catch (Exception e)
        {
            Debug.LogError("[BundleBuilder] Failed: " + e);
            EditorApplication.Exit(1);
        }
    }

    static string ReadTarget()
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-target") return args[i + 1].ToLowerInvariant();
        return "linux";
    }

    static void AssignBundleNames()
    {
        foreach (var guid in AssetDatabase.FindAssets("", BundleFolders))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path)) continue;
            var importer = AssetImporter.GetAtPath(path);
            if (importer != null && importer.assetBundleName != BundleName)
                importer.assetBundleName = BundleName;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.RemoveUnusedAssetBundleNames();
    }
}
