#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CombatAttackEventsTool
{
    private const int ENABLE_HITBOX_FRAME  = 6;
    private const int DISABLE_HITBOX_FRAME = 10;
    private const int TRY_ADVANCE_FRAME    = 11;

    private const int DEFAULT_STEP = 1;

    private const string FN_ENABLE_INT  = "EnableHitboxInt";
    private const string FN_ENABLE_OLD  = "EnableHitbox";
    private const string FN_DISABLE     = "DisableHitbox";
    private const string FN_TRY_ADVANCE = "TryAdvanceCombo";
    private const string FN_END_ATTACK  = "EndAttack";

    [MenuItem("Tools/Combat/Attack Clips - Set Combat Events")]
    public static void ApplyCombatEventsToSelectedClips()
    {
        var clips = GetSelectedClips();
        if (clips.Count == 0)
        {
            Debug.LogWarning("Select one or more AnimationClips in the Project window.");
            return;
        }

        int changed = 0;
        foreach (var clip in clips)
        {
            if (!clip) continue;
            if (ApplyToClip(clip)) changed++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CombatAttackEventsTool] Updated {changed} clip(s).");
    }

    private static bool ApplyToClip(AnimationClip clip)
    {
        int step = InferStepFromName(clip.name);
        float fps = clip.frameRate > 0f ? clip.frameRate : 24f;

        int totalFrames = GetTotalFrames(clip, fps);
        int lastFrameIndex = Mathf.Max(0, totalFrames - 1);

        // IMPORTANT:
        // These go to Unity timeline frame numbers directly.
        // Frame 6 means time = 6 / fps, not 5 / fps.
        float tEnable     = FrameNumberToUnityTime(ENABLE_HITBOX_FRAME, fps, lastFrameIndex);
        float tDisable    = FrameNumberToUnityTime(DISABLE_HITBOX_FRAME, fps, lastFrameIndex);
        float tTryAdvance = FrameNumberToUnityTime(TRY_ADVANCE_FRAME, fps, lastFrameIndex);
        float tEndAttack  = lastFrameIndex / fps;

        var events = AnimationUtility.GetAnimationEvents(clip).ToList();

        events.RemoveAll(e =>
            e != null &&
            (e.functionName == FN_ENABLE_INT ||
             e.functionName == FN_ENABLE_OLD ||
             e.functionName == FN_DISABLE ||
             e.functionName == FN_TRY_ADVANCE ||
             e.functionName == FN_END_ATTACK)
        );

        events.Add(new AnimationEvent
        {
            functionName = FN_ENABLE_INT,
            time = tEnable,
            intParameter = step
        });

        events.Add(new AnimationEvent
        {
            functionName = FN_DISABLE,
            time = tDisable
        });

        events.Add(new AnimationEvent
        {
            functionName = FN_TRY_ADVANCE,
            time = tTryAdvance
        });

        events.Add(new AnimationEvent
        {
            functionName = FN_END_ATTACK,
            time = tEndAttack
        });

        events = events.OrderBy(e => e.time).ToList();

        AnimationUtility.SetAnimationEvents(clip, events.ToArray());
        EditorUtility.SetDirty(clip);

        Debug.Log(
            $"[{clip.name}] " +
            $"EnableHitboxInt -> frame {ENABLE_HITBOX_FRAME} ({tEnable:0.###}s) | " +
            $"DisableHitbox -> frame {DISABLE_HITBOX_FRAME} ({tDisable:0.###}s) | " +
            $"TryAdvanceCombo -> frame {TRY_ADVANCE_FRAME} ({tTryAdvance:0.###}s) | " +
            $"EndAttack -> last frame {lastFrameIndex} ({tEndAttack:0.###}s)"
        );

        return true;
    }

    private static float FrameNumberToUnityTime(int frameNumber, float fps, int lastFrameIndex)
    {
        int clampedFrame = Mathf.Clamp(frameNumber, 0, lastFrameIndex);
        return clampedFrame / fps;
    }

    private static int GetTotalFrames(AnimationClip clip, float fps)
    {
        return Mathf.Max(1, Mathf.RoundToInt(clip.length * fps));
    }

    private static List<AnimationClip> GetSelectedClips()
    {
        return Selection.objects
            .OfType<AnimationClip>()
            .Where(c => !AssetDatabase.GetAssetPath(c).StartsWith("Library/", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static int InferStepFromName(string clipName)
    {
        if (string.IsNullOrEmpty(clipName)) return DEFAULT_STEP;

        string n = clipName.ToLowerInvariant();

        if (ContainsAttackStep(n, 1)) return 1;
        if (ContainsAttackStep(n, 2)) return 2;
        if (ContainsAttackStep(n, 3)) return 3;

        return DEFAULT_STEP;
    }

    private static bool ContainsAttackStep(string lowerName, int step)
    {
        return lowerName.Contains($"attack{step}") ||
               lowerName.Contains($"attack_{step}") ||
               lowerName.Contains($"attack-{step}") ||
               lowerName.Contains($"atk{step}") ||
               lowerName.Contains($"atk_{step}") ||
               lowerName.Contains($"atk-{step}");
    }
}
#endif