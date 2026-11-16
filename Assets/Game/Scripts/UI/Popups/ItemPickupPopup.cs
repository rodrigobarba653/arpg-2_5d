using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// World-space popup that floats above the player when an item is picked up.
/// Animates upward + fades out, then destroys itself.
///
/// Designed to be attached to a prefab the level designer builds. The prefab
/// should have:
///  - World Space Canvas
///  - CanvasGroup (drag into <see cref="canvasGroup"/>)
///  - Optional: Image for the item icon (drag into <see cref="iconImage"/>)
///  - TextMeshPro - Text (UI) for the label (drag into <see cref="label"/>)
///
/// The script doesn't touch font, size, color, sprites, layout — all that lives
/// in the prefab and the level designer is free to style it.
/// </summary>
public class ItemPickupPopup : MonoBehaviour
{
    [Header("Refs (assign in prefab)")]
    [Tooltip("CanvasGroup on the root of the popup. Used for fade-out.")]
    public CanvasGroup canvasGroup;

    [Tooltip("Optional icon for the item. Hidden if no sprite is passed.")]
    public Image iconImage;

    [Tooltip("Label that will display the obtained text.")]
    public TMP_Text label;

    [Header("Text Format")]
    [Tooltip("{0} will be replaced with the item's displayName.")]
    public string format = "Obtained \"{0}\"";

    [Header("Animation")]
    [Tooltip("Total time the popup stays alive (including fade).")]
    public float lifetime = 1.4f;

    [Tooltip("World units to rise during the lifetime.")]
    public float riseDistance = 1.2f;

    [Tooltip("0-1 of lifetime when the fade-out starts. 0.5 = half-way.")]
    [Range(0f, 1f)]
    public float fadeStartAt = 0.5f;

    [Header("Billboard")]
    [Tooltip("If true, the popup rotates to face the camera every frame.")]
    public bool billboardToCamera = true;

    Vector3 startPos;
    float spawnTime;
    Camera cam;
    bool screenMode;
    float screenBaseY;
    ItemPickupNotifier screenHost;

    public void Setup(string itemDisplayName, Sprite icon)
    {
        if (label != null)
            label.text = string.Format(format, itemDisplayName);

        if (iconImage != null)
        {
            if (icon != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.enabled = false;
            }
        }
    }

    void Awake()
    {
        startPos = transform.position;
        spawnTime = Time.time;
        cam = Camera.main;
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    /// <summary>
    /// Switches this popup onto the screen at the notification band.
    /// Returns false when the prefab has no canvas, so the caller can use the world path.
    /// </summary>
    public bool BeginScreenPresentation(int stackIndex, float rowStep)
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null)
            return false;

        screenMode = true;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = (int)DialoguePresentationPriority.Notification;

        var raycaster = GetComponent<UnityEngine.UI.GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = false;

        transform.localScale = Vector3.one;
        var rect = (RectTransform)transform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(420f, 44f);
        SetScreenStack(stackIndex, rowStep);

        spawnTime = Time.time;
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
        return true;
    }

    public void SetScreenStack(int stackIndex, float rowStep)
    {
        if (!screenMode)
            return;

        screenBaseY = -12f - stackIndex * rowStep;
        var rect = (RectTransform)transform;
        rect.anchoredPosition = new Vector2(0f, screenBaseY);
    }

    public void BindScreenHost(ItemPickupNotifier host)
    {
        screenHost = host;
    }

    void OnDestroy()
    {
        if (screenHost != null)
            screenHost.ReleaseScreenNotice(this);
    }

    void LateUpdate()
    {
        float t = (Time.time - spawnTime) / Mathf.Max(0.01f, lifetime);
        if (screenMode)
        {
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            var rect = (RectTransform)transform;
            rect.anchoredPosition = new Vector2(0f, screenBaseY + 28f * t);
            ApplyFade(t);
            return;
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        // Rise
        transform.position = startPos + Vector3.up * (riseDistance * t);

        ApplyFade(t);

        // Billboard
        if (billboardToCamera)
        {
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                Vector3 dir = cam.transform.position - transform.position;
                if (dir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(-dir);
            }
        }
    }

    void ApplyFade(float t)
    {
        if (canvasGroup == null || t <= fadeStartAt)
            return;

        float fadeT = (t - fadeStartAt) / Mathf.Max(0.0001f, 1f - fadeStartAt);
        canvasGroup.alpha = 1f - fadeT;
    }
}
