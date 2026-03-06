#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CombatAttackEventsTool
{
    // ============================================================
    // CONFIG
    // ============================================================

    // You asked: EnableHitboxInt on 6th frame, DisableHitbox on 10th frame
    private const int ENABLE_HITBOX_FRAME  = 7;
    private const int DISABLE_HITBOX_FRAME = 11;

    // IMPORTANT: "6th frame" is ambiguous (1-based vs 0-based).
    // Most artists mean 1-based (frame 1 = first frame at time 0).
    // We'll default to 1-based to match that mental model.
    private const bool FRAMES_ARE_1_BASED = true;

    // Safety: clamp events inside clip range
    private const float TIME_EPSILON = 0.0001f;

    // Auto step by name ("Attack1", "Attack2", "Attack3")
    // If not found, fallback to 1.
    private const int DEFAULT_STEP = 1;

    // Functions we manage
    private const string FN_ENABLE_INT = "EnableHitboxInt";
    private const string FN_ENABLE_OLD = "EnableHitbox";
    private const string FN_DISABLE    = "DisableHitbox";

    // ============================================================

    [MenuItem("Tools/Combat/Attack Clips - Set Hitbox Events (Enable f6, Disable f10)")]
    public static void ApplyHitboxEventsToSelectedClips()
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
        Debug.Log($"[CombatAttackEventsTool] Updated {changed} clip(s). (EnableHitboxInt @ frame {ENABLE_HITBOX_FRAME}, DisableHitbox @ frame {DISABLE_HITBOX_FRAME})");
    }

    private static bool ApplyToClip(AnimationClip clip)
    {
        // Step inferred from name
        int step = InferStepFromName(clip.name);

        // Convert "frame" -> time, using the clip's frameRate
        float fps = clip.frameRate > 0f ? clip.frameRate : 24f;

        float tEnable  = FrameToTime(ENABLE_HITBOX_FRAME, fps);
        float tDisable = FrameToTime(DISABLE_HITBOX_FRAME, fps);

        // Clamp inside clip safely
        float safeMax = GetSafeEndTime(clip, fps);
        tEnable  = Mathf.Clamp(tEnable,  0f, safeMax);
        tDisable = Mathf.Clamp(tDisable, 0f, safeMax);

        // Ensure enable < disable (in case the clip is super short)
        if (tDisable <= tEnable)
            tDisable = Mathf.Clamp(tEnable + TIME_EPSILON, 0f, safeMax);

        // Load existing events
        var events = AnimationUtility.GetAnimationEvents(clip).ToList();

        // Remove ONLY hitbox-related events (keep TryAdvanceCombo + EndAttack as-is)
        int before = events.Count;
        events.RemoveAll(e =>
            e != null &&
            (e.functionName == FN_ENABLE_INT ||
             e.functionName == FN_ENABLE_OLD ||
             e.functionName == FN_DISABLE)
        );

        // Add our two events
        var evEnable = new AnimationEvent
        {
            functionName = FN_ENABLE_INT,
            time = tEnable,
            intParameter = step
        };

        var evDisable = new AnimationEvent
        {
            functionName = FN_DISABLE,
            time = tDisable
        };

        events.Add(evEnable);
        events.Add(evDisable);

        // Sort by time
        events = events.OrderBy(e => e.time).ToList();

        // Apply
        AnimationUtility.SetAnimationEvents(clip, events.ToArray());
        EditorUtility.SetDirty(clip);

        // Log useful info
        Debug.Log($"[{clip.name}] step={step} | EnableHitboxInt@{tEnable:0.000}s (frame {ENABLE_HITBOX_FRAME}) | DisableHitbox@{tDisable:0.000}s (frame {DISABLE_HITBOX_FRAME}) | kept other events={before - (before - events.Count + 2)}");

        return true;
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

        // Common patterns: "Attack1", "Attack_1", "attack-1", etc.
        if (ContainsAttackStep(n, 1)) return 1;
        if (ContainsAttackStep(n, 2)) return 2;
        if (ContainsAttackStep(n, 3)) return 3;

        return DEFAULT_STEP;
    }

    private static bool ContainsAttackStep(string lowerName, int step)
    {
        // Try many common naming variations
        return lowerName.Contains($"attack{step}") ||
               lowerName.Contains($"attack_{step}") ||
               lowerName.Contains($"attack-{step}") ||
               lowerName.Contains($"atk{step}") ||
               lowerName.Contains($"atk_{step}") ||
               lowerName.Contains($"atk-{step}");
    }

    private static float FrameToTime(int frameNumber, float fps)
    {
        // If 1-based: frame 1 should be time 0
        // If 0-based: frame 0 should be time 0
        int index = FRAMES_ARE_1_BASED ? Mathf.Max(0, frameNumber - 1) : Mathf.Max(0, frameNumber);
        return index / fps;
    }

    private static float GetSafeEndTime(AnimationClip clip, float fps)
    {
        // Avoid placing exactly at clip.length (Unity can drop it outside last sample)
        float epsilon = 1f / Mathf.Max(1f, fps);
        return Mathf.Max(0f, clip.length - epsilon * 0.5f);
    }
}
#endif