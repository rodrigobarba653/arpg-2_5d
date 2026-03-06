using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DuplicateSpriteFramesTo24fps
{
    [MenuItem("Tools/Animation/Convert Selected Sprite Clips 12->24 (Duplicate Frames)")]
    public static void ConvertSelectedClips()
    {
        var selected = Selection.objects;
        if (selected == null || selected.Length == 0)
        {
            Debug.LogWarning("No animation clips selected.");
            return;
        }

        int processed = 0;

        foreach (var obj in selected)
        {
            var clip = obj as AnimationClip;
            if (clip == null) continue;

            if (ConvertClipTo24WithDuplicates(clip))
                processed++;
        }

        Debug.Log($"Converted {processed} clip(s) to 24fps by duplicating sprite frames.");
    }

    private static bool ConvertClipTo24WithDuplicates(AnimationClip clip)
    {
        // Find Sprite curves (SpriteRenderer.m_Sprite)
        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        if (bindings == null || bindings.Length == 0)
        {
            Debug.LogWarning($"{clip.name}: No ObjectReference curves found (no sprite frames).");
            return false;
        }

        bool changedAnything = false;

        foreach (var binding in bindings)
        {
            // Most sprite clips have propertyName "m_Sprite"
            if (binding.propertyName != "m_Sprite")
                continue;

            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keys == null || keys.Length < 2)
            {
                Debug.LogWarning($"{clip.name}: Not enough sprite keys to duplicate.");
                continue;
            }

            var newKeys = new List<ObjectReferenceKeyframe>(keys.Length * 2);

            for (int i = 0; i < keys.Length - 1; i++)
            {
                var a = keys[i];
                var b = keys[i + 1];

                // Keep original key
                newKeys.Add(a);

                // Insert duplicate halfway to double "frame count"
                float midTime = (a.time + b.time) * 0.5f;

                // Only insert if times are meaningfully apart
                if (midTime > a.time + 0.000001f && midTime < b.time - 0.000001f)
                {
                    var mid = new ObjectReferenceKeyframe
                    {
                        time = midTime,
                        value = a.value // duplicate sprite frame
                    };
                    newKeys.Add(mid);
                    changedAnything = true;
                }
            }

            // Add last original key
            newKeys.Add(keys[keys.Length - 1]);

            AnimationUtility.SetObjectReferenceCurve(clip, binding, newKeys.ToArray());
        }

        // Set clip sample rate to 24 for clean timeline + consistent export
        if (Math.Abs(clip.frameRate - 24f) > 0.001f)
        {
            clip.frameRate = 24f;
            changedAnything = true;
        }

        if (changedAnything)
        {
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            Debug.Log($"{clip.name}: ✅ updated to 24fps + duplicated frames.");
            return true;
        }

        Debug.Log($"{clip.name}: No changes needed.");
        return false;
    }
}