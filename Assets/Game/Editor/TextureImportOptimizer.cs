using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Re-applies lightweight texture import defaults used for load/VRAM optimization.
/// Prefer running after pulling texture .meta changes so Unity reimports cleanly.
/// </summary>
public static class TextureImportOptimizer
{
    const int ThirdPartyMaxSize = 1024;
    const int GameMaxSize = 2048;

    [MenuItem("Tools/ARPG/Optimize Texture Imports/Third Party (Max 1024 + Streaming)")]
    static void OptimizeThirdParty()
    {
        OptimizeFolder("Assets/Third Party", ThirdPartyMaxSize, enableStreaming: true);
    }

    [MenuItem("Tools/ARPG/Optimize Texture Imports/Game (Cap 2048 + Streaming)")]
    static void OptimizeGame()
    {
        OptimizeFolder("Assets/Game", GameMaxSize, enableStreaming: true);
    }

    static void OptimizeFolder(string folder, int maxSize, bool enableStreaming)
    {
        var guids = AssetDatabase.FindAssets("t:Texture", new[] { folder });
        int changed = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                    continue;
                if (path.IndexOf("Lightmap", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                EditorUtility.DisplayProgressBar(
                    "Optimize Texture Imports",
                    path,
                    (float)i / Mathf.Max(1, guids.Length));

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;

                bool dirty = false;
                if (importer.maxTextureSize > maxSize)
                {
                    importer.maxTextureSize = maxSize;
                    dirty = true;
                }

                if (importer.textureCompression == TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    dirty = true;
                }

                if (enableStreaming &&
                    importer.mipmapEnabled &&
                    importer.textureType != TextureImporterType.Sprite &&
                    !importer.streamingMipmaps)
                {
                    importer.streamingMipmaps = true;
                    dirty = true;
                }

                if (!dirty)
                    continue;

                importer.SaveAndReimport();
                changed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"TextureImportOptimizer: updated {changed} textures under {folder} (maxSize={maxSize}).");
    }
}
