using UnityEngine;

/// <summary>
/// Unity 6 URP: inexpensive collectible shimmer overlay with proximity gating.
/// Attach to the same object as a MeshFilter and MeshRenderer.
/// Does not change the original material. No physics or per-frame distance checks.
/// </summary>
[DisallowMultipleComponent]
public sealed class MathraMagazineShimmer : MonoBehaviour
{
    [Header("Proximity Activation")]
    [Tooltip("Assign the moving AX/Jyn player Transform (not a static spawn point).")]
    public Transform player;
    [Tooltip("If Player is not assigned, find the GameObject tagged Player once on enable.")]
    public bool autoFindPlayerByTag = true;
    [Min(0.1f)] public float activationDistance = 6f;
    [Tooltip("Extra distance allowed before switching OFF; avoids flickering at the boundary.")]
    [Min(0f)] public float exitDistanceBuffer = 1f;
    [Tooltip("Seconds between distance checks. 0.25 is inexpensive even for multiple pickups.")]
    [Range(0.05f, 2f)] public float distanceCheckInterval = 0.25f;
    [Tooltip("If no player is found, keep the shimmer off instead of running everywhere.")]
    public bool requirePlayer = true;

    [Header("Timing")]
    [Min(0.1f)] public float sweepDuration = 1.1f;
    [Min(0f)] public float pauseBetweenSweeps = 3f;
    public bool playImmediately = true;

    [Header("Art Direction")]
    [ColorUsage(true, true)] public Color shimmerColor = new Color(0.55f, 0.83f, 1f, 1f);
    [Range(0, 8)] public float brightness = 2f;
    [Range(0.02f, 0.8f)] public float bandWidth = 0.25f;
    [Range(2f, 24f)] public float coreSharpness = 12f;
    [Range(0f, 1f)] public float softVeil = 0.4f;
    [Range(0f, 0.01f)] public float surfaceLift = 0.0006f;
    [Tooltip("Sweep direction in mesh LOCAL coordinates.")]
    public Vector3 localSweepAxis = new Vector3(1, 0, 1);

    [Header("Optional")]
    [Tooltip("Optional custom material using Mathra/Collectibles/Magazine Shimmer URP.")]
    public Material shimmerMaterialTemplate;

    MeshRenderer overlayRenderer;
    Material instanceMaterial;
    float clock;
    float nextDistanceCheck;
    bool sweeping;
    bool inRange;

    void Awake()
    {
        MeshFilter source = GetComponent<MeshFilter>();
        MeshRenderer original = GetComponent<MeshRenderer>();
        if (source == null || source.sharedMesh == null || original == null)
        {
            Debug.LogError("Mathra Magazine Shimmer needs a MeshFilter and MeshRenderer on the same GameObject.", this);
            enabled = false;
            return;
        }

        Shader shader = Shader.Find("Mathra/Collectibles/Magazine Shimmer URP");
        if (shimmerMaterialTemplate == null && shader == null)
        {
            Debug.LogError("Mathra Magazine Shimmer shader not found. Import its .shader file.", this);
            enabled = false;
            return;
        }

        instanceMaterial = shimmerMaterialTemplate != null
            ? new Material(shimmerMaterialTemplate)
            : new Material(shader);
        instanceMaterial.name = "Mathra Shimmer (Runtime)";
        instanceMaterial.hideFlags = HideFlags.DontSave;
        ApplyAppearance();
        SetProjectionRange(source.sharedMesh.bounds);

        GameObject overlay = new GameObject("__MathraShimmerOverlay_Runtime");
        overlay.transform.SetParent(transform, false);
        MeshFilter filter = overlay.AddComponent<MeshFilter>();
        filter.sharedMesh = source.sharedMesh;
        overlayRenderer = overlay.AddComponent<MeshRenderer>();
        int count = Mathf.Max(1, source.sharedMesh.subMeshCount);
        Material[] mats = new Material[count];
        for (int i = 0; i < count; i++) mats[i] = instanceMaterial;
        overlayRenderer.sharedMaterials = mats;
        overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
        overlayRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        overlayRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        overlayRenderer.enabled = false;
    }

    void Start()
    {
        TryFindPlayer();
        CheckDistance(); // activate immediately if the player already starts nearby
        nextDistanceCheck = Time.time + Mathf.Max(0.05f, distanceCheckInterval);
    }

    void TryFindPlayer()
    {
        if (player != null || !autoFindPlayerByTag) return;
        // Only called on Start or when explicitly asked; never searched every frame.
        try
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }
        catch (UnityException)
        {
            // Project may not have a Player tag; assign the Transform in Inspector.
        }
    }

    void CheckDistance()
    {
        if (player == null)
        {
            if (requirePlayer) SetInRange(false);
            else SetInRange(true);
            return;
        }

        float enterRadius = Mathf.Max(0.1f, activationDistance);
        float exitRadius = enterRadius + Mathf.Max(0f, exitDistanceBuffer);
        float radius = inRange ? exitRadius : enterRadius;
        bool near = (player.position - transform.position).sqrMagnitude <= radius * radius;
        SetInRange(near);
    }

    void SetInRange(bool value)
    {
        if (inRange == value) return;
        inRange = value;
        sweeping = false;
        clock = 0f;
        if (overlayRenderer != null) overlayRenderer.enabled = false;
        if (inRange && playImmediately) StartSweep();
    }

    void ApplyAppearance()
    {
        if (instanceMaterial == null) return;
        instanceMaterial.SetColor("_ShimmerColor", shimmerColor);
        instanceMaterial.SetFloat("_Brightness", brightness);
        instanceMaterial.SetFloat("_BandWidth", bandWidth);
        instanceMaterial.SetFloat("_CoreSharpness", coreSharpness);
        instanceMaterial.SetFloat("_VeilStrength", softVeil);
        instanceMaterial.SetFloat("_SurfaceLift", surfaceLift);
        instanceMaterial.SetFloat("_SweepDuration", Mathf.Max(0.1f, sweepDuration));
    }

    void SetProjectionRange(Bounds bounds)
    {
        Vector3 axis = localSweepAxis.sqrMagnitude > 0.0001f ? localSweepAxis.normalized : Vector3.right;
        Vector3 c = bounds.center, e = bounds.extents;
        float mid = Vector3.Dot(c, axis);
        float extent = Mathf.Abs(axis.x) * e.x + Mathf.Abs(axis.y) * e.y + Mathf.Abs(axis.z) * e.z;
        instanceMaterial.SetVector("_Axis", new Vector4(axis.x, axis.y, axis.z, 0));
        instanceMaterial.SetVector("_Range", new Vector4(mid - extent, mid + extent, 0, 0));
    }

    void StartSweep()
    {
        if (overlayRenderer == null || !inRange) return;
        ApplyAppearance();
        instanceMaterial.SetFloat("_SweepStart", Time.timeSinceLevelLoad);
        clock = 0f;
        sweeping = true;
        overlayRenderer.enabled = true;
    }

    void Update()
    {
        if (overlayRenderer == null) return;

        if (Time.time >= nextDistanceCheck)
        {
            nextDistanceCheck = Time.time + Mathf.Max(0.05f, distanceCheckInterval);
            CheckDistance();
        }
        if (!inRange) return; // NO shimmer rendering or animation work while far away

        clock += Time.deltaTime;
        if (sweeping)
        {
            if (clock >= Mathf.Max(0.1f, sweepDuration))
            {
                sweeping = false;
                clock = 0f;
                overlayRenderer.enabled = false;
            }
        }
        else if (clock >= pauseBetweenSweeps) StartSweep();
    }

    [ContextMenu("Trigger Shimmer Now")]
    public void TriggerShimmerNow()
    {
        if (Application.isPlaying && inRange) StartSweep();
    }

    [ContextMenu("Find Tagged Player Again")]
    public void FindTaggedPlayerAgain()
    {
        player = null;
        TryFindPlayer();
        if (Application.isPlaying) CheckDistance();
    }

    void OnDisable()
    {
        if (overlayRenderer != null) overlayRenderer.enabled = false;
        sweeping = false;
        inRange = false;
    }

    void OnDestroy()
    {
        if (instanceMaterial != null)
        {
            if (Application.isPlaying) Destroy(instanceMaterial);
            else DestroyImmediate(instanceMaterial);
        }
    }
}
