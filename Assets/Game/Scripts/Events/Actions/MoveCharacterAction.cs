using System.Collections;
using UnityEngine;

/// <summary>
/// Walks a character to a world point or marker through PlayerMotor /
/// ScriptedNpcMotor. Does not call CharacterController.Move itself.
/// </summary>
[System.Serializable]
public class MoveCharacterAction : EventAction
{
    public ScriptedCharacterTarget character = new ScriptedCharacterTarget();

    [Tooltip("Destination Transform. Used when set.")]
    public Transform destination;

    [Tooltip("Used only when Destination is empty.")]
    public Vector3 worldPosition;

    [Min(0.1f)]
    public float speed = 4.5f;

    [Min(0.01f)]
    public float stopDistance = 0.2f;

    [Min(0.1f)]
    public float timeoutSeconds = 8f;

    public bool faceMovementDirection = true;

    PlayerMotor boundPlayer;
    ScriptedNpcMotor boundNpc;
    bool channelHeld;

    public override string DisplayName => "Move Character";

    public override IEnumerator Execute(EventContext ctx)
    {
        channelHeld = false;
        boundPlayer = null;
        boundNpc = null;

        if (!character.TryResolve(out Transform body, out PlayerMotor playerMotor, out ScriptedNpcMotor npcMotor, out string failure))
        {
            ctx?.RequestStop(failure);
            yield break;
        }

        Vector3 dest = destination != null ? destination.position : worldPosition;
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
            started = npcMotor.TryBeginScriptedMove(dest, speed, stopDistance, faceMovementDirection, this);
            if (started)
            {
                boundNpc = npcMotor;
                channelHeld = true;
            }
        }

        if (!started)
        {
            ctx?.RequestStop("character move rejected (busy or combat locomotion)");
            yield break;
        }

        ctx?.Log($"Character move → ({dest.x:0.0}, {dest.z:0.0}) speed={speed:0.0} stop={stopDistance:0.00}.");

        float elapsed = 0f;
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
            ReleaseChannel();
        }
    }

    public override void Cancel(EventContext ctx)
    {
        ReleaseChannel();
    }

    void ReleaseChannel()
    {
        if (!channelHeld)
            return;

        if (boundPlayer != null)
            boundPlayer.EndScriptedMove(this);
        if (boundNpc != null)
            boundNpc.EndScriptedMove(this);

        boundPlayer = null;
        boundNpc = null;
        channelHeld = false;
    }
}
