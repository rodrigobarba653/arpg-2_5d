using UnityEngine;

/// <summary>
/// Shared screen safe margins for event-driven UI (dialogue now; notifications later).
/// Normalized 0-1 insets from each screen edge.
/// </summary>
[System.Serializable]
public class DialogueUiSafeArea
{
    [Range(0f, 0.4f)] public float left = 0.04f;
    [Range(0f, 0.4f)] public float right = 0.04f;
    [Range(0f, 0.4f)] public float top = 0.06f;
    [Range(0f, 0.4f)] public float bottom = 0.12f;

    /// <summary>Band reserved so ambient avoids the typical interactive lower panel.</summary>
    [Range(0f, 0.6f)] public float interactiveReserveBottom = 0.34f;

    public Rect GetSafeScreenRect()
    {
        float x = Screen.width * left;
        float y = Screen.height * bottom;
        float w = Screen.width * (1f - left - right);
        float h = Screen.height * (1f - top - bottom);
        return new Rect(x, y, w, h);
    }

    public Rect GetInteractiveReserveScreenRect()
    {
        float x = Screen.width * left;
        float y = 0f;
        float w = Screen.width * (1f - left - right);
        float h = Screen.height * interactiveReserveBottom;
        return new Rect(x, y, w, h);
    }
}
