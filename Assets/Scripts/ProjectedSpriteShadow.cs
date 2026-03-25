using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ProjectedSpriteShadow : MonoBehaviour
{
    [Header("Target (world follow)")]
    public Transform target;               // Drag Player here (NOT SpriteBody)

    [Header("Source (sprite to copy)")]
    public SpriteRenderer source;          // Drag SpriteBody's SpriteRenderer here

    [Header("Light")]
    public Light directionalLight;         // Drag Directional Light here

    [Header("Look")]
    [Range(0f, 1f)] public float alpha = 0.35f;
    public float groundY = 0.01f;

    [Header("Projection")]
    public float height = 1.0f;
    public float offsetPerHeight = 0.35f;
    public float stretchPerHeight = 0.65f;

    [Header("Manual Alignment")]
    public Vector2 manualOffset;   // X = left/right, Y = forward/back

    [Header("Fixes")]
    public bool forcePositiveScale = true; // <- TURN THIS ON
    public bool keepUniformX = true;       // keeps X as the base scale

    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void LateUpdate()
    {
        if (!source || !target) return;

        // 0) Stick shadow to target position in WORLD space
        Vector3 p = target.position;
        transform.position = new Vector3(p.x, groundY, p.z);

        // 0.5) FIX MIRRORING: if any parent has negative scale, force ours positive
        if (forcePositiveScale)
        {
            Vector3 ls = transform.localScale;
            ls.x = Mathf.Abs(ls.x);
            ls.y = Mathf.Abs(ls.y);
            ls.z = Mathf.Abs(ls.z);

            if (keepUniformX)
            {
                // keep X as the "base" and let Y be stretched later
                ls.y = ls.x;
                ls.z = ls.x;
            }

            transform.localScale = ls;
        }

        // 1) Copy sprite (animation frame)
        sr.sprite = source.sprite;

        // IMPORTANT: don't flip the shadow for 8-direction sheets
        sr.flipX = false;
        sr.flipY = false;

        // 2) Shadow color
        sr.color = new Color(0f, 0f, 0f, alpha);

        if (!directionalLight) return;

        // 3) Directional light projection
        Vector3 lightDir = -directionalLight.transform.forward;
        Vector3 groundDir = new Vector3(lightDir.x, 0f, lightDir.z);
        if (groundDir.sqrMagnitude < 0.0001f) return;
        groundDir.Normalize();

        float yaw = Mathf.Atan2(groundDir.x, groundDir.z) * Mathf.Rad2Deg;
yaw += 180f; // <--- FIX: flip the shadow facing
transform.rotation = Quaternion.Euler(90f, yaw, 0f);

        // Automatic light-based offset (slide away from feet)
        Vector3 offsetWorld = transform.up * (height * offsetPerHeight);

        // Manual tweak offset
        Vector3 manualWorldOffset =
            transform.right * manualOffset.x +
            transform.up * manualOffset.y;

        transform.position += new Vector3(offsetWorld.x, 0f, offsetWorld.z);
        transform.position += new Vector3(manualWorldOffset.x, 0f, manualWorldOffset.z);

        // Stretch shadow (only along local Y)
        Vector3 s = transform.localScale;
        float baseX = Mathf.Abs(s.x);
        s.x = baseX;
        s.z = baseX;
        s.y = Mathf.Max(0.1f, baseX * (1f + height * stretchPerHeight));
        transform.localScale = s;
    }
}