using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Upright camera billboard with foreshortening compensation for 2.5D sprites.
/// Attach to the visual SpriteBody child, never the physics root.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public class BillboardToCamera : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("Leave empty to use Camera.main / the Cinemachine Brain output.")]
    public Camera targetCamera;

    [Header("Upright Billboard")]
    [Tooltip("Faces the camera horizontally while keeping the sprite upright.")]
    public bool uprightMode = true;

    [Tooltip("Compensates for visual squashing from elevated camera angles.")]
    public bool correctForeshortening = true;

    [Range(0.5f, 1.5f)]
    [Tooltip("Changes visual proportions, not floor clearance. Start at 1.")]
    public float heightAdjustment = 1f;

    [Range(1f, 4f)]
    [Tooltip("Limits stretching at very steep camera angles.")]
    public float maxHeightStretch = 3f;

    [Header("Floor Alignment")]
    [InspectorName("Height Offset")]
    [Tooltip("Moves the whole image up/down after stretch. Negative lowers it toward the floor. Tune in Play, copy this number, paste after Stop. Do not copy Transform Position Y.")]
    public float verticalOffset = 0f;

    Vector3 originalLocalPosition;
    Vector3 originalLocalScale;
    Quaternion originalLocalRotation;
    float footLocalY;
    bool baseCaptured;
    bool appliedThisFrame;
    Vector3 runtimeExtraOffset;
    Sprite lastSprite;
    SpriteRenderer spriteRenderer;

    /// <summary>Runtime offset on top of the captured pose (water dip, hits, etc.). Cleared when the component disables.</summary>
    public Vector3 ExtraOffset
    {
        get => runtimeExtraOffset;
        set => runtimeExtraOffset = value;
    }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        CaptureBaseIfNeeded();
    }

    void OnEnable()
    {
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCinemachineCameraUpdated);
        appliedThisFrame = false;
        CaptureBaseIfNeeded();
    }

    void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCinemachineCameraUpdated);
        runtimeExtraOffset = Vector3.zero;
        RestoreOriginalTransform();
    }

    void LateUpdate()
    {
        if (!appliedThisFrame)
            ApplyBillboard();

        appliedThisFrame = false;
    }

    void OnCinemachineCameraUpdated(CinemachineBrain brain)
    {
        if (brain != null && brain.OutputCamera != null)
            targetCamera = brain.OutputCamera;

        ApplyBillboard();
        appliedThisFrame = true;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Scrubbing Height Offset in Play (or while paused) updates immediately.
        if (Application.isPlaying && isActiveAndEnabled)
            ApplyBillboard();
    }
#endif

    [ContextMenu("Refresh Paused Preview")]
    void RefreshPausedPreview()
    {
        if (Application.isPlaying && isActiveAndEnabled)
            ApplyBillboard();
    }

    [ContextMenu("Recapture Base Transform")]
    public void RecaptureBase()
    {
        baseCaptured = false;
        CaptureBaseIfNeeded();
        if (Application.isPlaying && isActiveAndEnabled)
            ApplyBillboard();
    }

    void ApplyBillboard()
    {
        Camera cam = ResolveCamera();
        if (cam == null)
            return;

        if (!uprightMode)
        {
            RestoreOriginalTransform();
            transform.forward = cam.transform.forward;
            return;
        }

        CaptureBaseIfNeeded();
        RefreshFootIfSpriteChanged();

        // 1) Rest pose only. Floor offset is applied last so it is never eaten
        //    by the stretch / foot-lock step.
        transform.localPosition = originalLocalPosition + runtimeExtraOffset;
        transform.localScale = originalLocalScale;

        Vector3 uprightForward = cam.transform.forward;
        uprightForward.y = 0f;
        if (uprightForward.sqrMagnitude < 0.000001f)
        {
            uprightForward = transform.forward;
            uprightForward.y = 0f;
            if (uprightForward.sqrMagnitude < 0.000001f)
                uprightForward = Vector3.forward;
        }

        transform.rotation = Quaternion.LookRotation(uprightForward.normalized, Vector3.up);

        float heightStretch = 1f;
        if (correctForeshortening)
        {
            float sinPitch = Mathf.Abs(Vector3.Dot(cam.transform.forward, Vector3.up));
            float cosPitch = Mathf.Sqrt(Mathf.Max(0f, 1f - sinPitch * sinPitch));
            heightStretch = 1f / Mathf.Max(cosPitch, 0.001f);
            heightStretch = Mathf.Min(heightStretch, Mathf.Max(1f, maxHeightStretch));
        }
        heightStretch *= Mathf.Max(0.01f, heightAdjustment);

        // 2) Stretch Y while keeping the sprite's foot point fixed in world space.
        Vector3 footPoint = new Vector3(0f, footLocalY, 0f);
        Vector3 footBeforeStretch = transform.TransformPoint(footPoint);
        Vector3 adjustedScale = originalLocalScale;
        adjustedScale.y *= heightStretch;
        transform.localScale = adjustedScale;
        Vector3 footAfterStretch = transform.TransformPoint(footPoint);
        transform.position += footBeforeStretch - footAfterStretch;

        // 3) Floor alignment — moves the whole image after stretch is locked.
        if (verticalOffset != 0f)
            transform.localPosition += Vector3.up * verticalOffset;
    }

    Camera ResolveCamera()
    {
        if (targetCamera != null)
            return targetCamera;

        targetCamera = Camera.main;
        return targetCamera;
    }

    void CaptureBaseIfNeeded()
    {
        if (baseCaptured)
            return;

        originalLocalPosition = transform.localPosition - runtimeExtraOffset;
        originalLocalScale = transform.localScale;
        originalLocalRotation = transform.localRotation;
        CacheFootFromSprite();
        baseCaptured = true;
    }

    void RefreshFootIfSpriteChanged()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        Sprite current = spriteRenderer != null ? spriteRenderer.sprite : null;
        if (current == lastSprite)
            return;

        CacheFootFromSprite();
    }

    void CacheFootFromSprite()
    {
        footLocalY = 0f;
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        lastSprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        if (lastSprite == null)
            return;

        Bounds spriteBounds = lastSprite.bounds;
        footLocalY = spriteRenderer.flipY ? -spriteBounds.max.y : spriteBounds.min.y;
    }

    void RestoreOriginalTransform()
    {
        if (!baseCaptured)
            return;

        transform.localPosition = originalLocalPosition;
        transform.localScale = originalLocalScale;
        transform.localRotation = originalLocalRotation;
    }
}
