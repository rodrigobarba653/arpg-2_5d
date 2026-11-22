using UnityEngine;

/// <summary>
/// Narrative ambient NPC brain: patrol a waypoint route while free, pause
/// patrol (and optionally dialogue) when an EventRunner seizes the motor.
/// Requires ScriptedNpcMotor on the same object. No EnemyAI.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ScriptedNpcMotor))]
public class AmbientNpc : MonoBehaviour
{
    public enum PatrolMode
    {
        Loop = 0,
        PingPong = 1,
        Once = 2,
    }

    [Header("References")]
    public ScriptedNpcMotor motor;
    public NpcDialogueHost dialogue;

    [Header("Patrol")]
    [Tooltip("Waypoint markers. If they are children of this NPC they are detached " +
             "on play and their world positions are baked — otherwise the NPC would " +
             "chase a point that moves with it (endless straight walk).")]
    public Transform[] waypoints;

    public PatrolMode patrolMode = PatrolMode.Loop;

    [Min(0.05f)]
    public float arriveDistance = 0.4f;

    [Min(0f)]
    public float waitAtWaypoint = 1.25f;

    [Min(0.1f)]
    public float patrolSpeed = 2f;

    public bool facePatrolDirection = true;

    [Tooltip("After an Event Move releases the NPC, rebuild the square around the new position. " +
             "Off = keep the original world route.")]
    public bool rebakeRouteAroundSelfOnRelease;

    [Header("Notice Player (optional)")]
    [Tooltip("When the player enters the radius, stop patrol and face them.")]
    public bool noticePlayer;

    [Min(0.25f)]
    public float noticeRadius = 3.5f;

    [Tooltip("Extra meters before resuming patrol after the player leaves (avoids flicker).")]
    [Min(0f)]
    public float noticeExitPadding = 0.75f;

    [Header("Event seize")]
    [Tooltip("While seized by an EventAction, set NpcDialogueHost.available = false.")]
    public bool suspendDialogueWhileSeized = true;

    [Header("Gizmos")]
    public bool drawGizmos = true;
    public Color gizmoColor = new Color(0.35f, 0.85f, 1f, 0.85f);

    int currentIndex;
    int pingPongDir = 1;
    float waitTimer;
    bool finished;
    bool dialogueWasAvailable = true;
    bool dialogueSuspended;
    object localSeizeOwner;
    Vector3[] routePoints;
    Vector3[] localOffsetsFromNpc;
    bool wasEventBusy;
    bool noticingPlayer;
    Transform cachedPlayer;

    public bool HasRoute => routePoints != null && routePoints.Length > 0;
    public bool IsSeized => motor != null && motor.IsEventBusy;

    void Awake()
    {
        if (motor == null)
            motor = GetComponent<ScriptedNpcMotor>();
        if (dialogue == null)
            dialogue = GetComponent<NpcDialogueHost>();

        DetachChildWaypoints();
        BakeRoute();
    }

    void OnDisable()
    {
        motor?.ClearAmbientDrive();
        RestoreDialogue();
        if (localSeizeOwner != null && motor != null)
        {
            motor.ReleaseSeize(localSeizeOwner);
            localSeizeOwner = null;
        }
    }

    void Update()
    {
        if (motor == null)
            return;

        bool busy = motor.IsEventBusy;
        if (wasEventBusy && !busy && rebakeRouteAroundSelfOnRelease)
            RebakeAroundSelf();
        wasEventBusy = busy;

        if (busy)
        {
            motor.ClearAmbientDrive();
            ApplyDialogueSuspend(true);
            return;
        }

        ApplyDialogueSuspend(false);

        if (TickNoticePlayer())
            return;

        TickPatrol();
    }

    /// <summary>
    /// Optional: freeze patrol and look at the active party member while in range.
    /// Returns true when noticing (patrol should not run this frame).
    /// </summary>
    bool TickNoticePlayer()
    {
        if (!noticePlayer)
        {
            noticingPlayer = false;
            return false;
        }

        Transform player = ResolvePlayer();
        if (player == null)
        {
            noticingPlayer = false;
            return false;
        }

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;
        float enter = noticeRadius;
        float exit = noticeRadius + Mathf.Max(0f, noticeExitPadding);

        if (noticingPlayer)
        {
            if (dist > exit)
            {
                noticingPlayer = false;
                return false;
            }
        }
        else if (dist <= enter)
        {
            noticingPlayer = true;
        }
        else
        {
            return false;
        }

        motor.ClearAmbientDrive();
        if (toPlayer.sqrMagnitude > 0.0001f)
            motor.FaceWorldDirection(toPlayer.normalized);
        return true;
    }

    Transform ResolvePlayer()
    {
        if (cachedPlayer != null && cachedPlayer.gameObject.activeInHierarchy)
            return cachedPlayer;

        var active = Party.Active;
        if (active != null)
        {
            cachedPlayer = active.transform;
            return cachedPlayer;
        }

        var tagged = GameObject.FindWithTag("Player");
        cachedPlayer = tagged != null ? tagged.transform : null;
        return cachedPlayer;
    }

    /// <summary>
    /// Hold this NPC for a cutscene (pauses patrol; optionally dialogue).
    /// Prefer letting Move/Face call motor.TrySeize — this is for custom actions.
    /// </summary>
    public bool TrySeize(object owner)
    {
        if (motor == null || owner == null)
            return false;
        if (!motor.TrySeize(owner))
            return false;
        localSeizeOwner = owner;
        ApplyDialogueSuspend(true);
        return true;
    }

    public void Release(object owner)
    {
        if (motor == null)
            return;
        motor.ReleaseSeize(owner);
        if (localSeizeOwner != null && ReferenceEquals(localSeizeOwner, owner))
            localSeizeOwner = null;

        if (rebakeRouteAroundSelfOnRelease)
            RebakeAroundSelf();
        // Dialogue resumes next Update when not busy.
    }

    /// <summary>
    /// If PatrolRoute / WPs live under this NPC, unparent them so they stay
    /// world-fixed while the body walks.
    /// </summary>
    void DetachChildWaypoints()
    {
        if (waypoints == null)
            return;

        Transform self = transform;
        for (int i = 0; i < waypoints.Length; i++)
        {
            Transform wp = waypoints[i];
            if (wp == null)
                continue;
            if (!wp.IsChildOf(self))
                continue;

            // Prefer lifting the shared PatrolRoute parent once.
            Transform lift = wp.parent != null && wp.parent != self && wp.parent.IsChildOf(self)
                ? wp.parent
                : wp;
            if (lift.parent != null && lift.IsChildOf(self))
                lift.SetParent(null, true);
        }
    }

    void BakeRoute()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            routePoints = null;
            localOffsetsFromNpc = null;
            return;
        }

        var points = new System.Collections.Generic.List<Vector3>(waypoints.Length);
        var offsets = new System.Collections.Generic.List<Vector3>(waypoints.Length);
        Vector3 origin = transform.position;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null)
                continue;
            Vector3 p = waypoints[i].position;
            p.y = origin.y;
            points.Add(p);
            Vector3 off = p - origin;
            off.y = 0f;
            offsets.Add(off);
        }

        routePoints = points.ToArray();
        localOffsetsFromNpc = offsets.ToArray();
        currentIndex = 0;
        pingPongDir = 1;
        finished = false;
        waitTimer = 0f;
    }

    void RebakeAroundSelf()
    {
        if (localOffsetsFromNpc == null || localOffsetsFromNpc.Length == 0)
        {
            BakeRoute();
            return;
        }

        Vector3 origin = transform.position;
        routePoints = new Vector3[localOffsetsFromNpc.Length];
        for (int i = 0; i < localOffsetsFromNpc.Length; i++)
        {
            Vector3 p = origin + localOffsetsFromNpc[i];
            p.y = origin.y;
            routePoints[i] = p;
            if (waypoints != null && i < waypoints.Length && waypoints[i] != null)
                waypoints[i].position = p;
        }

        currentIndex = 0;
        pingPongDir = 1;
        finished = false;
        waitTimer = 0f;
    }

    void TickPatrol()
    {
        if (!HasRoute || finished)
        {
            motor.ClearAmbientDrive();
            return;
        }

        currentIndex = Mathf.Clamp(currentIndex, 0, routePoints.Length - 1);
        Vector3 target = routePoints[currentIndex];
        Vector3 toWp = target - transform.position;
        toWp.y = 0f;
        float dist = toWp.magnitude;

        if (dist <= arriveDistance)
        {
            motor.ClearAmbientDrive();
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
                AdvanceWaypoint();
            return;
        }

        motor.SetAmbientDrive(toWp.normalized, patrolSpeed, facePatrolDirection);
    }

    void AdvanceWaypoint()
    {
        waitTimer = waitAtWaypoint;
        int count = routePoints.Length;

        switch (patrolMode)
        {
            case PatrolMode.Loop:
                currentIndex = (currentIndex + 1) % count;
                break;

            case PatrolMode.PingPong:
                if (count <= 1)
                {
                    currentIndex = 0;
                    break;
                }

                currentIndex += pingPongDir;
                if (currentIndex >= count)
                {
                    currentIndex = count - 2;
                    pingPongDir = -1;
                }
                else if (currentIndex < 0)
                {
                    currentIndex = 1;
                    pingPongDir = 1;
                }
                break;

            case PatrolMode.Once:
                if (currentIndex >= count - 1)
                {
                    finished = true;
                    motor.ClearAmbientDrive();
                }
                else
                {
                    currentIndex++;
                }
                break;
        }
    }

    void ApplyDialogueSuspend(bool suspend)
    {
        if (!suspendDialogueWhileSeized || dialogue == null)
            return;

        if (suspend)
        {
            if (!dialogueSuspended)
            {
                dialogueWasAvailable = dialogue.available;
                dialogue.available = false;
                dialogueSuspended = true;
            }
        }
        else
        {
            RestoreDialogue();
        }
    }

    void RestoreDialogue()
    {
        if (!dialogueSuspended || dialogue == null)
            return;
        dialogue.available = dialogueWasAvailable;
        dialogueSuspended = false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        if (noticePlayer)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.25f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, noticeRadius);
            if (noticeExitPadding > 0f)
            {
                Gizmos.color = new Color(1f, 0.85f, 0.25f, 0.15f);
                Gizmos.DrawWireSphere(transform.position, noticeRadius + noticeExitPadding);
            }
        }

        Gizmos.color = gizmoColor;
        if (Application.isPlaying && routePoints != null && routePoints.Length > 0)
        {
            for (int i = 0; i < routePoints.Length; i++)
            {
                Gizmos.DrawWireSphere(routePoints[i], 0.25f);
                if (i < routePoints.Length - 1)
                    Gizmos.DrawLine(routePoints[i], routePoints[i + 1]);
            }

            if (patrolMode == PatrolMode.Loop && routePoints.Length > 1)
                Gizmos.DrawLine(routePoints[routePoints.Length - 1], routePoints[0]);
            return;
        }

        if (waypoints == null || waypoints.Length == 0)
            return;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null)
                continue;
            Gizmos.DrawWireSphere(waypoints[i].position, 0.25f);
            if (i < waypoints.Length - 1 && waypoints[i + 1] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }

        if (patrolMode == PatrolMode.Loop && waypoints.Length > 1 &&
            waypoints[0] != null && waypoints[waypoints.Length - 1] != null)
        {
            Gizmos.DrawLine(waypoints[waypoints.Length - 1].position, waypoints[0].position);
        }
    }
#endif
}
