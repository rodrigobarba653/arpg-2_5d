using UnityEngine;

public enum ClimbFacingDir
{
    Up,
    UpRight,
    Right,
    DownRight,
    Down,
    DownLeft,
    Left,
    UpLeft
}

/// <summary>
/// Marker for a climbable ladder. Holds the top/bottom anchor points and the
/// horizontal direction the player should be pushed when exiting at the top.
///
/// Place a trigger collider on the SAME GameObject (or assign one manually
/// to ladderTrigger). The trigger is what the player overlaps to be allowed
/// to climb — the player still has to press Up to actually start climbing.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Ladder : MonoBehaviour
{
    [Header("Points")]
    [Tooltip("World position where the ladder ends at the top. When the player " +
             "reaches this Y, they exit forward onto the floor above.")]
    public Transform topPoint;

    [Tooltip("Optional bottom point. When the player descends past this Y AND is " +
             "grounded, they exit cleanly.")]
    public Transform bottomPoint;

    [Header("Exit (Top)")]
    [Tooltip("Direction (its forward axis) that the player is pushed when exiting at the top. " +
             "If null, falls back to this Ladder's transform.forward.")]
    public Transform topExitDirection;

    [Tooltip("Vertical lift applied when exiting at the top, to clear the ledge.")]
    public float topExitLift = 0.35f;

    [Tooltip("Horizontal distance the player is pushed onto the floor above.")]
    public float topExitForward = 0.5f;

    [Header("Trigger")]
    [Tooltip("Trigger collider that detects the player. Defaults to a Collider on this object.")]
    public Collider ladderTrigger;

    [Header("Visual Facing")]
    [Tooltip("Direction the player sprite faces while climbing this ladder. " +
             "Up = back to camera (typical), Down = facing camera, etc.")]
    public ClimbFacingDir climbFacing = ClimbFacingDir.Up;

    public Vector2 GetClimbFacing2D()
    {
        const float D = 0.7071068f; // sqrt(2)/2

        switch (climbFacing)
        {
            case ClimbFacingDir.Up:        return new Vector2( 0f,  1f);
            case ClimbFacingDir.UpRight:   return new Vector2( D,   D );
            case ClimbFacingDir.Right:     return new Vector2( 1f,  0f);
            case ClimbFacingDir.DownRight: return new Vector2( D,  -D );
            case ClimbFacingDir.Down:      return new Vector2( 0f, -1f);
            case ClimbFacingDir.DownLeft:  return new Vector2(-D,  -D );
            case ClimbFacingDir.Left:      return new Vector2(-1f,  0f);
            case ClimbFacingDir.UpLeft:    return new Vector2(-D,   D );
            default:                       return new Vector2( 0f,  1f);
        }
    }

    public Vector3 GetTopExitDir()
    {
        Vector3 d = (topExitDirection != null ? topExitDirection.forward : transform.forward);
        d.y = 0f;

        if (d.sqrMagnitude < 0.0001f)
            d = transform.forward;

        return d.normalized;
    }

    void Awake()
    {
        if (!ladderTrigger)
            ladderTrigger = GetComponent<Collider>();

        if (ladderTrigger != null && !ladderTrigger.isTrigger)
            Debug.LogWarning($"Ladder '{name}' collider is not a trigger. Player will collide with it.", this);
    }

    void OnTriggerEnter(Collider other)
    {
        var climb = other.GetComponentInParent<PlayerClimbing>();
        if (climb != null)
            climb.SetCandidateLadder(this, true);
    }

    void OnTriggerExit(Collider other)
    {
        var climb = other.GetComponentInParent<PlayerClimbing>();
        if (climb != null)
            climb.SetCandidateLadder(this, false);
    }

    void OnDrawGizmosSelected()
    {
        if (topPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(topPoint.position, 0.15f);

            Vector3 exitDir = GetTopExitDir();
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(topPoint.position, topPoint.position + exitDir * topExitForward);
        }

        if (bottomPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(bottomPoint.position, 0.15f);
        }
    }
}
