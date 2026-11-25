using UnityEngine;

/// <summary>
/// Short-lived screen-space UI Kit notification (no pointer).
/// Used by <see cref="ItemPickupNotifier"/> when the kit is available.
/// </summary>
public class KitScreenToast : MonoBehaviour
{
    public float lifetime = 1.6f;
    public float fadeStartAt = 0.55f;

    CanvasGroup canvasGroup;
    RectTransform rect;
    float spawnTime;
    float baseY;
    ItemPickupNotifier host;

    public void Begin(int stackIndex, float rowStep, ItemPickupNotifier owner)
    {
        host = owner;
        rect = transform as RectTransform;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        SetStack(stackIndex, rowStep);
        spawnTime = Time.unscaledTime;
        canvasGroup.alpha = 1f;
    }

    public void SetStack(int stackIndex, float rowStep)
    {
        baseY = -16f - stackIndex * rowStep;
        if (rect != null)
            rect.anchoredPosition = new Vector2(0f, baseY);
    }

    void OnDestroy()
    {
        host?.ReleaseKitToast(this);
    }

    void Update()
    {
        float t = (Time.unscaledTime - spawnTime) / Mathf.Max(0.01f, lifetime);
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        if (rect != null)
            rect.anchoredPosition = new Vector2(0f, baseY + 24f * t);

        if (canvasGroup != null && t > fadeStartAt)
        {
            float fadeT = (t - fadeStartAt) / Mathf.Max(0.0001f, 1f - fadeStartAt);
            canvasGroup.alpha = 1f - fadeT;
        }
    }
}
