using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class Retiming_Attack5FramesPattern_24fps
{
    // Pattern in "frames" at 24fps: 2,4,2,2,2
    private static readonly int[] PatternFrames = { 2, 4, 2, 2, 2 };
    private const float TargetFps = 24f;

    [MenuItem("Tools/Animation/Attack Clips - Apply 5-Frame Pattern (2,4,2,2,2 @ 24fps)")]
    public static void ApplyPatternToSelectedClips()
    {
        var selected = Selection.objects;
        if (selected == null || selected.Length == 0)
        {
            Debug.LogWarning("Select one or more AnimationClips first.");
            return;
        }

        int processed = 0;
        int skipped = 0;

        foreach (var obj in selected)
        {
            var clip = obj as AnimationClip;
            if (clip == null) continue;

            // Optional: only run on clips that look like attacks (name contains "Attack")
            if (!clip.name.ToLowerInvariant().Contains("attack"))
            {
                skipped++;
                continue;
            }

            bool ok = RetimingApply(clip);
            if (ok) processed++;
            else skipped++;
        }

        Debug.Log($"✅ Attack retiming done. Processed: {processed}, Skipped: {skipped}");
    }

    private static bool RetimingApply(AnimationClip clip)
    {
        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        if (bindings == null || bindings.Length == 0)
        {
            Debug.LogWarning($"{clip.name}: No sprite curves found.");
            return false;
        }

        bool changed = false;

        foreach (var binding in bindings)
        {
            // SpriteRenderer sprite property
            if (binding.propertyName != "m_Sprite")
                continue;

            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keys == null || keys.Length == 0)
            {
                Debug.LogWarning($"{clip.name}: Sprite curve empty.");
                continue;
            }

            // Collect first 5 UNIQUE sprites in order
            var frames = ExtractFirstNUniqueSprites(keys, 5);
            if (frames.Count < 5)
            {
                Debug.LogWarning($"{clip.name}: Needs at least 5 unique sprite frames, found {frames.Count}. Skipping.");
                continue;
            }

            // Build new key list with the exact pattern
            // Times are where the sprite CHANGES.
            // We also add a final key at endTime to force clip length and keep last sprite visible.
            var newKeys = new List<ObjectReferenceKeyframe>();

            float t = 0f; // seconds
            for (int i = 0; i < 5; i++)
            {
                newKeys.Add(new ObjectReferenceKeyframe
                {
                    time = t,
                    value = frames[i]
                });

                t += PatternFrames[i] / TargetFps;
            }

            // Force the last sprite to last its duration by adding a key at the end time.
            // (Unity otherwise can truncate weirdly depending on last key time)
            newKeys.Add(new ObjectReferenceKeyframe
            {
                time = t,           // end time (0.5s)
                value = frames[4]   // same last sprite
            });

            // Apply curve + set fps
            AnimationUtility.SetObjectReferenceCurve(clip, binding, newKeys.ToArray());

            if (Math.Abs(clip.frameRate - TargetFps) > 0.001f)
                clip.frameRate = TargetFps;

            changed = true;
        }

        if (!changed)
            return false;

        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        Debug.Log($"{clip.name}: ✅ Applied 5-frame pattern (2,4,2,2,2 @ 24fps).");
        return true;
    }

    private static List<Sprite> ExtractFirstNUniqueSprites(ObjectReferenceKeyframe[] keys, int n)
    {
        var result = new List<Sprite>(n);
        Sprite last = null;

        foreach (var k in keys)
        {
            var s = k.value as Sprite;
            if (s == null) continue;

            // unique in sequence
            if (s == last) continue;

            result.Add(s);
            last = s;

            if (result.Count >= n)
                break;
        }

        return result;
    }
}