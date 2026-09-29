using UnityEngine;

/// <summary>
/// Puts the shoot point on the muzzle of whichever direction the sprite is showing.
/// Each slot is a position around the anchor, so every shooter can place its own.
/// Up is North. Down Left is South-West.
/// </summary>
[DefaultExecutionOrder(50)]
public class DirectionalShootPoint : MonoBehaviour
{
    public Transform anchor;
    public Transform shootPoint;

    [Header("Muzzle per sprite")]
    [Tooltip("Up sprite.")]
    public Vector3 north;
    [Tooltip("Up Right sprite.")]
    public Vector3 northEast;
    [Tooltip("Right sprite.")]
    public Vector3 east;
    [Tooltip("Down Right sprite.")]
    public Vector3 southEast;
    [Tooltip("Down sprite.")]
    public Vector3 south;
    [Tooltip("Down Left sprite. South-West.")]
    public Vector3 southWest;
    [Tooltip("Left sprite.")]
    public Vector3 west;
    [Tooltip("Up Left sprite.")]
    public Vector3 northWest;

    EnemyTopDownAnimDriver anim;
    EnemyAI ai;
    Vector3 anchorFromRoot;
    bool captured;

    public Vector3 CurrentOffset => OffsetFor(Facing());

    void Awake()
    {
        anim = GetComponentInChildren<EnemyTopDownAnimDriver>();
        ai = GetComponent<EnemyAI>();
        CaptureAnchor();
    }

    void CaptureAnchor()
    {
        if (anchor == null || captured)
            return;

        anchorFromRoot = anchor.position - transform.position;
        captured = true;
    }

    void LateUpdate()
    {
        if (shootPoint == null)
            return;

        if (ai != null && !ai.longRangeAttacker)
            return;

        CaptureAnchor();
        Vector3 center = anchor != null ? transform.position + anchorFromRoot : transform.position;
        Vector3 offset = OffsetFor(Facing());
        shootPoint.position = center + offset;
    }

    Vector3 Facing()
    {
        if (anim != null && anim.SpriteFacing.sqrMagnitude > 0.0001f)
            return anim.SpriteFacing;

        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        return fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
    }

    public Vector3 OffsetFor(Vector3 facing)
    {
        switch (IndexFor(facing))
        {
            case 1: return northEast;
            case 2: return east;
            case 3: return southEast;
            case 4: return south;
            case 5: return southWest;
            case 6: return west;
            case 7: return northWest;
            default: return north;
        }
    }

    /// <summary>
    /// 0 North (Up), 1 North-East, 2 East, 3 South-East,
    /// 4 South (Down), 5 South-West, 6 West, 7 North-West.
    /// </summary>
    public static int IndexFor(Vector3 facing)
    {
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.0001f)
            return 0;

        float angle = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
        int index = Mathf.RoundToInt(angle / 45f);
        index %= 8;
        if (index < 0)
            index += 8;
        return index;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 center = anchor != null ? anchor.position : transform.position;
        Vector3[] points =
        {
            north, northEast, east, southEast, south, southWest, west, northWest
        };

        int active = Application.isPlaying ? IndexFor(Facing()) : -1;
        for (int i = 0; i < points.Length; i++)
        {
            bool current = i == active;
            Gizmos.color = current ? new Color(1f, 0.45f, 0.1f, 0.95f) : new Color(0.3f, 0.85f, 1f, 0.7f);
            Gizmos.DrawSphere(center + points[i], current ? 0.09f : 0.06f);
        }
    }
}
