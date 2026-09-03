using UnityEngine;
using UnityEditor;

public static class AddAttackEventsTool
{
    // ============================================================
    // CONFIG: Choose ONE approach (Normalized OR Frame-based)
    // ============================================================

    // --- Option A: Normalized timing (0..1) ---
    // Good default: hitbox active in the middle, combo check near end, end at very end.
    private const float ENABLE_HITBOX_NORM  = 0.25f;
    private const float DISABLE_HITBOX_NORM = 0.55f;
    private const float TRY_COMBO_NORM      = 0.85f;
    private const float END_ATTACK_NORM     = 0.98f; // not 1.0 to avoid landing outside last sample

    // --- Option B: Frame-based timing ---
    // If you want "Enable at frame 3, Disable at frame 9" etc.
    // This uses clip.frameRate (samples per second) to convert frames -> seconds.
    private const bool USE_FRAME_BASED = false;

    private const int ENABLE_HITBOX_FRAME  = 3;
    private const int DISABLE_HITBOX_FRAME = 9;
    private const int TRY_COMBO_FRAME      = 12;
    private const int END_ATTACK_FRAME     = 14;

    // ============================================================

    [MenuItem("Tools/Animation/Add Attack Events To Selected Clips")]
    public static void AddAttackEventsToSelectedClips()
    {
        Object[] selection = Selection.objects;

        if (selection == null || selection.Length == 0)
        {
            Debug.LogWarning("No animation clips selected. Select one or more AnimationClips in Project window.");
            return;
        }

        int processed = 0;
        int skipped = 0;

        foreach (Object obj in selection)
        {
            AnimationClip clip = obj as AnimationClip;
            if (clip == null)
            {
                skipped++;
                continue;
            }

            AddOrReplaceAttackEventsOnClip(clip);
            processed++;
        }

        Debug.Log($"Attack events processed on {processed} clip(s). Skipped {skipped} non-clip selection(s).");
    }

    private static void AddOrReplaceAttackEventsOnClip(AnimationClip clip)
    {
        // Get existing
        var existing = AnimationUtility.GetAnimationEvents(clip);

        // Remove any prior versions of our functions (so rerunning tool is safe)
        existing = RemoveByFunction(existing, "EnableHitbox");
        existing = RemoveByFunction(existing, "DisableHitbox");
        existing = RemoveByFunction(existing, "TryAdvanceCombo");
        existing = RemoveByFunction(existing, "EndAttack");

        // Calculate event times
        float tEnable, tDisable, tTryCombo, tEnd;

        if (USE_FRAME_BASED)
        {
            // Convert frames to seconds using clip.frameRate
            float fps = clip.frameRate > 0 ? clip.frameRate : 24f;
            tEnable   = FramesToTime(ENABLE_HITBOX_FRAME, fps);
            tDisable  = FramesToTime(DISABLE_HITBOX_FRAME, fps);
            tTryCombo = FramesToTime(TRY_COMBO_FRAME, fps);
            tEnd      = FramesToTime(END_ATTACK_FRAME, fps);
        }
        else
        {
            tEnable   = clip.length * ENABLE_HITBOX_NORM;
            tDisable  = clip.length * DISABLE_HITBOX_NORM;
            tTryCombo = clip.length * TRY_COMBO_NORM;
            tEnd      = clip.length * END_ATTACK_NORM;
        }

        // Clamp times so they ALWAYS land inside the clip
        // (Unity can behave weird if you set event at exactly clip.length)
        float safeMax = GetSafeEndTime(clip);
        tEnable   = Mathf.Clamp(tEnable, 0f, safeMax);
        tDisable  = Mathf.Clamp(tDisable, 0f, safeMax);
        tTryCombo = Mathf.Clamp(tTryCombo, 0f, safeMax);
        tEnd      = Mathf.Clamp(tEnd, 0f, safeMax);

        // Ensure correct ordering (just in case)
        // We want: Enable < Disable < TryCombo < End
        SortTimes(ref tEnable, ref tDisable, ref tTryCombo, ref tEnd);

        // Build events
        var eEnable  = NewEvent("EnableHitbox", tEnable);
        var eDisable = NewEvent("DisableHitbox", tDisable);
        var eTry     = NewEvent("TryAdvanceCombo", tTryCombo);
        var eEnd     = NewEvent("EndAttack", tEnd);

        // Combine + sort by time
        var combined = Combine(existing, eEnable, eDisable, eTry, eEnd);
        System.Array.Sort(combined, (a, b) => a.time.CompareTo(b.time));

        // Apply
        AnimationUtility.SetAnimationEvents(clip, combined);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[{clip.name}] Added events: " +
            $"EnableHitbox@{tEnable:0.000}s, DisableHitbox@{tDisable:0.000}s, " +
            $"TryAdvanceCombo@{tTryCombo:0.000}s, EndAttack@{tEnd:0.000}s"
        );
    }

    // ----------------- Helpers -----------------

    private static AnimationEvent NewEvent(string functionName, float time)
    {
        return new AnimationEvent
        {
            functionName = functionName,
            time = time
        };
    }

    private static AnimationEvent[] RemoveByFunction(AnimationEvent[] events, string fn)
    {
        if (events == null || events.Length == 0) return events;

        int keepCount = 0;
        for (int i = 0; i < events.Length; i++)
        {
            if (events[i] != null && events[i].functionName == fn) continue;
            keepCount++;
        }

        AnimationEvent[] kept = new AnimationEvent[keepCount];
        int k = 0;
        for (int i = 0; i < events.Length; i++)
        {
            if (events[i] != null && events[i].functionName == fn) continue;
            kept[k++] = events[i];
        }

        return kept;
    }

    private static AnimationEvent[] Combine(AnimationEvent[] existing, params AnimationEvent[] add)
    {
        int a = existing != null ? existing.Length : 0;
        int b = add != null ? add.Length : 0;

        AnimationEvent[] combined = new AnimationEvent[a + b];

        for (int i = 0; i < a; i++) combined[i] = existing[i];
        for (int j = 0; j < b; j++) combined[a + j] = add[j];

        return combined;
    }

    private static float FramesToTime(int frame, float fps)
    {
        // frame 0 = time 0
        return frame / fps;
    }

    private static float GetSafeEndTime(AnimationClip clip)
    {
        // Put end event slightly before clip.length to avoid landing outside last sample.
        // We use 1 frame worth of time as a safe buffer.
        float fps = clip.frameRate > 0 ? clip.frameRate : 24f;
        float epsilon = 1f / fps;
        return Mathf.Max(0f, clip.length - epsilon * 0.5f);
    }

    private static void SortTimes(ref float a, ref float b, ref float c, ref float d)
    {
        // Simple bubble-ish reorder to ensure ascending
        if (b < a) Swap(ref a, ref b);
        if (c < b) Swap(ref b, ref c);
        if (b < a) Swap(ref a, ref b);
        if (d < c) Swap(ref c, ref d);
        if (c < b) Swap(ref b, ref c);
        if (b < a) Swap(ref a, ref b);

        // Extra safety: ensure distinct times (if clip is tiny)
        // If equal, nudge forward a tiny bit (but keep within safe end via clamp earlier).
        const float tiny = 0.0001f;
        if (b <= a) b = a + tiny;
        if (c <= b) c = b + tiny;
        if (d <= c) d = c + tiny;
    }

    private static void Swap(ref float x, ref float y)
    {
        float t = x;
        x = y;
        y = t;
    }
}