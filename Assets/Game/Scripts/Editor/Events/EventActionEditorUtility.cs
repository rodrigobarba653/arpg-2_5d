using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared Inspector helpers: summaries, ping, and soft validation warnings.
/// Mirrors ParallelActions runtime rules without blocking edits.
/// </summary>
public static class EventActionEditorUtility
{
    public static string Summary(EventAction action)
    {
        if (action == null)
            return "Null";

        var leaf = DelayedStartAction.Unwrap(action);
        if (leaf == null)
            return action.DisplayName;

        if (leaf is MoveCharacterAction move)
        {
            string who = DescribeCharacter(move.character);
            string dest;
            var mode = move.destinationMode;
            if (mode == MoveCharacterAction.DestinationMode.WorldPosition && move.destination != null)
                mode = MoveCharacterAction.DestinationMode.Marker;

            switch (mode)
            {
                case MoveCharacterAction.DestinationMode.Marker:
                    dest = move.destination != null ? move.destination.name : "(no marker)";
                    break;
                case MoveCharacterAction.DestinationMode.RelativeOffset:
                    dest = $"rel ({move.relativeOffset.x:0.#},{move.relativeOffset.y:0.#},{move.relativeOffset.z:0.#})";
                    break;
                default:
                    dest = $"({move.worldPosition.x:0.#},{move.worldPosition.z:0.#})";
                    break;
            }

            return $"Move {who} → {dest}";
        }

        if (leaf is FaceCharacterAction face)
        {
            string who = DescribeCharacter(face.character);
            string look = face.lookAt != null ? face.lookAt.name : "dir";
            return $"Face {who} → {look}";
        }

        if (leaf is PlayCharacterAnimationAction play)
        {
            string who = DescribeCharacter(play.character);
            return $"Play '{play.stateName}' on {who}";
        }

        if (leaf is CameraShotAction shot)
        {
            string m = shot.marker != null ? shot.marker.name : "(no marker)";
            return $"{shot.DisplayName}: {m}";
        }

        if (leaf is CameraFocusAction focus)
        {
            string t = focus.target != null ? focus.target.name : "(no target)";
            return focus.overrideFollowOffset
                ? $"Camera Focus → {t} follow+{focus.followOffset}"
                : $"Camera Focus → {t}";
        }

        if (leaf is CameraMoveAction camMove)
            return camMove.DisplayName;

        if (leaf is CameraRestoreAction)
            return "Camera Restore";

        if (leaf is CameraShakeAction)
            return "Camera Shake";

        if (leaf is GameplayFollowOffsetAction followTune)
            return followTune.DisplayName;

        if (leaf is StartDialogueAction startDialogue)
        {
            return startDialogue.dialogueAsset == null
                ? "Start Dialogue"
                : $"Start Dialogue '{startDialogue.dialogueAsset.dialogueId}'";
        }

        if (leaf is PlayTimelineAction timeline)
        {
            string n = timeline.director != null ? timeline.director.name : "(no director)";
            return $"Play Timeline → {n}";
        }

        if (leaf is ParallelActions parallel)
            return $"Parallel ({parallel.actions?.Count ?? 0})";

        if (leaf is DelayedStartAction delay)
            return delay.DisplayName;

        return leaf.DisplayName;
    }

    public static string DescribeCharacter(ScriptedCharacterTarget target)
    {
        if (target == null)
            return "?";
        if (target.source == ScriptedCharacterSource.ActivePlayer)
            return "ActivePlayer";
        if (target.explicitTransform != null)
            return target.explicitTransform.name;
        return "(no Transform)";
    }

    public static float GetStartDelay(EventAction action)
    {
        if (action is DelayedStartAction delay)
            return Mathf.Max(0f, delay.startDelay);
        return 0f;
    }

    public static void CollectWarnings(IList<EventAction> actions, List<string> into, bool insideParallel)
    {
        if (actions == null || into == null)
            return;

        int poseCount = 0;
        var moveKeys = new HashSet<string>();

        for (int i = 0; i < actions.Count; i++)
        {
            var raw = actions[i];
            if (raw == null)
            {
                into.Add($"[{i}] Null action.");
                continue;
            }

            if (raw is DelayedStartAction delay)
            {
                if (delay.startDelay < 0f)
                    into.Add($"[{i}] Start Delay is negative.");
                if (delay.action == null)
                    into.Add($"[{i}] DelayedStart has no child.");
                else if (delay.action is DelayedStartAction)
                    into.Add($"[{i}] Nested DelayedStart is not allowed.");
                else if (delay.action is ParallelActions)
                    into.Add($"[{i}] DelayedStart cannot wrap Parallel.");
            }

            var leaf = DelayedStartAction.Unwrap(raw);
            if (leaf == null)
                continue;

            if (leaf is ParallelActions nested)
            {
                if (insideParallel)
                    into.Add($"[{i}] Nested Parallel is not allowed.");
                else
                    CollectWarnings(nested.actions, into, true);
                continue;
            }

            CollectLeafRefWarnings(leaf, i, into);

            if (IsPoseAction(leaf))
                poseCount++;

            if (leaf is MoveCharacterAction move)
            {
                string key = MoveKey(move);
                if (!string.IsNullOrEmpty(key) && !moveKeys.Add(key))
                    into.Add($"[{i}] Two moves target the same character ({key}).");
            }
        }

        if (insideParallel && poseCount > 1)
            into.Add("Parallel group has more than one camera pose action.");
    }

    static void CollectLeafRefWarnings(EventAction leaf, int index, List<string> into)
    {
        if (leaf is CameraShotAction shot && shot.marker == null)
            into.Add($"[{index}] Camera Shot missing marker.");
        if (leaf is MoveCharacterAction move)
        {
            if (move.character != null &&
                move.character.source == ScriptedCharacterSource.Explicit &&
                move.character.explicitTransform == null)
                into.Add($"[{index}] Move Character missing explicit Transform.");

            var mode = move.destinationMode;
            if (mode == MoveCharacterAction.DestinationMode.WorldPosition && move.destination != null)
                mode = MoveCharacterAction.DestinationMode.Marker;

            if (mode == MoveCharacterAction.DestinationMode.Marker && move.destination == null)
                into.Add($"[{index}] Move Character Marker mode missing Destination.");
        }

        if (leaf is FaceCharacterAction face)
        {
            if (face.character != null &&
                face.character.source == ScriptedCharacterSource.Explicit &&
                face.character.explicitTransform == null)
                into.Add($"[{index}] Face Character missing explicit Transform.");
            if (face.lookAt == null && face.worldDirection.sqrMagnitude < 0.0001f)
                into.Add($"[{index}] Face Character has no lookAt and zero direction.");
        }

        if (leaf is PlayCharacterAnimationAction play)
        {
            if (string.IsNullOrEmpty(play.stateName))
                into.Add($"[{index}] Play Animation has empty state name.");
            if (play.character != null &&
                play.character.source == ScriptedCharacterSource.Explicit &&
                play.character.explicitTransform == null)
                into.Add($"[{index}] Play Animation missing explicit Transform.");
        }

        if (leaf is StartDialogueAction dialogue)
        {
            if (dialogue.dialogueAsset == null)
                into.Add($"[{index}] Start Dialogue missing Dialogue Asset.");
        }

        if (leaf is PlayTimelineAction timeline)
        {
            if (timeline.director == null)
                into.Add($"[{index}] Play Timeline missing PlayableDirector.");
            else if (timeline.director.playableAsset == null)
                into.Add($"[{index}] PlayableDirector has no Timeline asset.");
            else if (PlayTimelineAction.TryFindCameraDirectorConflict(timeline.director, out string conflict))
                into.Add($"[{index}] {conflict}");
        }
    }

    static string MoveKey(MoveCharacterAction move)
    {
        if (move?.character == null)
            return null;
        if (move.character.source == ScriptedCharacterSource.ActivePlayer)
            return "ActivePlayer";
        if (move.character.explicitTransform != null)
            return move.character.explicitTransform.GetInstanceID().ToString();
        return "Explicit(missing)";
    }

    public static bool IsPoseAction(EventAction action)
    {
        return action is CameraFocusAction
            || action is CameraShotAction
            || action is CameraMoveAction
            || action is CameraRestoreAction;
    }

    public static void PingActionRefs(EventAction action)
    {
        var leaf = DelayedStartAction.Unwrap(action);
        if (leaf == null)
            return;

        Object ping = null;
        if (leaf is CameraShotAction shot)
            ping = shot.marker;
        else if (leaf is MoveCharacterAction move)
            ping = move.destination != null
                ? (Object)move.destination
                : move.character?.explicitTransform;
        else if (leaf is FaceCharacterAction face)
            ping = face.lookAt != null
                ? (Object)face.lookAt
                : face.character?.explicitTransform;
        else if (leaf is PlayCharacterAnimationAction play)
            ping = play.character?.explicitTransform;
        else if (leaf is PlayTimelineAction timeline)
            ping = timeline.director;
        else if (leaf is StartDialogueAction dialogue)
            ping = dialogue.npcAnchor != null
                ? (Object)dialogue.npcAnchor
                : dialogue.dialogueAsset;

        if (ping != null)
            EditorGUIUtility.PingObject(ping);
    }

    public static string FormatWarnings(List<string> warnings)
    {
        if (warnings == null || warnings.Count == 0)
            return null;
        var sb = new StringBuilder();
        int max = Mathf.Min(warnings.Count, 8);
        for (int i = 0; i < max; i++)
        {
            if (i > 0)
                sb.Append('\n');
            sb.Append(warnings[i]);
        }

        if (warnings.Count > max)
            sb.Append($"\n… +{warnings.Count - max} more");
        return sb.ToString();
    }
}
