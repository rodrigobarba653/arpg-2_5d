using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Directed shots parented under the gameplay camera so they persist with it.
/// An empty host sits between them: Cinemachine ignores a virtual camera whose
/// direct parent is another virtual camera. Two slots alternate. The hidden
/// one stays at a lower priority so it can be posed before a cut. Gameplay
/// follow, look-at, priority, and Perlin are not written.
/// </summary>
public sealed class CameraDirector : MonoBehaviour
{
    public const float SettleSlackSeconds = 0.25f;
    const float BlendStartWindowSeconds = 0.5f;
    const int LivePriority = 10;
    const int StandbyPriority = 1;
    const int EventImpulseChannel = 1;
    const float EventImpulseRadius = 100000f;
    const float EventImpulsePropagation = 9999999f;
    static readonly Vector3 FallbackFollowOffset = new Vector3(0f, 4.5f, -9.3f);

    static CameraDirector instance;

    CinemachineCamera gameplay;
    CinemachineCamera slotA;
    CinemachineCamera slotB;
    CinemachineCamera liveCamera;
    CinemachineCamera stagedCamera;
    EventRunner owner;

    CinemachineVirtualCameraBase.StandbyUpdateMode savedStandby;
    bool standbyHeld;

    CinemachineBrain blendBrain;
    CinemachineBlendDefinition savedBlend;
    EventRunner blendOwner;
    bool blendOverrideActive;

    public bool LastTransitionOk { get; private set; }
    public bool LastMoveOk { get; private set; }

    int moveSerial;
    bool moveActive;
    bool moveFollow;
    bool moveAim;
    Vector3 moveFromPosition;
    Vector3 moveToPosition;
    Quaternion moveFromRotation;
    Quaternion moveToRotation;
    float moveFromFov;
    float moveToFov;
    Vector3 moveFromOffset;
    Vector3 moveToOffset;
    Transform moveFollowTarget;
    Transform moveLookAt;

    /// <summary>The directed camera currently promoted, or slot A before the first shot.</summary>
    public CinemachineCamera EventCamera => liveCamera != null ? liveCamera : slotA;

    public CinemachineCamera GameplayCamera => gameplay;

    public static CameraDirector Existing => instance;

    public static CameraDirector Ensure()
    {
        if (instance != null)
            return instance;

        var auto = Object.FindFirstObjectByType<CameraAutoTarget>();
        if (auto == null)
            return null;

        var gameplayCamera = auto.GetComponent<CinemachineCamera>();
        if (gameplayCamera == null)
            return null;

        var host = gameplayCamera.transform.Find("CameraDirector");
        var hostObject = host != null ? host.gameObject : new GameObject("CameraDirector");
        if (host == null)
            hostObject.transform.SetParent(gameplayCamera.transform, false);

        var director = hostObject.GetComponent<CameraDirector>();
        if (director == null)
            director = hostObject.AddComponent<CameraDirector>();
        director.Bind(gameplayCamera);
        return director;
    }

    public static void NotifyRunnerFinished(EventRunner runner)
    {
        if (instance == null || runner == null)
            return;
        instance.Abort(runner);
    }

    void Awake()
    {
        instance = this;
    }

    void OnDestroy()
    {
        ForceRestoreDefaultBlend();
        ReleaseStandby();
        if (instance == this)
            instance = null;
    }

    /// <summary>
    /// Broadcasts one Legacy impulse using the gameplay Perlin profile.
    /// Does not write that Perlin and does not take the camera lease.
    /// Each call builds its own definition, so a second shake cannot retune the first.
    /// </summary>
    public CinemachineImpulseManager.ImpulseEvent TryPlayEventShake(
        float amplitude, float frequency, float duration, out string reason)
    {
        if (gameplay == null)
        {
            reason = "Camera shake could not find the gameplay camera.";
            return null;
        }

        var perlin = gameplay.GetComponent<CinemachineBasicMultiChannelPerlin>();
        if (perlin == null || perlin.NoiseProfile == null)
        {
            reason = "Camera shake could not find the 6D Shake profile.";
            return null;
        }

        var definition = new CinemachineImpulseDefinition
        {
            ImpulseChannel = EventImpulseChannel,
            ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Legacy,
            RawSignal = perlin.NoiseProfile,
            AmplitudeGain = Mathf.Max(0f, amplitude),
            FrequencyGain = Mathf.Max(0.01f, frequency),
            Randomize = true,
            ImpactRadius = EventImpulseRadius,
            DissipationDistance = 0f,
            PropagationSpeed = EventImpulsePropagation,
            DirectionMode = CinemachineImpulseManager.ImpulseEvent.DirectionModes.Fixed,
            TimeEnvelope = new CinemachineImpulseManager.EnvelopeDefinition
            {
                AttackTime = 0f,
                SustainTime = Mathf.Max(0.05f, duration),
                DecayTime = 0f,
                ScaleWithImpact = false,
                HoldForever = false,
            },
        };

        reason = null;
        return definition.CreateAndReturnEvent(transform.position, Vector3.down);
    }

    public bool HasLease(EventRunner runner)
    {
        return runner != null && owner == runner;
    }

    public bool TryAcquire(EventRunner runner, out string reason)
    {
        if (runner == null)
        {
            reason = "Camera lease requires an EventRunner.";
            return false;
        }

        if (owner != null && owner != runner)
        {
            reason = "Camera is leased by another event.";
            return false;
        }

        if (gameplay == null || slotA == null || slotB == null)
        {
            reason = "Gameplay camera is missing.";
            return false;
        }

        if (FindBrain() == null)
        {
            reason = "CinemachineBrain is missing.";
            return false;
        }

        owner = runner;
        HoldStandby();
        reason = null;
        return true;
    }

    public void ShowEventCamera(Transform target)
    {
        if (target == null)
            return;

        var incoming = PickIncoming();
        if (incoming == null)
            return;

        ApplyFollow(incoming, target);
        Promote(incoming);
    }

    /// <summary>
    /// Poses the hidden slot for a cut or a blend. Returns true when that slot
    /// is already updating behind the live shot and the caller must wait one
    /// frame before promoting it. From gameplay the slot stays inactive:
    /// priority 1 would beat VCam_Main.
    /// </summary>
    public bool StageShot(CameraShotMarker marker)
    {
        stagedCamera = null;
        if (marker == null)
            return false;

        var incoming = PickIncoming();
        if (incoming == null)
            return false;

        if (marker.mode == CameraShotMarker.CameraShotMode.Follow)
            ApplyMarkerFollow(incoming, marker);
        else
            ApplyFixed(incoming, marker);
        stagedCamera = incoming;

        bool otherIsLive = liveCamera != null && liveCamera != incoming;
        if (!otherIsLive)
            return false;

        incoming.Priority = StandbyPriority;
        incoming.StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
        incoming.gameObject.SetActive(true);
        return true;
    }

    public CinemachineCamera PromoteStagedCut()
    {
        var incoming = stagedCamera;
        stagedCamera = null;
        if (incoming == null)
            return null;

        Promote(incoming);
        return incoming;
    }

    public void HideEventCamera()
    {
        InvalidateMove();
        Park(slotA);
        Park(slotB);
        liveCamera = null;
        stagedCamera = null;
    }

    /// <summary>
    /// Samples the live shot and the destination. Fixed and Follow are not
    /// mixed. A Follow move keeps the same target. Aim mode (marker rotation
    /// or LookAt) is not switched here.
    /// </summary>
    public bool TryBeginMove(EventRunner runner, CameraShotMarker destination, out string reason)
    {
        LastMoveOk = false;
        moveActive = false;
        if (!HasLease(runner))
        {
            reason = "Camera move requires the camera lease.";
            return false;
        }

        if (destination == null)
        {
            reason = "Camera move has no marker.";
            return false;
        }

        if (liveCamera == null)
        {
            reason = "Camera move has no directed camera.";
            return false;
        }

        bool liveFollow = IsFollowShot(liveCamera);
        bool destFollow = destination.mode == CameraShotMarker.CameraShotMode.Follow;
        if (liveFollow != destFollow)
        {
            reason = "Camera move cannot change Fixed/Follow. Use Cut or Blend.";
            return false;
        }

        moveFromFov = liveCamera.Lens.FieldOfView;
        moveToFov = destination.overrideFieldOfView
            ? Mathf.Max(1f, destination.fieldOfView)
            : GameplayFieldOfView();

        if (destFollow)
        {
            var destTarget = destination.followTarget != null
                ? destination.followTarget
                : destination.transform;
            if (destTarget == null || liveCamera.Follow != destTarget)
            {
                reason = "Camera move cannot change the follow target. Use Cut or Blend.";
                return false;
            }

            var destLook = destination.lookAt != null ? destination.lookAt : destTarget;
            if (liveCamera.LookAt != destLook)
            {
                reason = "Camera move cannot change LookAt. Use Cut or Blend.";
                return false;
            }

            var follow = liveCamera.GetComponent<CinemachineFollow>();
            if (follow == null || !follow.enabled)
            {
                reason = "Camera move has no follow body.";
                return false;
            }

            moveFollow = true;
            moveAim = false;
            moveFollowTarget = destTarget;
            moveLookAt = destLook;
            moveFromOffset = follow.FollowOffset;
            moveToOffset = destination.overrideFollowOffset
                ? destination.followOffset
                : GameplayFollowOffset();
        }
        else
        {
            bool liveAim = IsAiming(liveCamera);
            bool destAim = destination.lookAt != null;
            if (liveAim != destAim)
            {
                reason = "Camera move cannot switch between marker rotation and LookAt. Use Cut or Blend.";
                return false;
            }

            moveFollow = false;
            moveAim = destAim;
            moveLookAt = destination.lookAt;
            moveFollowTarget = null;
            moveFromPosition = liveCamera.transform.position;
            moveToPosition = destination.transform.position;
            moveFromRotation = liveCamera.transform.rotation;
            moveToRotation = destination.transform.rotation;
            if (moveAim)
            {
                liveCamera.LookAt = destination.lookAt;
                SetBody(liveCamera, followEnabled: false, aimEnabled: true);
            }
        }

        moveSerial++;
        moveActive = true;
        reason = null;
        return true;
    }

    public void InvalidateMove()
    {
        moveSerial++;
        moveActive = false;
    }

    public IEnumerator RunMove(EventContext ctx, float duration, bool smooth)
    {
        LastMoveOk = false;
        int serial = moveSerial;
        if (!moveActive || ctx == null)
            yield break;

        if (duration <= 0f)
        {
            if (!ApplyMove(1f, serial))
                yield break;
            yield return null;
            if (ctx.IsCancelled || serial != moveSerial)
                yield break;
            LastMoveOk = true;
            moveActive = false;
            yield break;
        }

        float start = Now();
        float end = start + duration;
        while (Now() < end)
        {
            if (ctx.IsCancelled || serial != moveSerial)
                yield break;
            float u = Mathf.Clamp01((Now() - start) / duration);
            float eased = smooth ? Mathf.SmoothStep(0f, 1f, u) : u;
            if (!ApplyMove(eased, serial))
                yield break;
            yield return null;
        }

        if (ctx.IsCancelled || serial != moveSerial)
            yield break;
        if (!ApplyMove(1f, serial))
            yield break;
        LastMoveOk = true;
        moveActive = false;
    }

    public void PushBlend(EventRunner runner, float blendSeconds)
    {
        ForceRestoreDefaultBlend();
        var brain = FindBrain();
        if (brain == null || runner == null)
            return;

        savedBlend = brain.DefaultBlend;
        var next = savedBlend;
        if (blendSeconds <= 0f)
        {
            next.Style = CinemachineBlendDefinition.Styles.Cut;
            next.Time = 0f;
        }
        else
        {
            next.Time = blendSeconds;
        }

        brain.DefaultBlend = next;
        blendBrain = brain;
        blendOwner = runner;
        blendOverrideActive = true;
    }

    /// <summary>
    /// Writes the saved DefaultBlend back. Cancel, failure, and destroy always
    /// call this. The settle loop calls it only after the transition is visible.
    /// </summary>
    public void ForceRestoreDefaultBlend()
    {
        if (!blendOverrideActive)
            return;

        if (blendBrain != null)
            blendBrain.DefaultBlend = savedBlend;

        blendOverrideActive = false;
        blendBrain = null;
        blendOwner = null;
    }

    public void Abort(EventRunner runner)
    {
        if (blendOwner == runner)
            ForceRestoreDefaultBlend();
        Release(runner);
    }

    public void Release(EventRunner runner)
    {
        if (runner == null || owner != runner)
            return;

        HideEventCamera();
        ReleaseStandby();
        owner = null;
        if (blendOwner == runner)
            ForceRestoreDefaultBlend();
    }

    public IEnumerator WaitUntilSettled(
        EventContext ctx, ICinemachineCamera expected, float blendSeconds)
    {
        LastTransitionOk = false;
        if (ctx == null)
            yield break;

        bool started = false;
        float duration = Mathf.Max(0f, blendSeconds);
        float deadline = Now() + duration + SettleSlackSeconds;
        float startDeadline = Now() + Mathf.Min(
            BlendStartWindowSeconds, duration + SettleSlackSeconds);

        // The brain applies DefaultBlend in LateUpdate. Wait one frame so a
        // restore cannot land before that transition has been created.
        yield return null;
        if (ctx.IsCancelled)
            yield break;

        while (Now() < deadline)
        {
            if (ctx.IsCancelled)
                yield break;

            var brain = blendBrain != null ? blendBrain : FindBrain();
            if (brain == null)
            {
                Fail("CinemachineBrain is missing.", expected, false, false);
                yield break;
            }

            if (!started)
            {
                bool blendingToTarget = brain.IsBlending
                    && brain.ActiveBlend != null
                    && brain.ActiveBlend.Uses(expected);
                if (blendingToTarget || IsSettled(brain, expected))
                {
                    started = true;
                    if (blendingToTarget && blendSeconds > 0.01f && brain.ActiveBlend != null)
                    {
                        float actual = brain.ActiveBlend.Duration;
                        if (Mathf.Abs(actual - blendSeconds) > 0.05f)
                        {
                            Debug.LogWarning(
                                $"[Camera] Blend duration {actual:0.00}s does not match requested {blendSeconds:0.00}s.",
                                this);
                        }
                    }

                    ForceRestoreDefaultBlend();
                }
                else if (Now() >= startDeadline)
                {
                    Fail("Blend did not start.", expected, false, brain.IsBlending);
                    yield break;
                }
            }

            if (started && IsSettled(brain, expected))
            {
                LastTransitionOk = true;
                yield break;
            }

            if (started && LostTarget(brain, expected))
            {
                Fail(
                    "Blend lost the destination.",
                    expected,
                    CinemachineCore.IsLive(expected),
                    brain.IsBlending);
                yield break;
            }

            yield return null;
        }

        var lateBrain = FindBrain();
        bool live = expected != null && CinemachineCore.IsLive(expected);
        bool blending = lateBrain != null && lateBrain.IsBlending;
        if (IsSettled(lateBrain, expected))
        {
            Debug.LogWarning(
                $"[Camera] Blend deadline fired but '{NameOf(expected)}' is live and settled. Accepting it.",
                this);
            ForceRestoreDefaultBlend();
            LastTransitionOk = true;
            yield break;
        }

        Fail("Blend timed out.", expected, live, blending);
    }

    void Fail(string reason, ICinemachineCamera expected, bool live, bool blending)
    {
        Debug.LogWarning(
            $"[Camera] {reason} expected='{NameOf(expected)}' live={live} blending={blending}. " +
            "Transition is not successful. Lease released.",
            this);
        var runner = owner;
        ForceRestoreDefaultBlend();
        Release(runner);
        LastTransitionOk = false;
    }

    CinemachineCamera PickIncoming()
    {
        if (liveCamera == slotA)
            return slotB;
        if (liveCamera == slotB)
            return slotA;
        return slotA;
    }

    void Promote(CinemachineCamera incoming)
    {
        if (incoming == null)
            return;

        incoming.Priority = LivePriority;
        incoming.StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
        incoming.gameObject.SetActive(true);

        if (liveCamera != null && liveCamera != incoming)
        {
            liveCamera.Priority = StandbyPriority;
            liveCamera.StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
        }

        liveCamera = incoming;
    }

    void ApplyFollow(CinemachineCamera cam, Transform target)
    {
        CopyChannelAndLens(cam, false, 45f);
        SetBody(cam, followEnabled: true, aimEnabled: true);
        var sourceFollow = gameplay != null ? gameplay.GetComponent<CinemachineFollow>() : null;
        var follow = cam.GetComponent<CinemachineFollow>();
        if (follow != null)
        {
            follow.FollowOffset = sourceFollow != null
                ? sourceFollow.FollowOffset
                : FallbackFollowOffset;
        }

        if (gameplay != null)
        {
            cam.transform.SetPositionAndRotation(
                gameplay.transform.position, gameplay.transform.rotation);
        }

        cam.Follow = target;
        cam.LookAt = target;
    }

    /// <summary>
    /// Follow orbits the marker target. An explicit LookAt wins. With no
    /// LookAt, the camera looks at the follow target.
    /// </summary>
    void ApplyMarkerFollow(CinemachineCamera cam, CameraShotMarker marker)
    {
        CopyChannelAndLens(cam, marker.overrideFieldOfView, marker.fieldOfView);
        var target = marker.followTarget != null ? marker.followTarget : marker.transform;
        var look = marker.lookAt != null ? marker.lookAt : target;
        SetBody(cam, followEnabled: true, aimEnabled: true);

        var follow = cam.GetComponent<CinemachineFollow>();
        if (follow != null)
        {
            if (marker.overrideFollowOffset)
                follow.FollowOffset = marker.followOffset;
            else
            {
                var sourceFollow = gameplay != null ? gameplay.GetComponent<CinemachineFollow>() : null;
                follow.FollowOffset = sourceFollow != null
                    ? sourceFollow.FollowOffset
                    : FallbackFollowOffset;
            }
        }

        cam.Follow = target;
        cam.LookAt = look;
    }

    /// <summary>
    /// LookAt wins over the marker rotation. With no LookAt, the camera keeps
    /// the marker rotation so a later move can interpolate that rotation.
    /// </summary>
    void ApplyFixed(CinemachineCamera cam, CameraShotMarker marker)
    {
        CopyChannelAndLens(cam, marker.overrideFieldOfView, marker.fieldOfView);
        bool aim = marker.lookAt != null;
        SetBody(cam, followEnabled: false, aimEnabled: aim);
        cam.Follow = null;
        cam.LookAt = aim ? marker.lookAt : null;
        cam.transform.SetPositionAndRotation(
            marker.transform.position, marker.transform.rotation);
    }

    void CopyChannelAndLens(CinemachineCamera cam, bool overrideFov, float fieldOfView)
    {
        if (gameplay == null || cam == null)
            return;

        cam.OutputChannel = gameplay.OutputChannel;
        var lens = gameplay.Lens;
        if (overrideFov)
            lens.FieldOfView = Mathf.Max(1f, fieldOfView);
        cam.Lens = lens;
    }

    static void SetBody(CinemachineCamera cam, bool followEnabled, bool aimEnabled)
    {
        var follow = cam.GetComponent<CinemachineFollow>();
        if (follow != null)
            follow.enabled = followEnabled;
        var aim = cam.GetComponent<CinemachineHardLookAt>();
        if (aim != null)
            aim.enabled = aimEnabled;
    }

    bool ApplyMove(float t, int serial)
    {
        if (serial != moveSerial || liveCamera == null)
            return false;

        if (moveFollow)
        {
            if (moveFollowTarget == null || moveLookAt == null)
            {
                Debug.LogWarning("[Camera] Move stopped. Follow target or LookAt is missing.", this);
                return false;
            }

            var follow = liveCamera.GetComponent<CinemachineFollow>();
            if (follow == null)
                return false;
            follow.FollowOffset = Vector3.Lerp(moveFromOffset, moveToOffset, t);
            SetMoveFov(t);
            return true;
        }

        if (moveAim && moveLookAt == null)
        {
            Debug.LogWarning("[Camera] Move stopped. LookAt is missing.", this);
            return false;
        }

        liveCamera.transform.position = Vector3.Lerp(moveFromPosition, moveToPosition, t);
        if (!moveAim)
            liveCamera.transform.rotation = Quaternion.Slerp(moveFromRotation, moveToRotation, t);
        SetMoveFov(t);
        return true;
    }

    void SetMoveFov(float t)
    {
        var lens = liveCamera.Lens;
        lens.FieldOfView = Mathf.Lerp(moveFromFov, moveToFov, t);
        liveCamera.Lens = lens;
    }

    float GameplayFieldOfView()
    {
        return gameplay != null ? gameplay.Lens.FieldOfView : moveFromFov;
    }

    Vector3 GameplayFollowOffset()
    {
        var source = gameplay != null ? gameplay.GetComponent<CinemachineFollow>() : null;
        return source != null ? source.FollowOffset : FallbackFollowOffset;
    }

    static bool IsFollowShot(CinemachineCamera cam)
    {
        if (cam == null || cam.Follow == null)
            return false;
        var follow = cam.GetComponent<CinemachineFollow>();
        return follow != null && follow.enabled;
    }

    static bool IsAiming(CinemachineCamera cam)
    {
        if (cam == null || cam.LookAt == null)
            return false;
        var aim = cam.GetComponent<CinemachineHardLookAt>();
        return aim != null && aim.enabled;
    }

    void Park(CinemachineCamera cam)
    {
        if (cam == null)
            return;
        cam.Priority = 0;
        cam.gameObject.SetActive(false);
    }

    static bool LostTarget(CinemachineBrain brain, ICinemachineCamera expected)
    {
        if (brain == null || expected == null)
            return true;
        if (!brain.IsBlending)
            return !CinemachineCore.IsLive(expected);
        return brain.ActiveBlend != null && !brain.ActiveBlend.Uses(expected);
    }

    static bool IsSettled(CinemachineBrain brain, ICinemachineCamera expected)
    {
        return brain != null
            && expected != null
            && !brain.IsBlending
            && CinemachineCore.IsLive(expected);
    }

    float Now()
    {
        var brain = blendBrain != null ? blendBrain : FindBrain();
        return brain != null && brain.IgnoreTimeScale ? Time.unscaledTime : Time.time;
    }

    void Bind(CinemachineCamera gameplayCamera)
    {
        gameplay = gameplayCamera;
        EnsureEventImpulseListener(gameplayCamera.gameObject);
        slotA = EnsureSlot("VCam_Event");
        slotB = EnsureSlot("VCam_Event_B");
        if (owner == null)
        {
            Park(slotA);
            Park(slotB);
        }
    }

    CinemachineCamera EnsureSlot(string slotName)
    {
        var child = transform.Find(slotName);
        var childObject = child != null ? child.gameObject : new GameObject(slotName);
        if (child == null)
            childObject.transform.SetParent(transform, false);

        var cam = childObject.GetComponent<CinemachineCamera>();
        if (cam == null)
            cam = childObject.AddComponent<CinemachineCamera>();

        cam.OutputChannel = gameplay.OutputChannel;
        cam.Lens = gameplay.Lens;
        cam.Priority = 0;

        var follow = childObject.GetComponent<CinemachineFollow>();
        if (follow == null)
            follow = childObject.AddComponent<CinemachineFollow>();
        var sourceFollow = gameplay.GetComponent<CinemachineFollow>();
        if (sourceFollow != null)
        {
            follow.TrackerSettings = sourceFollow.TrackerSettings;
            follow.FollowOffset = sourceFollow.FollowOffset;
        }

        if (childObject.GetComponent<CinemachineHardLookAt>() == null)
            childObject.AddComponent<CinemachineHardLookAt>();

        EnsureEventImpulseListener(childObject);
        return cam;
    }

    static void EnsureEventImpulseListener(GameObject host)
    {
        var listener = host.GetComponent<CinemachineImpulseListener>();
        if (listener == null)
            listener = host.AddComponent<CinemachineImpulseListener>();

        listener.ApplyAfter = CinemachineCore.Stage.Noise;
        listener.ChannelMask = EventImpulseChannel;
        listener.Gain = 1f;
        listener.Use2DDistance = false;
        listener.UseCameraSpace = true;
        listener.SignalCombinationMode = CinemachineImpulseListener.SignalCombinationModes.Additive;
        listener.ReactionSettings = new CinemachineImpulseListener.ImpulseReaction
        {
            m_SecondaryNoise = null,
            AmplitudeGain = 1f,
            FrequencyGain = 1f,
            Duration = 1f,
        };
    }

    void HoldStandby()
    {
        if (standbyHeld || gameplay == null)
            return;
        savedStandby = gameplay.StandbyUpdate;
        gameplay.StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
        standbyHeld = true;
    }

    void ReleaseStandby()
    {
        if (!standbyHeld)
            return;
        if (gameplay != null)
            gameplay.StandbyUpdate = savedStandby;
        standbyHeld = false;
    }

    static CinemachineBrain FindBrain()
    {
        int count = CinemachineBrain.ActiveBrainCount;
        CinemachineBrain fallback = null;
        for (int i = 0; i < count; i++)
        {
            var brain = CinemachineBrain.GetActiveBrain(i);
            if (brain == null || !brain.isActiveAndEnabled)
                continue;
            if (fallback == null)
                fallback = brain;
            if (brain.OutputCamera != null && brain.OutputCamera.CompareTag("MainCamera"))
                return brain;
        }

        return fallback;
    }

    static string NameOf(ICinemachineCamera camera)
    {
        return camera != null ? camera.Name : "(none)";
    }
}
