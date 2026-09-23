using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Keeps WeaponBody on the Camera → SpriteBody ray at a fixed proportional
/// distance (t), and applies the mathematically correct perspective scale.
///
/// Attach to WeaponBody. Does not touch billboard/orientation.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public class WeaponPerspectiveAlignment : MonoBehaviour
{
    [Header("References")]
    [Tooltip("SpriteBody (parent). Auto-resolved from transform.parent if empty.")]
    [SerializeField] Transform spriteBody;

    [Tooltip("Rendering camera. Auto-resolved from Camera.main / Cinemachine brain if empty.")]
    [SerializeField] Camera targetCamera;

    [Header("Behavior")]
    [Tooltip("If true, recalculate t once camera + transforms are available at play start.")]
    [SerializeField] bool calibrateTOnStart = true;

    [Tooltip("Optional safety clamp. B must stay between Camera and SpriteBody.")]
    [SerializeField] Vector2 tClamp = new Vector2(0.01f, 0.99f);

    [Header("Debug (read-only)")]
    [SerializeField] float t;
    [SerializeField] float tPercent;
    [SerializeField] float cameraToSpriteDistance;
    [SerializeField] float cameraToWeaponDistance;
    [SerializeField] Vector3 desiredWeaponWorldScale;
    [SerializeField] bool calibrated;

    [Header("Gizmos")]
    [SerializeField] bool drawGizmos = true;
    [SerializeField] Color rayColor = new Color(0.2f, 0.85f, 1f, 0.9f);
    [SerializeField] Color weaponPointColor = new Color(1f, 0.85f, 0.2f, 0.95f);

    bool appliedThisFrame;

    void Reset()
    {
        spriteBody = transform.parent;
    }

    void Awake()
    {
        if (!spriteBody)
            spriteBody = transform.parent;
    }

    void OnEnable()
    {
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCinemachineCameraUpdated);
        appliedThisFrame = false;

        if (calibrateTOnStart && !calibrated)
            TryCalibrateAndApply();
    }

    void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCinemachineCameraUpdated);
    }

    void Start()
    {
        if (calibrateTOnStart && !calibrated)
            TryCalibrateAndApply();
    }

    void LateUpdate()
    {
        // Fallback when no CinemachineBrain is driving the camera this frame.
        if (!appliedThisFrame)
            ApplyPerspective();

        appliedThisFrame = false;
    }

    void OnCinemachineCameraUpdated(CinemachineBrain brain)
    {
        if (brain != null && brain.OutputCamera != null)
            targetCamera = brain.OutputCamera;

        if (calibrateTOnStart && !calibrated)
            TryCalibrateAndApply();
        else
            ApplyPerspective();

        appliedThisFrame = true;
    }

    /// <summary>
    /// Capture t from the CURRENT world-space WeaponBody position, then apply
    /// perspective position + mathematically correct scale.
    /// </summary>
    [ContextMenu("Recalibrate T From Current Position")]
    public void RecalibrateFromCurrentPosition()
    {
        calibrated = false;
        TryCalibrateAndApply();
    }

    void TryCalibrateAndApply()
    {
        if (!ResolveRefs())
            return;

        Vector3 c = targetCamera.transform.position;
        Vector3 a = spriteBody.position;
        Vector3 b = transform.position;

        Vector3 ca = a - c;
        float caSqr = ca.sqrMagnitude;
        if (caSqr < 1e-8f)
            return;

        float rawT = Vector3.Dot(b - c, ca) / caSqr;
        t = Mathf.Clamp(rawT, tClamp.x, tClamp.y);

        if (rawT <= 0f || rawT >= 1f)
        {
            Debug.LogWarning(
                $"[WeaponPerspectiveAlignment] Calibrated t={rawT:F4} was outside (0,1) " +
                $"and was clamped to {t:F4}. Check Camera / SpriteBody / WeaponBody placement.",
                this);
        }

        calibrated = true;
        ApplyPerspective();
    }

    void ApplyPerspective()
    {
        if (!calibrated)
            return;

        if (!ResolveRefs())
            return;

        Vector3 c = targetCamera.transform.position;
        Vector3 a = spriteBody.position;

        Vector3 ca = a - c;
        float caMag = ca.magnitude;
        if (caMag < 1e-8f)
            return;

        // Position: stay on Camera → SpriteBody at fixed proportional distance.
        Vector3 bWorld = Vector3.LerpUnclamped(c, a, t);
        transform.position = bWorld;

        // Scale: derive independently every frame from SpriteBody + t + parent.
        // Desired world scale so B matches A's apparent projected size:
        //   B_worldScale = A_worldScale * t
        // Parent is SpriteBody, so localScale ≈ t * Vector3.one after accounting
        // for parent lossy scale (handles PlayerPrefab ~2.5 hierarchy safely).
        Vector3 parentLossy = transform.parent != null
            ? transform.parent.lossyScale
            : Vector3.one;

        Vector3 desiredWorld = spriteBody.lossyScale * t;
        desiredWeaponWorldScale = desiredWorld;

        transform.localScale = new Vector3(
            desiredWorld.x / SafeDivisor(parentLossy.x),
            desiredWorld.y / SafeDivisor(parentLossy.y),
            desiredWorld.z / SafeDivisor(parentLossy.z));

        // Debug readouts
        tPercent = t * 100f;
        cameraToSpriteDistance = caMag;
        cameraToWeaponDistance = Vector3.Distance(c, transform.position);
    }

    bool ResolveRefs()
    {
        if (!spriteBody)
            spriteBody = transform.parent;

        if (!spriteBody)
            return false;

        if (!targetCamera)
            targetCamera = Camera.main;

        return targetCamera != null;
    }

    static float SafeDivisor(float v)
    {
        return Mathf.Abs(v) < 1e-6f ? 1e-6f * Mathf.Sign(v == 0f ? 1f : v) : v;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        Transform sprite = spriteBody != null ? spriteBody : transform.parent;
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (sprite == null || cam == null)
            return;

        Vector3 c = cam.transform.position;
        Vector3 a = sprite.position;
        Vector3 b = calibrated
            ? Vector3.LerpUnclamped(c, a, t)
            : transform.position;

        Gizmos.color = rayColor;
        Gizmos.DrawLine(c, a);

        Gizmos.color = weaponPointColor;
        Gizmos.DrawSphere(b, 0.05f);
    }
}
