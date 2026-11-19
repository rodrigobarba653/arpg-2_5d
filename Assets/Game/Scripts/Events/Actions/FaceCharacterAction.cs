using System.Collections;
using UnityEngine;

/// <summary>
/// Faces a Transform or world direction. Does not leave a permanent facing lock.
/// </summary>
[System.Serializable]
public class FaceCharacterAction : EventAction
{
    public ScriptedCharacterTarget character = new ScriptedCharacterTarget();

    [Tooltip("Look at this Transform. Used when set.")]
    public Transform lookAt;

    [Tooltip("Used only when Look At is empty. World-space direction (Y ignored).")]
    public Vector3 worldDirection = Vector3.forward;

    [Tooltip("Optional hold so the facing is visible before the next action.")]
    [Min(0f)]
    public float holdSeconds = 0.15f;

    public override string DisplayName => "Face Character";

    public override IEnumerator Execute(EventContext ctx)
    {
        if (!character.TryResolve(out Transform body, out PlayerMotor playerMotor, out ScriptedNpcMotor npcMotor, out string failure))
        {
            ctx?.RequestStop(failure);
            yield break;
        }

        Vector3 dir;
        if (lookAt != null)
        {
            dir = lookAt.position - body.position;
        }
        else
        {
            dir = worldDirection;
        }

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
        {
            ctx?.RequestStop("face direction is zero");
            yield break;
        }

        if (playerMotor != null)
        {
            Vector2 face = playerMotor.WorldToFacing(dir.normalized);
            playerMotor.SetFacing(face);
            // Brief lock so TopDownAnimDriver keeps the pose while standing still.
            playerMotor.LockFacing(face);
            ctx?.Log($"Face character → ({face.x:0.00}, {face.y:0.00}).");

            float t = 0f;
            while (t < holdSeconds)
            {
                if (ctx != null && (ctx.IsCancelled || ctx.StopRequested))
                {
                    playerMotor.UnlockFacing();
                    yield break;
                }

                t += Time.deltaTime;
                yield return null;
            }

            playerMotor.UnlockFacing();
            yield break;
        }

        npcMotor.FaceWorldDirection(dir.normalized);
        ctx?.Log("Face NPC.");

        float wait = 0f;
        while (wait < holdSeconds)
        {
            if (ctx != null && (ctx.IsCancelled || ctx.StopRequested))
                yield break;
            wait += Time.deltaTime;
            yield return null;
        }
    }

    public override void Cancel(EventContext ctx)
    {
        if (character != null &&
            character.TryResolve(out _, out PlayerMotor playerMotor, out _, out _))
        {
            playerMotor?.UnlockFacing();
        }
    }
}
