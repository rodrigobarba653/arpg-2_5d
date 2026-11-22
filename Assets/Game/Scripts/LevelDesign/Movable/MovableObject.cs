using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Generic A→B prop motion for ladders, blocking rocks, bridges, etc.
/// Drive animation from <see cref="MoveObjectAction"/>; persist with a WorldState flag.
/// Scene placement = Rest (A). Active (B) = marker or offset from Rest.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-400)]
public class MovableObject : MonoBehaviour
{
    public enum ActivePoseMode
    {
        Offset = 0,
        Marker = 1,
    }

    public enum EaseKind
    {
        SmoothStep = 0,
        Linear = 1,
        Curve = 2,
    }

    [Header("Target")]
    [Tooltip("Transform that moves. Defaults to this GameObject.")]
    public Transform movingRoot;

    [Header("Active Pose (B)")]
    public ActivePoseMode activePoseMode = ActivePoseMode.Offset;

    [Tooltip("World offset from Rest when mode = Offset.")]
    public Vector3 activePositionOffset = new Vector3(0f, -3f, 0f);

    [Tooltip("Euler offset (degrees) from Rest when mode = Offset.")]
    public Vector3 activeEulerOffset = Vector3.zero;

    [Tooltip("World pose for Active when mode = Marker.")]
    public Transform activePoseMarker;

    [Header("What Moves")]
    public bool movePosition = true;
    public bool moveRotation = false;

    [Header("Motion")]
    [Min(0f)]
    public float duration = 1.2f;

    public EaseKind ease = EaseKind.SmoothStep;

    [Tooltip("Used when Ease = Curve.")]
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Persistence")]
    [Tooltip("WorldState flag. True = Active (B), false/missing = Rest (A).")]
    public string flagId = "";

    [Tooltip("Save/load pose via WorldState. No disk write until the player saves.")]
    public bool persistState = false;

    [Tooltip("Inspector default when no saved flag exists (New Game).")]
    public bool startAtActive = false;

    [Header("Events")]
    public UnityEvent onReachedActive;
    public UnityEvent onReachedRest;

    public bool IsAtActive => isAtActive;
    public bool IsMoving => isMoving;

    Transform Root => movingRoot != null ? movingRoot : transform;

    bool posesReady;
    bool isAtActive;
    bool isMoving;
    Coroutine motionRoutine;

    Vector3 restPosition;
    Quaternion restRotation;
    Vector3 activePosition;
    Quaternion activeRotation;

    void Awake()
    {
        CacheRestFromScene();
        RebuildActivePose();
        posesReady = true;

        // Early snap so colliders / Ladder triggers match save before other Start logic.
        if (persistState && !string.IsNullOrEmpty(flagId) && WorldState.HasFlag(flagId))
            SetStateImmediate(WorldState.GetFlag(flagId), writePersist: false);
        else if (startAtActive)
            SetStateImmediate(true, writePersist: false);
        else
            ApplyPose(false);
    }

    void OnEnable()
    {
        // Restore only — do not listen to OnFlagChanged. A SetFlag in the same
        // EventRunner before Move Object would snap and skip the tween.
        WorldState.OnRestored += ApplyPersistedState;
    }

    void OnDisable()
    {
        WorldState.OnRestored -= ApplyPersistedState;
        StopMotion();
    }

    void OnValidate()
    {
        if (duration < 0f)
            duration = 0f;
        if (movingRoot == null)
            movingRoot = transform;
    }

    /// <summary>Animate toward Active (B). No-ops if already there or mid-motion to Active.</summary>
    public void MoveToActive()
    {
        EnsurePoses();
        if (isAtActive && !isMoving)
            return;
        StartMotion(toActive: true);
    }

    /// <summary>Animate toward Rest (A). No-ops if already there or mid-motion to Rest.</summary>
    public void MoveToRest()
    {
        EnsurePoses();
        if (!isAtActive && !isMoving)
            return;
        StartMotion(toActive: false);
    }

    /// <summary>Toggle between Rest and Active with animation.</summary>
    public void Toggle()
    {
        if (isAtActive)
            MoveToRest();
        else
            MoveToActive();
    }

    /// <summary>
    /// Instant pose for save restore / editor preview. No UnityEvents.
    /// </summary>
    public void SetStateImmediate(bool active, bool writePersist = true)
    {
        EnsurePoses();
        StopMotion();
        isAtActive = active;
        ApplyPose(active);
        if (writePersist)
            WritePersistedState();
    }

    /// <summary>Re-read Rest from the current transform (editor / tools).</summary>
    public void CaptureRestFromCurrent()
    {
        CacheRestFromScene();
        RebuildActivePose();
        posesReady = true;
        ApplyPose(isAtActive);
    }

    /// <summary>Bake Active from the current transform as an offset from Rest.</summary>
    public void CaptureActiveAsOffsetFromRest()
    {
        EnsurePoses();
        var root = Root;
        activePositionOffset = root.position - restPosition;
        activeEulerOffset = (root.rotation * Quaternion.Inverse(restRotation)).eulerAngles;
        activePoseMode = ActivePoseMode.Offset;
        RebuildActivePose();
        ApplyPose(isAtActive);
    }

    void ApplyPersistedState()
    {
        if (!persistState || string.IsNullOrEmpty(flagId))
            return;

        bool active = WorldState.HasFlag(flagId) && WorldState.GetFlag(flagId);
        SetStateImmediate(active, writePersist: false);
    }

    void WritePersistedState()
    {
        if (!persistState || string.IsNullOrEmpty(flagId))
            return;
        WorldState.SetFlag(flagId, isAtActive);
    }

    void StartMotion(bool toActive)
    {
        EnsurePoses();
        StopMotion();
        motionRoutine = StartCoroutine(MotionRoutine(toActive));
    }

    void StopMotion()
    {
        if (motionRoutine != null)
        {
            StopCoroutine(motionRoutine);
            motionRoutine = null;
        }
        isMoving = false;
    }

    IEnumerator MotionRoutine(bool toActive)
    {
        isMoving = true;

        var root = Root;
        Vector3 fromPos = root.position;
        Quaternion fromRot = root.rotation;
        Vector3 toPos = toActive ? activePosition : restPosition;
        Quaternion toRot = toActive ? activeRotation : restRotation;

        float seconds = Mathf.Max(0f, duration);
        if (seconds <= 0f)
        {
            ApplyPose(toActive);
            FinishMotion(toActive);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            float w = EvaluateEase(t);

            if (movePosition)
                root.position = Vector3.LerpUnclamped(fromPos, toPos, w);
            if (moveRotation)
                root.rotation = Quaternion.SlerpUnclamped(fromRot, toRot, w);

            yield return null;
        }

        ApplyPose(toActive);
        FinishMotion(toActive);
    }

    void FinishMotion(bool toActive)
    {
        isAtActive = toActive;
        isMoving = false;
        motionRoutine = null;
        WritePersistedState();

        if (toActive)
            onReachedActive?.Invoke();
        else
            onReachedRest?.Invoke();
    }

    float EvaluateEase(float t)
    {
        switch (ease)
        {
            case EaseKind.Linear:
                return t;
            case EaseKind.Curve:
                return easeCurve != null ? Mathf.Clamp01(easeCurve.Evaluate(t)) : t;
            default:
                return t * t * (3f - 2f * t);
        }
    }

    void EnsurePoses()
    {
        if (posesReady)
        {
            // Marker may have been moved in editor while playing — refresh Active only.
            if (activePoseMode == ActivePoseMode.Marker)
                RebuildActivePose();
            return;
        }

        CacheRestFromScene();
        RebuildActivePose();
        posesReady = true;
    }

    void CacheRestFromScene()
    {
        var root = Root;
        restPosition = root.position;
        restRotation = root.rotation;
    }

    void RebuildActivePose()
    {
        if (activePoseMode == ActivePoseMode.Marker && activePoseMarker != null)
        {
            activePosition = activePoseMarker.position;
            activeRotation = activePoseMarker.rotation;
            return;
        }

        activePosition = restPosition + activePositionOffset;
        activeRotation = restRotation * Quaternion.Euler(activeEulerOffset);
    }

    void ApplyPose(bool active)
    {
        var root = Root;
        if (movePosition)
            root.position = active ? activePosition : restPosition;
        if (moveRotation)
            root.rotation = active ? activeRotation : restRotation;
    }

#if UNITY_EDITOR
    public void EditorPreviewActive()
    {
        if (!posesReady)
            CacheRestFromScene();
        RebuildActivePose();
        posesReady = true;
        ApplyPose(true);
        isAtActive = true;
    }

    public void EditorPreviewRest()
    {
        if (!posesReady)
            CacheRestFromScene();
        RebuildActivePose();
        posesReady = true;
        ApplyPose(false);
        isAtActive = false;
    }

    public bool EditorPosesReady => posesReady;

    public Vector3 EditorRestPosition => posesReady ? restPosition : Root.position;

    public Vector3 EditorActivePosition
    {
        get
        {
            if (!posesReady)
            {
                if (activePoseMode == ActivePoseMode.Marker && activePoseMarker != null)
                    return activePoseMarker.position;
                return Root.position + activePositionOffset;
            }

            RebuildActivePose();
            return activePosition;
        }
    }
#endif
}
