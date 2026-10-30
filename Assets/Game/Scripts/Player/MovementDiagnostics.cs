using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Temporary read-only recorder for player movement. It does not move the character.
/// Leave Enable Movement Diagnostics off until a capture is needed.
/// </summary>
[DisallowMultipleComponent]
public class MovementDiagnostics : MonoBehaviour
{
    [Tooltip("Off by default. While off, the recorder does no per-frame work beyond one boolean check.")]
    public bool enableMovementDiagnostics;

    [Header("Corner suspension")]
    [Tooltip("Airborne frames with a downward Move and almost no vertical displacement required before a report.")]
    public int suspensionFrames = 6;

    [Tooltip("Meters. Requested Move Y must be more negative than this.")]
    public float downwardRequestMeters = 0.004f;

    [Tooltip("Meters. Actual vertical displacement at or inside this band counts as stopped.")]
    public float verticalStuckMeters = 0.002f;

    [Tooltip("Frames kept in the report from before the suspension was confirmed.")]
    public int historyFrames = 24;

    [Tooltip("Frames recorded after the body starts moving vertically again.")]
    public int framesAfterRecovery = 12;

    [Header("Slope interruption")]
    [Tooltip("Degrees. A groundNormal change larger than this, while on a slope, is reported.")]
    public float normalJumpDegrees = 20f;

    [Tooltip("Actual horizontal displacement below this fraction of the requested step counts as an interruption.")]
    [Range(0.05f, 1f)]
    public float horizontalInterruptRatio = 0.35f;

    [Tooltip("Seconds. A second landing lock inside this window is reported as a repeat.")]
    public float landingLockRepeatWindow = 0.75f;

    [Tooltip("Seconds. Same slope reason will not print again until this elapses.")]
    public float slopeLogCooldown = 0.5f;

    public bool IsRecording => enableMovementDiagnostics && isActiveAndEnabled;

    const int Capacity = 96;

    FrameRecord[] ring;
    int ringHead;
    int ringCount;

    bool pendingMotor;
    bool pendingJump;
    MotorObservation pendingMotorObs;
    JumpObservation pendingJumpObs;
    int pendingMotorStamp;
    int pendingJumpStamp;
    int pendingFrame = -1;
    int stamp;
    int stampFrame = -1;

    int cornerRun;
    bool cornerLatched;
    int cornerClearRun;
    bool cornerEndPending;
    int cornerEndFrames;

    FrameRecord previous;
    bool hasPrevious;
    float lastLockRising = -999f;
    float nextDownhillLog;
    float nextGroundFlipLog;
    float nextNormalLog;
    bool loggedFailure;

    static readonly StringBuilder Builder = new StringBuilder(4096);

    public struct MotorObservation
    {
        public Vector2 input;
        public Vector3 airMoveDirection;
        public Vector3 requestedVelocity;
        public Vector3 requestedStep;
        public Vector3 actualDisplacement;
        public float verticalVelocityBefore;
        public float verticalVelocityApplied;
        public bool controllerGroundedBefore;
        public bool controllerGroundedAfter;
        public bool onSteepSlope;
        public Vector3 groundNormal;
        public float groundAngle;
        public bool probeHit;
        public string probeCollider;
        public Vector3 probePoint;
        public Vector3 probeNormal;
        public CollisionFlags flags;
        public bool movementLocked;
    }

    public struct JumpObservation
    {
        public bool isGrounded;
        public bool jumpedFromGround;
        public bool fallingPose;
        public bool landedThisFrame;
        public bool landingLock;
        public bool probeHit;
        public string probeCollider;
        public Vector3 probePoint;
        public Vector3 probeNormal;
        public float probeAngle;
        public bool probeWalkable;
        public string animatorClip;
        public float animatorNormalizedTime;
        public bool animatorInAir;
        public bool animatorIsGrounded;
        public bool animatorParamsRead;
    }

    struct FrameRecord
    {
        public int frame;
        public float time;
        public bool hasMotor;
        public bool hasJump;
        public int motorStamp;
        public int jumpStamp;
        public MotorObservation motor;
        public JumpObservation jump;
    }

    void OnEnable()
    {
        if (ring == null)
            ring = new FrameRecord[Capacity];
    }

    void LateUpdate()
    {
        if (!IsRecording)
            return;

        try
        {
            if (pendingFrame == Time.frameCount && (pendingMotor || pendingJump))
            {
                FrameRecord record = new FrameRecord
                {
                    frame = Time.frameCount,
                    time = Time.time,
                    hasMotor = pendingMotor,
                    hasJump = pendingJump,
                    motorStamp = pendingMotorStamp,
                    jumpStamp = pendingJumpStamp,
                    motor = pendingMotorObs,
                    jump = pendingJumpObs
                };
                Push(record);
                Evaluate(record);
            }

            pendingMotor = false;
            pendingJump = false;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
                Dump("MANUAL CAPTURE (F8)", historyFrames);
        }
        catch (Exception exception)
        {
            ReportOnce(exception);
        }
    }

    public void RecordMotor(MotorObservation observation)
    {
        if (!IsRecording)
            return;

        try
        {
            PreparePending();
            pendingMotor = true;
            pendingMotorObs = observation;
            pendingMotorStamp = NextStamp();
        }
        catch (Exception exception)
        {
            ReportOnce(exception);
        }
    }

    public void RecordJump(JumpObservation observation)
    {
        if (!IsRecording)
            return;

        try
        {
            PreparePending();
            pendingJump = true;
            pendingJumpObs = observation;
            pendingJumpStamp = NextStamp();
        }
        catch (Exception exception)
        {
            ReportOnce(exception);
        }
    }

    [ContextMenu("Capture Movement Snapshot")]
    public void CaptureSnapshot()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[MOVEMENT DEBUG — MANUAL CAPTURE] Enter Play mode first.", this);
            return;
        }

        Dump("MANUAL CAPTURE", historyFrames);
    }

    void PreparePending()
    {
        if (pendingFrame == Time.frameCount)
            return;

        pendingFrame = Time.frameCount;
        pendingMotor = false;
        pendingJump = false;
    }

    int NextStamp()
    {
        if (stampFrame != Time.frameCount)
        {
            stampFrame = Time.frameCount;
            stamp = 0;
        }

        stamp++;
        return stamp;
    }

    void Push(FrameRecord record)
    {
        ring[ringHead] = record;
        ringHead = (ringHead + 1) % Capacity;
        if (ringCount < Capacity)
            ringCount++;
    }

    void Evaluate(FrameRecord current)
    {
        bool corner = IsCornerFrame(current);
        if (corner)
        {
            cornerRun++;
            cornerClearRun = 0;
            cornerEndPending = false;
            cornerEndFrames = 0;
            if (!cornerLatched && cornerRun >= Mathf.Max(1, suspensionFrames))
            {
                cornerLatched = true;
                cornerEndPending = false;
                LogReport(
                    "CORNER SUSPENSION",
                    "Airborne, downward Move requested, vertical displacement stayed near zero for "
                    + cornerRun + " frames. Observation only.",
                    historyFrames);
            }
        }
        else
        {
            cornerRun = 0;
            if (cornerLatched)
            {
                cornerClearRun++;
                if (!cornerEndPending && cornerClearRun >= 3)
                {
                    cornerEndPending = true;
                    cornerEndFrames = 0;
                }
            }
        }

        if (cornerEndPending)
        {
            cornerEndFrames++;
            if (cornerEndFrames >= Mathf.Max(1, framesAfterRecovery))
            {
                cornerLatched = false;
                cornerEndPending = false;
                cornerClearRun = 0;
                LogReport(
                    "CORNER SUSPENSION ENDED",
                    "Vertical motion resumed or the airborne-stuck pattern stopped. Frames after the hang are included.",
                    framesAfterRecovery + 6);
            }
        }

        if (hasPrevious)
            EvaluateSlope(current, previous);

        previous = current;
        hasPrevious = true;
    }

    bool IsCornerFrame(FrameRecord current)
    {
        if (!current.hasMotor)
            return false;

        MotorObservation motor = current.motor;
        bool airborne = !motor.controllerGroundedAfter;
        bool requestedDown = motor.requestedStep.y < -Mathf.Abs(downwardRequestMeters);
        bool stopped = Mathf.Abs(motor.actualDisplacement.y) <= Mathf.Abs(verticalStuckMeters);
        return airborne && requestedDown && stopped;
    }

    void EvaluateSlope(FrameRecord current, FrameRecord prior)
    {
        if (!current.hasMotor || !prior.hasMotor)
            return;

        MotorObservation motor = current.motor;
        MotorObservation before = prior.motor;
        bool sloped = motor.groundAngle > 3f || before.groundAngle > 3f;
        bool moving = motor.input.sqrMagnitude > 0.02f;

        if (!cornerLatched && moving && IsDownhill(motor) && HorizontalInterrupted(motor))
        {
            if (Time.time >= nextDownhillLog)
            {
                nextDownhillLog = Time.time + slopeLogCooldown;
                LogReport(
                    "SLOPE INTERRUPTION",
                    "Downhill step was requested and actual horizontal displacement was under "
                    + (horizontalInterruptRatio * 100f).ToString("F0")
                    + "% of that step. Observation only.",
                    historyFrames);
            }
        }

        if (moving && sloped && motor.controllerGroundedAfter != before.controllerGroundedAfter)
        {
            if (Time.time >= nextGroundFlipLog)
            {
                nextGroundFlipLog = Time.time + slopeLogCooldown;
                LogReport(
                    "SLOPE INTERRUPTION",
                    "controller.isGrounded changed while the slope angle was above 3 degrees. "
                    + before.controllerGroundedAfter + " -> " + motor.controllerGroundedAfter + ".",
                    historyFrames);
            }
        }

        bool lockNow = current.hasJump && current.jump.landingLock;
        bool lockBefore = prior.hasJump && prior.jump.landingLock;
        if (sloped && lockNow && !lockBefore)
        {
            bool repeat = Time.time - lastLockRising <= landingLockRepeatWindow;
            lastLockRising = Time.time;
            LogReport(
                "SLOPE INTERRUPTION",
                repeat
                    ? "Landing lock started again within " + landingLockRepeatWindow.ToString("F2") + "s on a slope."
                    : "Landing lock started while the slope angle was above 3 degrees.",
                historyFrames);
        }

        if (moving && sloped && motor.probeHit && before.probeHit)
        {
            float turn = Vector3.Angle(before.groundNormal, motor.groundNormal);
            if (turn >= normalJumpDegrees && Time.time >= nextNormalLog)
            {
                nextNormalLog = Time.time + slopeLogCooldown;
                LogReport(
                    "SLOPE INTERRUPTION",
                    "groundNormal turned " + turn.ToString("F1") + " degrees between frames.",
                    historyFrames);
            }
        }
    }

    static bool IsDownhill(MotorObservation motor)
    {
        if (motor.groundAngle <= 3f)
            return false;

        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, motor.groundNormal);
        if (downhill.sqrMagnitude < 0.000001f)
            return false;

        Vector3 wish = new Vector3(motor.requestedVelocity.x, 0f, motor.requestedVelocity.z);
        if (wish.sqrMagnitude < 0.0001f)
            return false;

        return Vector3.Dot(wish.normalized, downhill.normalized) > 0.5f;
    }

    bool HorizontalInterrupted(MotorObservation motor)
    {
        float requested = new Vector2(motor.requestedStep.x, motor.requestedStep.z).magnitude;
        float actual = new Vector2(motor.actualDisplacement.x, motor.actualDisplacement.z).magnitude;
        return requested > 0.01f && actual < requested * horizontalInterruptRatio;
    }

    void Dump(string title, int frames)
    {
        LogReport(title, "Snapshot requested by hand. Recent frames follow.", frames);
    }

    void LogReport(string title, string reason, int frames)
    {
        Builder.Clear();
        Builder.Append("[MOVEMENT DEBUG — ").Append(title).Append("]\n");
        Builder.Append("object: ").Append(name).Append('\n');
        Builder.Append("reason: ").Append(reason).Append('\n');
        Builder.Append("thresholds: suspensionFrames=").Append(suspensionFrames);
        Builder.Append(" downwardRequestMeters=").Append(downwardRequestMeters.ToString("F4"));
        Builder.Append(" verticalStuckMeters=").Append(verticalStuckMeters.ToString("F4")).Append('\n');
        Builder.Append("Displacement is the result of the single PlayerMotor Move this frame, before any LateUpdate push.\n");
        Builder.Append("verticalVelocity after is the value applied to that Move. Move itself does not write verticalVelocity.\n");
        Builder.Append("Stamp 1 ran before stamp 2 in this frame. If J is lower than M, CheckGround ran before this Move, so jumpGrounded is from the previous Move.\n");

        if (ringCount == 0)
        {
            Builder.Append("No frames recorded yet.\n");
            Debug.Log(Builder.ToString(), this);
            return;
        }

        int count = Mathf.Clamp(frames, 1, ringCount);
        int start = ringHead - count;
        if (start < 0)
            start += Capacity;

        Builder.Append("--- ").Append(count).Append(" frames, oldest first ---\n");
        for (int i = 0; i < count; i++)
            AppendFrame(ring[(start + i) % Capacity]);

        Debug.Log(Builder.ToString(), this);
    }

    void AppendFrame(FrameRecord record)
    {
        Builder.Append("f=").Append(record.frame);
        Builder.Append(" t=").Append(record.time.ToString("F3"));
        Builder.Append(" stamp M").Append(record.hasMotor ? record.motorStamp.ToString() : "-");
        Builder.Append("/J").Append(record.hasJump ? record.jumpStamp.ToString() : "-");

        if (record.hasMotor)
        {
            MotorObservation motor = record.motor;
            bool sides = (motor.flags & CollisionFlags.Sides) != 0;
            bool below = (motor.flags & CollisionFlags.Below) != 0;
            bool above = (motor.flags & CollisionFlags.Above) != 0;
            bool downBlocked = motor.requestedStep.y < -Mathf.Abs(downwardRequestMeters)
                && Mathf.Abs(motor.actualDisplacement.y) <= Mathf.Abs(verticalStuckMeters);

            Builder.Append("\n  input=").Append(Fmt(motor.input));
            Builder.Append(" airMove=").Append(Fmt(motor.airMoveDirection));
            Builder.Append(" reqVel=").Append(Fmt(motor.requestedVelocity));
            Builder.Append(" reqStep=").Append(Fmt(motor.requestedStep));
            Builder.Append(" act=").Append(Fmt(motor.actualDisplacement));
            Builder.Append(" vy ").Append(motor.verticalVelocityBefore.ToString("F3"));
            Builder.Append(" -> ").Append(motor.verticalVelocityApplied.ToString("F3"));
            Builder.Append("\n  CC grounded before/after=").Append(Bit(motor.controllerGroundedBefore));
            Builder.Append("/").Append(Bit(motor.controllerGroundedAfter));
            Builder.Append(" steep=").Append(Bit(motor.onSteepSlope));
            Builder.Append(" angle=").Append(motor.groundAngle.ToString("F2"));
            Builder.Append(" normal=").Append(Fmt(motor.groundNormal));
            Builder.Append(" flags=").Append(motor.flags);
            Builder.Append(" sides=").Append(Bit(sides));
            Builder.Append(" below=").Append(Bit(below));
            Builder.Append(" above=").Append(Bit(above));
            Builder.Append(" downBlocked=").Append(Bit(downBlocked));
            Builder.Append(" moveLocked=").Append(Bit(motor.movementLocked));
            Builder.Append("\n  slopeProbe=").Append(motor.probeHit ? motor.probeCollider : "none");
            if (motor.probeHit)
            {
                Builder.Append(" point=").Append(Fmt(motor.probePoint));
                Builder.Append(" normal=").Append(Fmt(motor.probeNormal));
            }
        }

        if (record.hasJump)
        {
            JumpObservation jump = record.jump;
            Builder.Append("\n  jumpGrounded=").Append(Bit(jump.isGrounded));
            Builder.Append(" jumping=").Append(Bit(jump.jumpedFromGround));
            Builder.Append(" fallingPose=").Append(Bit(jump.fallingPose));
            Builder.Append(" landed=").Append(Bit(jump.landedThisFrame));
            Builder.Append(" landingLock=").Append(Bit(jump.landingLock));
            Builder.Append(" clip=").Append(string.IsNullOrEmpty(jump.animatorClip) ? "none" : jump.animatorClip);
            Builder.Append(" norm=").Append(jump.animatorNormalizedTime.ToString("F2"));
            if (jump.animatorParamsRead)
            {
                Builder.Append(" animInAir=").Append(Bit(jump.animatorInAir));
                Builder.Append(" animGrounded=").Append(Bit(jump.animatorIsGrounded));
            }

            Builder.Append("\n  jumpProbe=").Append(jump.probeHit ? jump.probeCollider : "none");
            if (jump.probeHit)
            {
                Builder.Append(" point=").Append(Fmt(jump.probePoint));
                Builder.Append(" normal=").Append(Fmt(jump.probeNormal));
                Builder.Append(" angle=").Append(jump.probeAngle.ToString("F2"));
                Builder.Append(" walkable=").Append(Bit(jump.probeWalkable));
            }
        }

        Builder.Append('\n');
    }

    static string Bit(bool value)
    {
        return value ? "1" : "0";
    }

    static string Fmt(Vector2 value)
    {
        return "(" + value.x.ToString("F3") + ", " + value.y.ToString("F3") + ")";
    }

    static string Fmt(Vector3 value)
    {
        return "(" + value.x.ToString("F3") + ", " + value.y.ToString("F3") + ", " + value.z.ToString("F3") + ")";
    }

    void ReportOnce(Exception exception)
    {
        if (loggedFailure)
            return;

        loggedFailure = true;
        Debug.LogError("[MOVEMENT DEBUG] Recorder failed and stopped this capture. Movement was not changed.\n" + exception, this);
    }
}
