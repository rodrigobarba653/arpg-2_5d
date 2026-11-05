using UnityEngine;

/// <summary>
/// Ensures a FeetBody child exists under SpriteBody for the feet-overlay experiment.
/// Creates it at runtime if the prefab child is missing (stale scene instance).
/// FeetBody uses the same Camera→SpriteBody perspective placement as WeaponBody,
/// but only draws the bottom slice of the body sprite.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(9990)]
public class FeetBodyBootstrap : MonoBehaviour
{
    [SerializeField] SpriteRenderer sourceBody;

    [Tooltip("Fraction of SpriteBody height kept as the feet overlay.")]
    [Range(0.05f, 0.75f)]
    [SerializeField] float bottomFraction = 0.35f;

    [Tooltip("Initial local Z before WeaponPerspectiveAlignment calibrates t.")]
    [SerializeField] float localZTowardCamera = -0.28f;

    void Awake()
    {
        EnsureFeetBody();
    }

    [ContextMenu("Ensure FeetBody Now")]
    public void EnsureFeetBody()
    {
        if (transform.Find("FeetBody") != null)
            return;

        if (!sourceBody)
            sourceBody = GetComponent<SpriteRenderer>();

        var go = new GameObject("FeetBody");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, localZTowardCamera);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = true;
        sr.spriteSortPoint = SpriteSortPoint.Pivot;
        if (sourceBody != null)
        {
            sr.sharedMaterial = sourceBody.sharedMaterial;
            sr.sortingLayerID = sourceBody.sortingLayerID;
            sr.sortingOrder = sourceBody.sortingOrder + 1;
        }

        go.AddComponent<WeaponPerspectiveAlignment>();

        var mirror = go.AddComponent<FeetBodyMirror>();
        mirror.Configure(sourceBody, bottomFraction);
    }
}
