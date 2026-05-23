using UnityEngine;

/// <summary>
/// Drives an enemy along a set of waypoints when EnemyAI says it's out of
/// combat. Add to the same GameObject as EnemyAI / EnemyMotor.
///
/// The AI calls Tick() each frame while in patrol mode; this component then
/// decides whether to move toward the next waypoint, wait, or sit idle.
/// </summary>
public class EnemyPatrol : MonoBehaviour
{
    public enum Mode
    {
        Loop,      // ... 0 → 1 → 2 → 0 → 1 → 2 ...
        PingPong,  // ... 0 → 1 → 2 → 1 → 0 → 1 → 2 ...
        Once       // ... 0 → 1 → 2 (stop)
    }

    [Header("Route")]
    [Tooltip("World-space waypoints visited in order. Empty array disables patrol.")]
    public Transform[] waypoints;

    public Mode mode = Mode.Loop;

    [Header("Behavior")]
    [Tooltip("How close (XZ) the enemy must be to a waypoint to consider it reached.")]
    public float arriveDistance = 0.5f;

    [Tooltip("Seconds to pause at each waypoint before moving to the next.")]
    public float waitAtWaypoint = 1.0f;

    [Tooltip("If true, the patrol restarts from waypoint 0 whenever AI re-enters " +
             "patrol mode (after chasing the player, etc). If false, resumes from " +
             "wherever it left off.")]
    public bool restartOnResume = false;

    [Header("Gizmos")]
    public bool drawGizmos = true;
    public Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.8f);

    int currentIndex;
    int pingPongDir = 1;
    float waitTimer;
    bool finished;
    bool wasActiveLastFrame;

    public bool HasRoute => waypoints != null && waypoints.Length > 0;

    /// <summary>
    /// Drive the motor toward the current waypoint, handle the wait, and
    /// advance when reached. Called by EnemyAI each frame while in patrol mode.
    /// </summary>
    public void Tick(Transform self, EnemyMotor motor)
    {
        if (!HasRoute || motor == null)
            return;

        // Detect "just resumed patrol" — optionally restart route.
        if (!wasActiveLastFrame && restartOnResume)
        {
            currentIndex = 0;
            pingPongDir = 1;
            finished = false;
            waitTimer = 0f;
        }
        wasActiveLastFrame = true;

        if (finished)
        {
            motor.Stop();
            return;
        }

        Transform wp = GetCurrentWaypoint();
        if (wp == null)
        {
            motor.Stop();
            return;
        }

        Vector3 toWp = wp.position - self.position;
        toWp.y = 0f;
        float dist = toWp.magnitude;

        if (dist <= arriveDistance)
        {
            motor.Stop();

            // Stop the agent's path while waiting so it doesn't slide.
            if (motor.agent != null && motor.agent.enabled && motor.agent.isOnNavMesh)
                motor.agent.ResetPath();

            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
                AdvanceWaypoint();

            return;
        }

        // Pathfinding via NavMeshAgent if available, otherwise straight line.
        Vector3 moveDir = toWp.normalized;

        if (motor.agent != null && motor.agent.enabled && motor.agent.isOnNavMesh)
        {
            // Only re-issue when the destination actually changed (cheap perf).
            if ((motor.agent.destination - wp.position).sqrMagnitude > 0.01f)
                motor.agent.SetDestination(wp.position);

            Vector3 desired = motor.agent.desiredVelocity;
            desired.y = 0f;

            if (desired.sqrMagnitude > 0.001f)
                moveDir = desired.normalized;
        }

        motor.SetMoveDirection(moveDir);
    }

    /// <summary>Called by EnemyAI when patrol stops (combat starts, etc).</summary>
    public void OnPatrolPaused()
    {
        wasActiveLastFrame = false;
    }

    Transform GetCurrentWaypoint()
    {
        currentIndex = Mathf.Clamp(currentIndex, 0, waypoints.Length - 1);
        return waypoints[currentIndex];
    }

    void AdvanceWaypoint()
    {
        waitTimer = waitAtWaypoint;

        switch (mode)
        {
            case Mode.Loop:
                currentIndex = (currentIndex + 1) % waypoints.Length;
                break;

            case Mode.PingPong:
                if (waypoints.Length <= 1) { currentIndex = 0; break; }

                currentIndex += pingPongDir;

                if (currentIndex >= waypoints.Length)
                {
                    currentIndex = waypoints.Length - 2;
                    pingPongDir = -1;
                }
                else if (currentIndex < 0)
                {
                    currentIndex = 1;
                    pingPongDir = 1;
                }
                break;

            case Mode.Once:
                if (currentIndex < waypoints.Length - 1)
                    currentIndex++;
                else
                    finished = true;
                break;
        }
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || waypoints == null || waypoints.Length == 0) return;

        Gizmos.color = gizmoColor;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;

            Gizmos.DrawWireSphere(waypoints[i].position, 0.25f);

            // Lines between waypoints
            if (i < waypoints.Length - 1 && waypoints[i + 1] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }

        // Closing line for Loop / PingPong
        if (mode == Mode.Loop && waypoints.Length > 1 &&
            waypoints[0] != null && waypoints[waypoints.Length - 1] != null)
        {
            Gizmos.DrawLine(waypoints[waypoints.Length - 1].position, waypoints[0].position);
        }
    }
}
