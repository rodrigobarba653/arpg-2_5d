using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prevents CharacterControllers from stepping on top of each other while
/// still keeping them solid horizontally.
///
/// Place on Player and every Enemy (anything with a CharacterController that
/// shouldn't be climbable by another character).
///
/// Does two things on registered characters:
///   1) Ignores native CharacterController-vs-CharacterController collision
///      so Unity's step-offset can't lift one character on top of another.
///   2) In LateUpdate, detects horizontal overlap with other registered
///      characters and pushes them apart on the XZ plane only (Y is locked).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class CharacterPushApart : MonoBehaviour
{
    static readonly List<CharacterPushApart> All = new List<CharacterPushApart>();

    [Header("Push")]
    [Tooltip("Max separation speed (units/sec). Higher = snappier resolution.")]
    public float pushSpeed = 10f;

    [Tooltip("Extra padding added to the overlap radius (per character).")]
    public float pushPadding = 0.02f;

    [HideInInspector] public CharacterController controller;

    EnemyMotor enemyMotor;
    PlayerMotor playerMotor;
    Collider[] solidColliders;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        enemyMotor = GetComponent<EnemyMotor>();
        playerMotor = GetComponent<PlayerMotor>();

        // Cache every non-trigger collider on this character (including children).
        // We'll use these to ignore physical collision between characters while
        // leaving triggers (hitboxes/hurtboxes) intact.
        var all = GetComponentsInChildren<Collider>(true);
        var list = new List<Collider>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            if (all[i].isTrigger) continue;
            list.Add(all[i]);
        }
        solidColliders = list.ToArray();
    }

    static void IgnoreSolidPair(CharacterPushApart a, CharacterPushApart b, bool ignore)
    {
        if (a == null || b == null) return;
        if (a.solidColliders == null || b.solidColliders == null) return;

        for (int i = 0; i < a.solidColliders.Length; i++)
        {
            var ca = a.solidColliders[i];
            if (ca == null) continue;

            for (int j = 0; j < b.solidColliders.Length; j++)
            {
                var cb = b.solidColliders[j];
                if (cb == null) continue;
                Physics.IgnoreCollision(ca, cb, ignore);
            }
        }
    }

    /// <summary>
    /// Returns true if this character is currently being externally forced
    /// (knockback). A forced character should absorb 100% of the separation
    /// against a non-forced one, so the non-forced character feels like a wall
    /// and doesn't get launched.
    /// </summary>
    public bool IsBeingForced()
    {
        if (enemyMotor != null) return enemyMotor.IsInKnockback();
        if (playerMotor != null) return playerMotor.IsKnockbackActive();
        return false;
    }

    /// <summary>
    /// Returns true if this character must never be moved by the push system
    /// (e.g. Fixed-type enemies like turrets / rooted bosses).
    /// </summary>
    public bool IsImmovable()
    {
        if (enemyMotor != null && enemyMotor.moveType == EnemyMotor.EnemyMoveType.Fixed)
            return true;
        return false;
    }

    void OnEnable()
    {
        // Ignore physical collision with every already-registered character.
        // Iterates ALL non-trigger colliders on both sides so auxiliary colliders
        // (Rigidbody body, hurtbox, etc.) can't trigger step-offset climbs either.
        for (int i = 0; i < All.Count; i++)
        {
            var other = All[i];
            if (other == null) continue;
            IgnoreSolidPair(this, other, true);
        }

        All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void LateUpdate()
    {
        if (controller == null || !controller.enabled)
            return;

        // Immovable characters (Fixed-type enemies) never get pushed. Period.
        if (IsImmovable())
            return;

        Vector3 resolve = Vector3.zero;
        float myRadius = controller.radius + pushPadding;
        bool meForced = IsBeingForced();

        for (int i = 0; i < All.Count; i++)
        {
            var other = All[i];
            if (other == this || other == null) continue;
            if (other.controller == null || !other.controller.enabled) continue;

            bool otherForced = other.IsBeingForced();
            bool otherImmovable = other.IsImmovable();

            // If only the OTHER is being forced (knockback) and I'm stable, I act as a wall.
            if (otherForced && !meForced)
                continue;

            float otherRadius = other.controller.radius + other.pushPadding;
            float minDist = myRadius + otherRadius;

            Vector3 delta = transform.position - other.transform.position;
            delta.y = 0f;

            float distSqr = delta.sqrMagnitude;

            if (distSqr >= minDist * minDist)
                continue;

            float dist;
            Vector3 pushDir;

            if (distSqr < 0.0001f)
            {
                // Perfectly overlapping: pick a deterministic but varied direction
                float a = (GetInstanceID() - other.GetInstanceID()) * 0.0001f;
                pushDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                dist = 0f;
            }
            else
            {
                dist = Mathf.Sqrt(distSqr);
                pushDir = delta / dist;
            }

            float overlap = minDist - dist;

            // Push factor:
            //  - Other immovable (wall):       I take 100% of the push
            //  - I'm forced, other isn't:      I take 100% (they're a wall to me)
            //  - Otherwise (symmetric case):   share 50/50
            float pushFactor;
            if (otherImmovable || (meForced && !otherForced))
                pushFactor = 1f;
            else
                pushFactor = 0.5f;

            resolve += pushDir * overlap * pushFactor;
        }

        if (resolve.sqrMagnitude < 0.000001f)
            return;

        float maxThisFrame = pushSpeed * Time.deltaTime;
        Vector3 move = Vector3.ClampMagnitude(resolve, maxThisFrame);
        move.y = 0f;

        // CharacterController.Move applies stepOffset automatically when it
        // detects a collision, which can lift the character vertically when
        // pushed against another character. Save Y before and restore after
        // so the push remains strictly horizontal.
        float yBefore = transform.position.y;

        controller.Move(move);

        if (!Mathf.Approximately(transform.position.y, yBefore))
        {
            Vector3 p = transform.position;
            p.y = yBefore;
            transform.position = p;
        }
    }
}
