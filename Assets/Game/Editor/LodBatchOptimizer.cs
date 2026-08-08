using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;

/// <summary>
/// Batch-generates LOD Groups for heavy Third Party prefabs via UnityMeshSimplifier.
/// Run from CLI:
///   Unity -batchmode -nographics -projectPath ... -executeMethod LodBatchOptimizer.RunBatch -quit
/// </summary>
public static class LodBatchOptimizer
{
    const string RootFolder = "Assets/Third Party";
    const string GeneratedMeshFolder = "Assets/Game/Generated/MeshLODs";
    const int MinTriangles = 5000;
    const int MaxPrefabs = 200; // safety cap for first pass
    const float Lod1Quality = 0.45f;
    const float Lod2Quality = 0.18f;
    const float Lod1Screen = 0.35f;
    const float Lod2Screen = 0.12f;
    const float CullScreen = 0.03f;

    [MenuItem("Tools/ARPG/LOD/Generate Heavy Third Party LODs")]
    public static void RunFromMenu()
    {
        RunInternal(interactive: true);
    }

    /// <summary>Entry point for -executeMethod.</summary>
    public static void RunBatch()
    {
        try
        {
            RunInternal(interactive: false);
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogError($"LodBatchOptimizer failed: {ex}");
            EditorApplication.Exit(1);
        }
    }

    static void RunInternal(bool interactive)
    {
        EnsureFolder(GeneratedMeshFolder);

        var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { RootFolder });
        var candidates = new List<(string path, int tris)>();

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            if (string.IsNullOrEmpty(path))
                continue;
            if (path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            var tris = CountPrefabTriangles(path);
            if (tris >= MinTriangles)
                candidates.Add((path, tris));
        }

        candidates = candidates
            .OrderByDescending(c => c.tris)
            .Take(MaxPrefabs)
            .ToList();

        Debug.Log($"LodBatchOptimizer: {candidates.Count} heavy prefabs (>= {MinTriangles} tris) under {RootFolder}");

        int updated = 0;
        int skipped = 0;
        int failed = 0;

        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < candidates.Count; i++)
            {
                var (path, tris) = candidates[i];
                if (interactive)
                {
                    EditorUtility.DisplayProgressBar(
                        "LOD Batch Optimizer",
                        $"{path} ({tris} tris)",
                        (float)i / Mathf.Max(1, candidates.Count));
                }

                try
                {
                    var result = ProcessPrefab(path, tris);
                    if (result == ProcessResult.Updated)
                        updated++;
                    else
                        skipped++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Debug.LogWarning($"LodBatchOptimizer: failed {path}: {ex.Message}");
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            if (interactive)
                EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"LodBatchOptimizer done. updated={updated} skipped={skipped} failed={failed}");
    }

    enum ProcessResult { Updated, Skipped }

    static int CountPrefabTriangles(string prefabPath)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            return CountTriangles(root);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static int CountTriangles(GameObject root)
    {
        int tris = 0;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh != null)
                tris += Mathf.Max(0, mf.sharedMesh.triangles.Length / 3);
        }

        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh != null)
                tris += Mathf.Max(0, smr.sharedMesh.triangles.Length / 3);
        }

        return tris;
    }

    static ProcessResult ProcessPrefab(string prefabPath, int tris)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            // Already has a meaningful LOD setup
            var existing = root.GetComponent<LODGroup>();
            if (existing != null && existing.GetLODs() != null && existing.GetLODs().Length >= 2)
                return ProcessResult.Skipped;

            var meshFilters = root.GetComponentsInChildren<MeshFilter>(true)
                .Where(mf => mf != null && mf.sharedMesh != null && mf.GetComponent<MeshRenderer>() != null)
                .Where(mf => mf.sharedMesh.triangles.Length / 3 >= MinTriangles / 2) // per-renderer heavy
                .ToList();

            // Prefer whole-prefab LODGenerator when hierarchy is manageable
            bool useHierarchyLod = meshFilters.Count >= 1 &&
                                   root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0 &&
                                   CountTriangles(root) >= MinTriangles;

            if (!useHierarchyLod)
                return ProcessResult.Skipped;

            // Remove empty/broken LODGroup if present
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);

            // Level args: (screenRelativeTransitionHeight, quality)
            var levels = new LODLevel[]
            {
                new LODLevel(0.55f, 1.0f)
                {
                    CombineMeshes = false,
                    CombineSubMeshes = false,
                    ShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ReceiveShadows = true,
                    LightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes,
                    ReflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes,
                },
                new LODLevel(Lod1Screen, Lod1Quality)
                {
                    CombineMeshes = false,
                    CombineSubMeshes = false,
                    ShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ReceiveShadows = true,
                    LightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes,
                    ReflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes,
                },
                new LODLevel(Lod2Screen, Lod2Quality)
                {
                    CombineMeshes = false,
                    CombineSubMeshes = false,
                    ShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                    ReceiveShadows = false,
                    LightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off,
                    ReflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off,
                },
                new LODLevel(CullScreen, 0f)
            };

            var options = SimplificationOptions.Default;
            options.PreserveBorderEdges = true;
            options.PreserveUVSeamEdges = true;
            options.PreserveUVFoldoverEdges = true;

            // GenerateLODs creates child renderers + LODGroup
            var lodGroup = LODGenerator.GenerateLODs(root, levels, true, options);
            if (lodGroup == null)
                return ProcessResult.Skipped;

            // Save generated meshes into our folder so they persist as assets
            PersistGeneratedMeshes(root, prefabPath);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"LodBatchOptimizer: LOD added ({tris} tris) -> {prefabPath}");
            return ProcessResult.Updated;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void PersistGeneratedMeshes(GameObject root, string prefabPath)
    {
        string safeName = Path.GetFileNameWithoutExtension(prefabPath)
            .Replace(" ", "_")
            .Replace("(", "")
            .Replace(")", "");
        string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
        string folder = $"{GeneratedMeshFolder}/{safeName}_{prefabGuid.Substring(0, 8)}";
        EnsureFolder(folder);

        var filters = root.GetComponentsInChildren<MeshFilter>(true);
        int index = 0;
        foreach (var mf in filters)
        {
            var mesh = mf.sharedMesh;
            if (mesh == null)
                continue;

            // Skip meshes that already live on disk as project assets with a stable path
            var existingPath = AssetDatabase.GetAssetPath(mesh);
            if (!string.IsNullOrEmpty(existingPath) && !existingPath.StartsWith("Library/", StringComparison.Ordinal))
            {
                // If this is an original imported mesh, leave it; generated ones are often unnamed clones
                if (!mesh.name.Contains("LOD", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(Path.GetExtension(existingPath)))
                    continue;
            }

            string assetPath = $"{folder}/{Sanitize(mesh.name)}_{index++}.asset";
            if (File.Exists(assetPath))
                AssetDatabase.DeleteAsset(assetPath);

            var copy = UnityEngine.Object.Instantiate(mesh);
            copy.name = mesh.name;
            AssetDatabase.CreateAsset(copy, assetPath);
            mf.sharedMesh = copy;
        }
    }

    static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrEmpty(name) ? "Mesh" : name;
    }

    static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder))
            return;

        var parts = assetFolder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
