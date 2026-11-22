using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Walks a character to a world point or marker through PlayerMotor /
/// ScriptedNpcMotor. Does not call CharacterController.Move itself.
///
/// For Ambient NPCs, Hold Position Until Event Ends keeps patrol paused after
/// arrival so the body stays put until the EventRunner finishes or another Move
/// takes over.
/// </summary>
[System.Serializable]
public class MoveCharacterAction : EventAction
{
    public enum DestinationMode
    {
        WorldPosition = 0,
        Marker = 1,
        RelativeOffset = 2,
    }

    public ScriptedCharacterTarget character = new ScriptedCharacterTarget();

    [Tooltip("Where to walk. Relative Offset = current position + offset (e.g. X=2).")]
    public DestinationMode destinationMode = DestinationMode.WorldPosition;

    [Tooltip("Used when Destination Mode = Marker. Also honored for legacy actions that only set this field.")]
    public Transform destination;

    [Tooltip("Used when Destination Mode = World Position.")]
    public Vector3 worldPosition;

    [Tooltip("Used when Destination Mode = Relative Offset. Added to the character's position at start.")]
    public Vector3 relativeOffset = new Vector3(2f, 0f, 0f);

    [Min(0.1f)]
    public float speed = 4.5f;

    [Min(0.01f)]
    public float stopDistance = 0.2f;

    [Min(0.1f)]
    public float timeoutSeconds = 8f;

    public bool faceMovementDirection = true;

    [Tooltip("NPC only. After arriving, keep patrol/dialogue paused until the EventRunner " +
             "ends or another Move Character targets this NPC. Off = resume patrol immediately.")]
    public bool holdPositionUntilEventEnds = true;

    PlayerMotor boundPlayer;
    ScriptedNpcMotor boundNpc;
    bool channelHeld;
    bool holdingAfterArrive;
    EventRunner holdRunner;

    static readonly List<MoveCharacterAction> heldByEvents = new List<MoveCharacterAction>(8);

    public override string DisplayName
    {
        get
        {
            string dest = destinationMode switch
            {
                DestinationMode.Marker => destination != null ? destination.name : "?",
                DestinationMode.RelativeOffset =>
                    $"rel ({relativeOffset.x:0.#},{relativeOffset.y:0.#},{relativeOffset.z:0.#})",
                _ => $"({worldPosition.x:0.#},{worldPosition.z:0.#})",
            };
            string hold = holdPositionUntilEventEnds ? " hold" : "";
            return $"Move Character → {dest}{hold}";
        }
    }

    public override IEnumerator Execute(EventContext ctx)
    {
        ClearLocalBindings();

        if (!character.TryResolve(out Transform body, out PlayerMotor playerMotor, out ScriptedNpcMotor npcMotor, out string failure))
        {
            ctx?.RequestStop(failure);
            yield break;
        }

        if (!TryResolveDestination(body, out Vector3 dest, out string destFail))
        {
            ctx?.RequestStop(destFail);
            yield break;
        }

        // Keep walk on the character's current ground height.
        dest.y = body.position.y;

        bool started;
        if (playerMotor != null)
        {
            started = playerMotor.TryBeginScriptedMove(dest, speed, stopDistance, faceMovementDirection, this);
            if (started)
            {
                boundPlayer = playerMotor;
                channelHeld = true;
            }
        }
        else
        {
            // Another Move in this event may still hold seize after arrive — take over.
            ReleaseHeldSeizeForMotor(npcMotor);

            if (!npcMotor.TrySeize(this))
            {
                ctx?.RequestStop("NPC is busy (another event holds it)");
                yield break;
            }

            started = npcMotor.TryBeginScriptedMove(dest, speed, stopDistance, faceMovementDirection, this);
            if (started)
            {
                boundNpc = npcMotor;
                channelHeld = true;
            }
            else
            {
                npcMotor.ReleaseSeize(this);
            }
        }

        if (!started)
        {
            ctx?.RequestStop("character move rejected (busy or combat locomotion)");
            yield break;
        }

        ctx?.Log($"Character move → ({dest.x:0.0}, {dest.z:0.0}) speed={speed:0.0} stop={stopDistance:0.00}.");

        float elapsed = 0f;
        bool arrived = false;
        try
        {
            while (true)
            {
                if (ctx != null && (ctx.IsCancelled || ctx.StopRequested))
                    yield break;

                if (boundPlayer != null)
                {
                    if (!boundPlayer.IsScriptedMoving)
                    {
                        if (boundPlayer.ScriptedMoveArrived)
                        {
                            arrived = true;
                            ctx?.Log("Character move arrived.");
                            yield break;
                        }

                        ctx?.RequestStop("character move ended without arrival");
                        yield break;
                    }
                }
                else if (boundNpc != null)
                {
                    if (!boundNpc.IsScriptedMoving)
                    {
                        if (boundNpc.ScriptedMoveArrived)
                        {
                            arrived = true;
                            ctx?.Log("Character move arrived.");
                            yield break;
                        }

                        ctx?.RequestStop("character move ended without arrival");
                        yield break;
                    }
                }
                else
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                if (elapsed >= timeoutSeconds)
                {
                    ctx?.RequestStop("character move timed out");
                    yield break;
                }

                yield return null;
            }
        }
        finally
        {
            bool keepHold = arrived &&
                            boundNpc != null &&
                            holdPositionUntilEventEnds &&
                            ctx != null &&
                            ctx.Runner != null &&
                            !ctx.IsCancelled &&
                            !ctx.StopRequested;

            if (keepHold)
                BeginHoldAfterArrive(ctx.Runner);
            else
                ReleaseChannel();
        }
    }

    public override void Cancel(EventContext ctx)
    {
        ReleaseChannel();
    }

    public static void ReleaseHeldForRunner(EventRunner runner)
    {
        if (runner == null || heldByEvents.Count == 0)
            return;

        for (int i = heldByEvents.Count - 1; i >= 0; i--)
        {
            var action = heldByEvents[i];
            if (action == null || action.holdRunner != runner)
                continue;
            action.ReleaseChannel();
        }
    }

    static void ReleaseHeldSeizeForMotor(ScriptedNpcMotor motor)
    {
        if (motor == null || heldByEvents.Count == 0)
            return;

        for (int i = heldByEvents.Count - 1; i >= 0; i--)
        {
            var action = heldByEvents[i];
            if (action == null || action.boundNpc != motor)
                continue;
            action.ReleaseChannel();
        }
    }

    void BeginHoldAfterArrive(EventRunner runner)
    {
        if (boundNpc == null)
        {
            ReleaseChannel();
            return;
        }

        // Stop walking; keep seize so AmbientNpc patrol stays paused.
        boundNpc.EndScriptedMove(this);
        holdingAfterArrive = true;
        holdRunner = runner;
        if (!heldByEvents.Contains(this))
            heldByEvents.Add(this);

        // channelHeld stays true so ReleaseChannel still clears seize later.
    }

    void ReleaseChannel()
    {
        heldByEvents.Remove(this);
        holdRunner = null;
        holdingAfterArrive = false;

        if (!channelHeld)
        {
            ClearLocalBindings();
            return;
        }

        if (boundPlayer != null)
            boundPlayer.EndScriptedMove(this);
        if (boundNpc != null)
        {
            boundNpc.EndScriptedMove(this);
            boundNpc.ReleaseSeize(this);
        }

        ClearLocalBindings();
    }

    void ClearLocalBindings()
    {
        boundPlayer = null;
        boundNpc = null;
        channelHeld = false;
        holdingAfterArrive = false;
        holdRunner = null;
    }

    /// <summary>
    /// Resolves the walk target. Legacy actions with a Destination Transform and
    /// mode still at WorldPosition keep using the marker.
    /// </summary>
    public bool TryResolveDestination(Transform body, out Vector3 dest, out string failure)
    {
        dest = default;
        failure = null;

        if (body == null)
        {
            failure = "character body missing";
            return false;
        }

        DestinationMode mode = destinationMode;
        if (mode == DestinationMode.WorldPosition && destination != null)
            mode = DestinationMode.Marker;

        switch (mode)
        {
            case DestinationMode.Marker:
                if (destination == null)
                {
                    failure = "Move Character Marker mode needs a Destination Transform";
                    return false;
                }
                dest = destination.position;
                return true;

            case DestinationMode.RelativeOffset:
                dest = body.position + relativeOffset;
                return true;

            default:
                dest = worldPosition;
                return true;
        }
    }
}
