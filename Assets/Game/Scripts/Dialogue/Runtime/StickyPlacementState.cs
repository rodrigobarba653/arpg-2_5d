using UnityEngine;

/// <summary>Per-bubble sticky slot for interactive (and reusable) layout.</summary>
public struct StickyPlacementState
{
    public bool hasSlot;
    public DialogueChatboxPlacement side;
    public float liftScreen;
    public float xNudgeScreen;
    public Vector2 pivot;

    /// <summary>Ideal screen position for current anchor + slot (updated every frame).</summary>
    public Vector2 followTarget;

    /// <summary>Currently displayed position (may lerp toward followTarget only on slot change).</summary>
    public Vector2 displayed;

    public bool transitioning;
    public Vector2 transitionFrom;
    public float transitionElapsed;
    public float transitionDuration;

    public void BeginTransition(Vector2 from, float duration)
    {
        transitioning = duration > 0.001f;
        transitionFrom = from;
        transitionElapsed = 0f;
        transitionDuration = Mathf.Max(0.001f, duration);
        if (!transitioning)
            displayed = followTarget;
    }

    public Vector2 TickDisplayed(float dt)
    {
        if (!transitioning)
        {
            displayed = followTarget;
            return displayed;
        }

        transitionElapsed += dt;
        float t = Mathf.Clamp01(transitionElapsed / transitionDuration);
        // Smoothstep for a short, readable hop.
        t = t * t * (3f - 2f * t);
        displayed = Vector2.LerpUnclamped(transitionFrom, followTarget, t);
        if (t >= 1f)
            transitioning = false;
        return displayed;
    }
}
