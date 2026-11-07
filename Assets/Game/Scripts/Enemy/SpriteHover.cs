using UnityEngine;

/// <summary>
/// Offsets a flying enemy's sprite on X and Y so it feels like it is hovering.
/// Put this on the sprite object, not the body that moves and collides.
/// Each hop picks a new point inside the distance box and eases there.
/// </summary>
public class SpriteHover : MonoBehaviour
{
    [Tooltip("How far the sprite may drift left and right, in local units.")]
    public float distanceX = 0.08f;

    [Tooltip("How far the sprite may drift up and down, in local units.")]
    public float distanceY = 0.12f;

    [Tooltip("How fast the sprite travels, in local units per second.")]
    public float speed = 0.35f;

    Vector3 restLocal;
    Vector3 fromOffset;
    Vector3 toOffset;
    float hopDuration = 0.4f;
    float hopTime;

    void Awake()
    {
        restLocal = transform.localPosition;
        PickTarget(true);
    }

    void OnDisable()
    {
        transform.localPosition = restLocal;
    }

    void Update()
    {
        if (HitStopperManager.Frozen)
            return;

        hopTime += Time.deltaTime;
        float u = hopDuration <= 0f ? 1f : Mathf.Clamp01(hopTime / hopDuration);
        float eased = Mathf.SmoothStep(0f, 1f, u);
        transform.localPosition = restLocal + Vector3.Lerp(fromOffset, toOffset, eased);

        if (u >= 1f)
            PickTarget(false);
    }

    void PickTarget(bool first)
    {
        fromOffset = first ? Vector3.zero : toOffset;
        toOffset = new Vector3(
            Random.Range(-Mathf.Abs(distanceX), Mathf.Abs(distanceX)),
            Random.Range(-Mathf.Abs(distanceY), Mathf.Abs(distanceY)),
            0f);

        float span = Vector3.Distance(fromOffset, toOffset);
        hopDuration = Mathf.Max(0.2f, span / Mathf.Max(0.01f, speed));
        hopTime = 0f;
    }
}
