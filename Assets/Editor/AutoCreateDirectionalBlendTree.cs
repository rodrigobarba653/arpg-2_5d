#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AutoCreateDirectionalBlendTree
{
    private const string DEFAULT_PARAM_X = "MoveX";
    private const string DEFAULT_PARAM_Y = "MoveY";

    [MenuItem("Tools/Animation/Create 8-Direction BlendTree From Selected Clips")]
    public static void CreateBlendTreeFromSelectedClips()
    {
        var clips = Selection.objects
            .OfType<AnimationClip>()
            .Where(c => !AssetDatabase.GetAssetPath(c).StartsWith("Library/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (clips.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "No clips selected",
                "Select your 8 directional animation clips in the Project window first.",
                "OK"
            );
            return;
        }

        string savePath = EditorUtility.SaveFilePanelInProject(
            "Save BlendTree",
            "NewDirectionalBlendTree",
            "asset",
            "Choose where to save the generated BlendTree asset."
        );

        if (string.IsNullOrEmpty(savePath))
            return;

        var blendTree = new BlendTree
        {
            name = System.IO.Path.GetFileNameWithoutExtension(savePath),
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = DEFAULT_PARAM_X,
            blendParameterY = DEFAULT_PARAM_Y,
            useAutomaticThresholds = false
        };

        AssetDatabase.CreateAsset(blendTree, savePath);

        int added = 0;
        List<string> skipped = new List<string>();

        foreach (var clip in clips)
        {
            if (TryGetDirectionPosition(clip.name, out Vector2 pos))
            {
                blendTree.AddChild(clip, pos);
                added++;
            }
            else
            {
                skipped.Add(clip.name);
            }
        }

        EditorUtility.SetDirty(blendTree);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string message =
            $"BlendTree created.\n\n" +
            $"Added clips: {added}\n" +
            $"Blend Type: 2D Freeform Directional\n" +
            $"Parameters: {DEFAULT_PARAM_X}, {DEFAULT_PARAM_Y}";

        if (skipped.Count > 0)
        {
            message += "\n\nSkipped clips:\n- " + string.Join("\n- ", skipped);
        }

        message += "\n\nNow drag this BlendTree asset into your Animator state.";

        EditorUtility.DisplayDialog("Done", message, "OK");

        Selection.activeObject = blendTree;
        EditorGUIUtility.PingObject(blendTree);
    }

    private static bool TryGetDirectionPosition(string clipName, out Vector2 position)
    {
        string n = clipName.ToLowerInvariant();

        // Order matters:
        // check diagonals before single directions
        if (ContainsAny(n, "downright", "down_right", "down-right"))
        {
            position = new Vector2(1, -1);
            return true;
        }

        if (ContainsAny(n, "upright", "up_right", "up-right"))
        {
            position = new Vector2(1, 1);
            return true;
        }

        if (ContainsAny(n, "upleft", "up_left", "up-left"))
        {
            position = new Vector2(-1, 1);
            return true;
        }

        if (ContainsAny(n, "downleft", "down_left", "down-left"))
        {
            position = new Vector2(-1, -1);
            return true;
        }

        if (ContainsAny(n, "_down", "-down", " down") || EndsWithAny(n, "down"))
        {
            position = new Vector2(0, -1);
            return true;
        }

        if (ContainsAny(n, "_up", "-up", " up") || EndsWithAny(n, "up"))
        {
            position = new Vector2(0, 1);
            return true;
        }

        if (ContainsAny(n, "_left", "-left", " left") || EndsWithAny(n, "left"))
        {
            position = new Vector2(-1, 0);
            return true;
        }

        if (ContainsAny(n, "_right", "-right", " right") || EndsWithAny(n, "right"))
        {
            position = new Vector2(1, 0);
            return true;
        }

        position = Vector2.zero;
        return false;
    }

    private static bool ContainsAny(string source, params string[] values)
    {
        foreach (var v in values)
        {
            if (source.Contains(v))
                return true;
        }

        return false;
    }

    private static bool EndsWithAny(string source, params string[] values)
    {
        foreach (var v in values)
        {
            if (source.EndsWith(v))
                return true;
        }

        return false;
    }
}
#endif