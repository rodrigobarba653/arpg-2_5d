using UnityEngine;

/// <summary>
/// Mirrors SpriteBody onto this renderer, but only the bottom slice of the
/// sprite (feet). Uses Sprite Lighting's feet-clip props so lighting settings
/// stay in sync with SpriteBody. Pair with WeaponPerspectiveAlignment so the
/// feet sit on the Camera → SpriteBody ray in front of the floor.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(10002)]
public class FeetBodyMirror : MonoBehaviour
{
    static readonly int FeetClipEnabledId = Shader.PropertyToID("_FeetClipEnabled");
    static readonly int FeetFractionId = Shader.PropertyToID("_FeetFraction");

    [Header("Refs")]
    [Tooltip("Source body to mirror. Defaults to parent SpriteBody renderer.")]
    [SerializeField] SpriteRenderer sourceBody;

    [Header("Crop")]
    [Tooltip("Fraction of the sprite height kept from the bottom (feet).")]
    [Range(0.05f, 0.75f)]
    [SerializeField] float bottomFraction = 0.35f;

    SpriteRenderer feetRenderer;
    MaterialPropertyBlock properties;

    void Awake()
    {
        EnsureRefs();
    }

    void OnEnable()
    {
        EnsureRefs();
        Sync();
    }

    public void Configure(SpriteRenderer source, float fraction)
    {
        sourceBody = source;
        bottomFraction = Mathf.Clamp(fraction, 0.05f, 0.75f);
        EnsureRefs();
        Sync();
    }

    void LateUpdate()
    {
        Sync();
    }

    void EnsureRefs()
    {
        if (!feetRenderer)
            feetRenderer = GetComponent<SpriteRenderer>();

        if (!sourceBody && transform.parent != null)
            sourceBody = transform.parent.GetComponent<SpriteRenderer>();

        if (properties == null)
            properties = new MaterialPropertyBlock();
    }

    void Sync()
    {
        if (!sourceBody || !feetRenderer)
            return;

        Sprite src = sourceBody.sprite;
        feetRenderer.sprite = src;
        if (src == null)
            return;

        feetRenderer.flipX = sourceBody.flipX;
        feetRenderer.flipY = sourceBody.flipY;
        feetRenderer.color = sourceBody.color;
        feetRenderer.sortingLayerID = sourceBody.sortingLayerID;
        feetRenderer.sortingOrder = sourceBody.sortingOrder + 1;
        feetRenderer.spriteSortPoint = sourceBody.spriteSortPoint;

        // Stay on SpriteBody's lighting material so SpriteLightingController
        // can push the same settings here.
        if (sourceBody.sharedMaterial != null)
            feetRenderer.sharedMaterial = sourceBody.sharedMaterial;

        // Preserve lighting MPB from SpriteLightingController, then force the
        // bottom-band clip for this overlay only.
        feetRenderer.GetPropertyBlock(properties);
        properties.SetFloat(FeetClipEnabledId, 1f);
        properties.SetFloat(FeetFractionId, bottomFraction);
        feetRenderer.SetPropertyBlock(properties);
    }
}
