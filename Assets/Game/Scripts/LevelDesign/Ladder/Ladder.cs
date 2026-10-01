using UnityEngine;

public enum ClimberLook
{
    North,
    NorthEast,
    NorthWest
}

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

    [Tooltip("Where the player stands on the platform after climbing off. Move this " +
             "with the ledge if the ladder gets longer. The climb-off animation walks here.")]
    public Transform platformPoint;

    [Tooltip("Where the player stands in front of the ladder after climbing all the way down.")]
    public Transform bottomStand;

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

    [Header("Climber Look")]
    [Tooltip("Which climb sprite plays. North is the front view, North East and North West are the side views.")]
    public ClimberLook climberLook = ClimberLook.North;

    [HideInInspector] public bool climberLookSet;
    [HideInInspector] public ClimbFacingDir climbFacing = ClimbFacingDir.Up;

    public Vector2 GetClimbFacing2D()
    {
        EnsureClimberLook();
        switch (climberLook)
        {
            case ClimberLook.NorthEast: return new Vector2(1f, 1f);
            case ClimberLook.NorthWest: return new Vector2(-1f, 1f);
            default:                    return new Vector2(0f, 1f);
        }
    }

    void EnsureClimberLook()
    {
        if (climberLookSet)
            return;

        switch (climbFacing)
        {
            case ClimbFacingDir.UpRight:
            case ClimbFacingDir.Right:
            case ClimbFacingDir.DownRight:
                climberLook = ClimberLook.NorthEast;
                break;
            case ClimbFacingDir.UpLeft:
            case ClimbFacingDir.Left:
            case ClimbFacingDir.DownLeft:
                climberLook = ClimberLook.NorthWest;
                break;
            default:
                climberLook = ClimberLook.North;
                break;
        }

        climberLookSet = true;
    }

    public Vector3 GetTopExitDir()
    {
        Vector3 d = (topExitDirection != null ? topExitDirection.forward : transform.forward);
        d.y = 0f;

        if (d.sqrMagnitude < 0.0001f)
            d = transform.forward;

        return d.normalized;
    }

    public Vector3 GetTopDismount()
    {
        if (platformPoint != null && topPoint != null)
            return SwingDismount(topPoint.position, platformPoint.position);
        if (platformPoint != null)
            return platformPoint.position;
        if (topPoint == null)
            return transform.position;

        return topPoint.position + SwingDirection(GetTopExitDir()) * topExitForward
               + Vector3.up * topExitLift;
    }

    public Vector3 GetBottomDismount()
    {
        if (bottomStand != null && bottomPoint != null)
            return SwingDismount(bottomPoint.position, bottomStand.position, true);
        if (bottomStand != null)
            return bottomStand.position;

        Vector3 anchor = bottomPoint != null ? bottomPoint.position : transform.position;
        return anchor - SwingDirection(GetTopExitDir()) * 0.45f;
    }

    Vector3 SwingDismount(Vector3 anchor, Vector3 stand, bool keepOutwardReach = false)
    {
        float yaw = LookYaw();
        if (Mathf.Abs(yaw) < 0.01f)
            return stand;

        Vector3 flat = new Vector3(stand.x - anchor.x, 0f, stand.z - anchor.z);
        float reach = flat.magnitude;
        flat = Quaternion.AngleAxis(yaw, Vector3.up) * flat;

        // A plain 45° turn shortens the step toward the ladder, so the
        // bottom landing still sits on the south pad. Stretch it so the
        // south reach stays and the side step is just as long.
        if (keepOutwardReach && reach > 0.01f)
        {
            float along = Mathf.Cos(Mathf.Abs(yaw) * Mathf.Deg2Rad);
            if (along > 0.01f)
                flat = flat.normalized * (reach / along);
        }

        return new Vector3(anchor.x + flat.x, stand.y, anchor.z + flat.z);
    }

    Vector3 SwingDirection(Vector3 direction)
    {
        float yaw = LookYaw();
        if (Mathf.Abs(yaw) < 0.01f)
            return direction;
        return Quaternion.AngleAxis(yaw, Vector3.up) * direction;
    }

    float LookYaw()
    {
        EnsureClimberLook();
        switch (climberLook)
        {
            case ClimberLook.NorthEast: return 45f;
            case ClimberLook.NorthWest: return -45f;
            default:                    return 0f;
        }
    }

    void Awake()
    {
        EnsureClimberLook();

        if (!ladderTrigger)
            ladderTrigger = GetComponent<Collider>();

        if (ladderTrigger != null && !ladderTrigger.isTrigger)
            Debug.LogWarning($"Ladder '{name}' collider is not a trigger. Player will collide with it.", this);
    }

    void OnValidate()
    {
        EnsureClimberLook();
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

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(topPoint.position, GetTopDismount());
        }

        if (bottomPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(bottomPoint.position, 0.15f);
        }

        if (platformPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(GetTopDismount(), 0.15f);
            if (topPoint != null)
                Gizmos.DrawLine(topPoint.position, GetTopDismount());
        }

        if (bottomStand != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(GetBottomDismount(), 0.12f);
        }
    }
}
