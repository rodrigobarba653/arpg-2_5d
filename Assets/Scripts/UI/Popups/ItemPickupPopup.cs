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

    void LateUpdate()
    {
        float t = (Time.time - spawnTime) / lifetime;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        // Rise
        transform.position = startPos + Vector3.up * (riseDistance * t);

        // Fade after fadeStartAt
        if (canvasGroup != null)
        {
            if (t > fadeStartAt)
            {
                float fadeT = (t - fadeStartAt) / Mathf.Max(0.0001f, 1f - fadeStartAt);
                canvasGroup.alpha = 1f - fadeT;
            }
        }

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
}
