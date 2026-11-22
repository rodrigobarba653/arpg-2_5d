using UnityEngine;

/// <summary>
/// Lightweight narrative mover for non-player NPCs. Straight-line XZ walk,
/// eight-direction facing, Walk/Idle via Animator MoveX/MoveY/IsMoving.
/// No combat AI and no NavMesh.
///
/// Priority each frame: event scripted move → ambient drive → idle.
/// AmbientNpc / Face / Move use Seize so patrol and dialogue can pause.
/// </summary>
[DisallowMultipleComponent]
public class ScriptedNpcMotor : MonoBehaviour
{
    [Header("References")]
    public CharacterController controller;
    public Animator animator;

    [Header("Movement")]
    public float moveSpeed = 3.2f;
    public float gravity = -20f;
    public float deadZone = 0.05f;
    public bool snapTo8Directions = true;

    bool scriptedMoveActive;
    object scriptedMoveOwner;
    Vector3 scriptedDestination;
    float scriptedSpeed;
    float scriptedStopDistance;
    bool scriptedFaceMovement;
    bool scriptedArrived;

    bool ambientDriveActive;
    Vector3 ambientPlanar;
    float ambientSpeed;
    bool ambientFaceMovement;

    object eventSeizeOwner;
    Vector2 facing2D = Vector2.down;
    Vector3 verticalVelocity;
    bool animOwnedByEvent;
    object animOwner;

    static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    static readonly int MoveXHash = Animator.StringToHash("MoveX");
    static readonly int MoveYHash = Animator.StringToHash("MoveY");

    public bool IsScriptedMoving => scriptedMoveActive;
    public bool ScriptedMoveArrived => scriptedArrived;
    public object ScriptedMoveOwner => scriptedMoveOwner;
    public bool AnimationOwnedByEvent => animOwnedByEvent;

    /// <summary>True while an EventAction (or AmbientNpc helper) holds the NPC.</summary>
    public bool IsSeized => eventSeizeOwner != null;

    /// <summary>True when patrol must not drive (event seize, scripted move, or held anim).</summary>
    public bool IsEventBusy =>
        eventSeizeOwner != null || scriptedMoveActive || animOwnedByEvent;

    void Awake()
    {
        if (!controller)
            controller = GetComponent<CharacterController>();
        if (!animator)
            animator = ResolveLocomotionAnimator();
    }

    /// <summary>
    /// Prefer the child Animator that owns IsMoving/MoveX/MoveY (SpriteBody),
    /// not prompt or VFX Animators without a locomotion controller.
    /// </summary>
    Animator ResolveLocomotionAnimator()
    {
        var animators = GetComponentsInChildren<Animator>(true);
        Animator fallback = null;
        for (int i = 0; i < animators.Length; i++)
        {
            var a = animators[i];
            if (a == null || a.runtimeAnimatorController == null)
                continue;
            if (fallback == null)
                fallback = a;
            var parameters = a.parameters;
            for (int p = 0; p < parameters.Length; p++)
            {
                if (parameters[p].nameHash == IsMovingHash)
                    return a;
            }
        }

        return fallback;
    }

    void OnDisable()
    {
        ForceEndScriptedMove();
        ClearAmbientDrive();
        ForceReleaseSeize();
        ReleaseAnimationOwner(null);
    }

    void Update()
    {
        Vector3 planar = Vector3.zero;
        float speed = 0f;
        bool movingAnim = false;

        if (scriptedMoveActive)
        {
            Vector3 toDest = scriptedDestination - transform.position;
            toDest.y = 0f;
            float dist = toDest.magnitude;
            if (dist <= Mathf.Max(0.01f, scriptedStopDistance))
            {
                scriptedArrived = true;
                ForceEndScriptedMove();
            }
            else
            {
                planar = toDest / dist;
                speed = scriptedSpeed > 0f ? scriptedSpeed : moveSpeed;
                if (scriptedFaceMovement)
                    SetFacingFromWorld(planar);
                movingAnim = true;
            }
        }
        else if (ambientDriveActive && !IsEventBusy)
        {
            planar = ambientPlanar;
            speed = ambientSpeed > 0f ? ambientSpeed : moveSpeed;
            if (ambientFaceMovement && planar.sqrMagnitude > 0.0001f)
                SetFacingFromWorld(planar);
            movingAnim = planar.sqrMagnitude > deadZone * deadZone;
        }

        if (controller != null && controller.enabled)
        {
            if (controller.isGrounded && verticalVelocity.y < 0f)
                verticalVelocity.y = -2f;
            verticalVelocity.y += gravity * Time.deltaTime;

            Vector3 step = planar * speed;
            step.y = verticalVelocity.y;
            controller.Move(step * Time.deltaTime);
        }
        else if (planar.sqrMagnitude > 0.0001f && speed > 0f)
        {
            transform.position += planar * (speed * Time.deltaTime);
        }

        if (!animOwnedByEvent)
            WriteLocomotionAnim(movingAnim && planar.sqrMagnitude > deadZone * deadZone);
    }

    /// <summary>
    /// Event / cutscene hold. Pauses ambient patrol (and AmbientNpc can suspend dialogue).
    /// </summary>
    public bool TrySeize(object owner)
    {
        if (owner == null)
            return false;
        if (eventSeizeOwner != null && !ReferenceEquals(eventSeizeOwner, owner))
            return false;

        eventSeizeOwner = owner;
        ClearAmbientDrive();
        return true;
    }

    public void ReleaseSeize(object owner)
    {
        if (eventSeizeOwner == null)
            return;
        if (owner != null && !ReferenceEquals(eventSeizeOwner, owner))
            return;
        eventSeizeOwner = null;
    }

    void ForceReleaseSeize()
    {
        eventSeizeOwner = null;
    }

    /// <summary>
    /// Soft ambient walk used by AmbientNpc patrol. Ignored while event-busy.
    /// </summary>
    public void SetAmbientDrive(Vector3 planarDir, float speed, bool faceMovement)
    {
        if (IsEventBusy)
        {
            ClearAmbientDrive();
            return;
        }

        planarDir.y = 0f;
        if (planarDir.sqrMagnitude < 0.0001f)
        {
            ClearAmbientDrive();
            return;
        }

        ambientDriveActive = true;
        ambientPlanar = planarDir.normalized;
        ambientSpeed = speed > 0f ? speed : moveSpeed;
        ambientFaceMovement = faceMovement;
    }

    public void ClearAmbientDrive()
    {
        ambientDriveActive = false;
        ambientPlanar = Vector3.zero;
        ambientSpeed = 0f;
    }

    public bool TryBeginScriptedMove(
        Vector3 worldDestination,
        float speed,
        float stopDistance,
        bool faceMovement,
        object owner)
    {
        if (owner == null)
            return false;
        if (scriptedMoveActive && !ReferenceEquals(scriptedMoveOwner, owner))
            return false;
        if (eventSeizeOwner != null && !ReferenceEquals(eventSeizeOwner, owner))
            return false;

        // Event move implies seize so patrol stays paused for the whole action.
        if (eventSeizeOwner == null)
            eventSeizeOwner = owner;

        ClearAmbientDrive();

        scriptedMoveActive = true;
        scriptedMoveOwner = owner;
        scriptedDestination = worldDestination;
        scriptedSpeed = speed > 0f ? speed : moveSpeed;
        scriptedStopDistance = Mathf.Max(0.01f, stopDistance);
        scriptedFaceMovement = faceMovement;
        scriptedArrived = false;

        Vector3 toDest = scriptedDestination - transform.position;
        toDest.y = 0f;
        if (toDest.sqrMagnitude > 0.0001f && faceMovement)
            SetFacingFromWorld(toDest.normalized);

        return true;
    }

    public void EndScriptedMove(object owner)
    {
        if (!scriptedMoveActive)
            return;
        if (owner != null && !ReferenceEquals(scriptedMoveOwner, owner))
            return;
        ForceEndScriptedMove();
    }

    void ForceEndScriptedMove()
    {
        scriptedMoveActive = false;
        scriptedMoveOwner = null;
        if (!animOwnedByEvent)
            WriteLocomotionAnim(false);
    }

    public void FaceWorldDirection(Vector3 worldDir, bool lockFacing = false)
    {
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 0.0001f)
            return;
        SetFacingFromWorld(worldDir.normalized);
        WriteLocomotionAnim(false);
    }

    public void FaceWorldPoint(Vector3 worldPoint)
    {
        FaceWorldDirection(worldPoint - transform.position);
    }

    public Vector2 GetFacing2D() => facing2D;

    public bool TryBeginAnimationOwner(object owner)
    {
        if (owner == null)
            return false;
        if (animOwnedByEvent && animOwner != null && !ReferenceEquals(animOwner, owner))
            return false;
        animOwnedByEvent = true;
        animOwner = owner;
        ClearAmbientDrive();
        if (eventSeizeOwner == null)
            eventSeizeOwner = owner;
        return true;
    }

    public void ReleaseAnimationOwner(object owner)
    {
        if (owner != null && animOwner != null && !ReferenceEquals(animOwner, owner))
            return;
        animOwnedByEvent = false;
        animOwner = null;
        if (owner != null && ReferenceEquals(eventSeizeOwner, owner))
            eventSeizeOwner = null;
        WriteLocomotionAnim(false);
    }

    void SetFacingFromWorld(Vector3 worldDir)
    {
        Camera cam = Camera.main;
        Vector2 face;
        if (cam == null)
        {
            face = new Vector2(worldDir.x, worldDir.z);
        }
        else
        {
            Vector3 fwd = cam.transform.forward;
            fwd.y = 0f;
            fwd.Normalize();
            Vector3 right = cam.transform.right;
            right.y = 0f;
            right.Normalize();
            face = new Vector2(Vector3.Dot(worldDir, right), Vector3.Dot(worldDir, fwd));
        }

        if (face.sqrMagnitude < 0.0001f)
            return;
        face.Normalize();
        if (snapTo8Directions)
            face = SnapTo8(face);
        facing2D = face;
    }

    void WriteLocomotionAnim(bool moving)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;
        animator.SetBool(IsMovingHash, moving);
        animator.SetFloat(MoveXHash, facing2D.x);
        animator.SetFloat(MoveYHash, facing2D.y);
    }

    static Vector2 SnapTo8(Vector2 v)
    {
        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        float snapped = Mathf.Round(angle / 45f) * 45f;
        float rad = snapped * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }
}
