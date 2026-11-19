using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Plays a scene <see cref="PlayableDirector"/> and waits until it finishes.
/// Camera policy: release this runner's CameraDirector lease before play so
/// Timeline and CameraDirector never write poses together. Timeline may drive
/// its own Cinemachine cameras; it must not bind CameraDirector event slots.
/// </summary>
[Serializable]
public class PlayTimelineAction : EventAction
{
    [Tooltip("Scene PlayableDirector that owns the Timeline asset.")]
    public PlayableDirector director;

    [Tooltip("When true (default), wait until playback ends before the next action.")]
    public bool waitUntilFinished = true;

    [Tooltip("Stop and rewind the director when the event cancels or this action fails.")]
    public bool stopOnCancel = true;

    [Tooltip("Reject Timeline wrap modes that never end (Loop).")]
    public bool rejectLooping = true;

    PlayableDirector bound;
    TimelineDialogueBridge bridge;
    bool playing;

    public override string DisplayName =>
        director != null ? $"Play Timeline '{director.name}'" : "Play Timeline (?)";

    public override IEnumerator Execute(EventContext ctx)
    {
        bound = null;
        bridge = null;
        playing = false;

        if (ctx == null || ctx.Runner == null)
        {
            ctx?.RequestStop("Play Timeline requires an EventRunner.");
            yield break;
        }

        if (director == null)
        {
            ctx.RequestStop("Play Timeline has no PlayableDirector.");
            yield break;
        }

        if (director.playableAsset == null)
        {
            ctx.RequestStop($"PlayableDirector '{director.name}' has no Timeline asset.");
            yield break;
        }

        if (rejectLooping && IsLooping(director))
        {
            ctx.RequestStop(
                $"PlayableDirector '{director.name}' wraps/loops and would never complete.");
            yield break;
        }

        if (TryFindCameraDirectorConflict(director, out string conflict))
        {
            ctx.RequestStop(conflict);
            yield break;
        }

        // Camera ownership: drop any CameraDirector lease for this runner so
        // event slots are hidden and gameplay is the baseline. Timeline may
        // then promote its own vcams. Do not Acquire a second lock owner.
        CameraDirector.Existing?.Abort(ctx.Runner);

        bridge = director.GetComponent<TimelineDialogueBridge>();
        if (bridge == null)
            bridge = director.gameObject.AddComponent<TimelineDialogueBridge>();
        bridge.Bind(ctx, director);

        bound = director;
        bound.time = 0;
        bound.Evaluate();
        bound.Play();
        playing = true;
        ctx.Log($"Timeline play '{bound.name}' duration={bound.duration:0.00}s.");

        try
        {
            if (!waitUntilFinished)
                yield break;

            const double endEpsilon = 0.0001;
            while (true)
            {
                if (ctx.IsCancelled || ctx.StopRequested)
                    yield break;

                if (bound == null)
                {
                    ctx.RequestStop("PlayableDirector was destroyed during playback.");
                    yield break;
                }

                if (HasFinished(bound, endEpsilon))
                {
                    // Continue mode may still own a dialogue after the Timeline ends.
                    // Do not Complete the event while abandoning that session.
                    while (bridge != null && bridge.OwnsRunningDialogue)
                    {
                        if (ctx.IsCancelled || ctx.StopRequested)
                            yield break;
                        yield return null;
                    }

                    if (ctx.StopRequested)
                        yield break;

                    ctx.Log($"Timeline finished '{bound.name}'.");
                    yield break;
                }

                yield return null;
            }
        }
        finally
        {
            Cleanup(ctx, interrupted: ctx != null && (ctx.IsCancelled || ctx.StopRequested));
        }
    }

    public override void Cancel(EventContext ctx)
    {
        Cleanup(ctx, interrupted: true);
    }

    void Cleanup(EventContext ctx, bool interrupted)
    {
        if (!playing && bound == null && bridge == null)
            return;

        var d = bound != null ? bound : director;
        var b = bridge;
        playing = false;
        bound = null;
        bridge = null;

        // Unbind first so owned Pause&Wait / Continue dialogue is cancelled.
        if (b != null)
            b.Unbind(cancelOwnedDialogue: interrupted || (ctx != null && ctx.StopRequested));

        if (d != null && stopOnCancel)
        {
            try
            {
                // Do NOT Evaluate at time=0 after Stop — ActivationTrack clips that
                // start at 0 would re-enable the Timeline VCam and leave it live.
                d.Stop();
                if (interrupted || (ctx != null && ctx.StopRequested))
                    DeactivateActivationBindings(d);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        if (ctx?.Runner != null)
            CameraDirector.Existing?.Abort(ctx.Runner);
    }

    /// <summary>
    /// After an interrupt, force ActivationTrack targets off so a high-priority
    /// Timeline vcam cannot keep owning the Brain.
    /// </summary>
    static void DeactivateActivationBindings(PlayableDirector d)
    {
        if (d == null || d.playableAsset is not TimelineAsset timeline)
            return;

        foreach (var track in timeline.GetOutputTracks())
        {
            if (track is not ActivationTrack)
                continue;

            var binding = d.GetGenericBinding(track);
            GameObject go = null;
            if (binding is GameObject g)
                go = g;
            else if (binding is Component c)
                go = c.gameObject;

            if (go != null && go.activeSelf)
                go.SetActive(false);
        }
    }

    static bool HasFinished(PlayableDirector d, double endEpsilon)
    {
        double duration = d.duration;

        // Reached the end (Playing or Hold/Paused at end).
        if (duration > 0 && d.time + endEpsilon >= duration)
            return true;

        // Mid-timeline Pause (DialogueCheckpoint Pause & Wait) is NOT finished.
        if (d.state == PlayState.Paused && duration > 0 && d.time + endEpsilon < duration)
            return false;

        // Stopped / destroyed graph / other non-playing states after a real stop.
        if (d.state != PlayState.Playing)
            return true;

        return false;
    }

    static bool IsLooping(PlayableDirector d)
    {
        if (d.extrapolationMode == DirectorWrapMode.Loop)
            return true;

        if (d.playableAsset is TimelineAsset timeline)
        {
            // Root clip wrap is director-level; also reject infinite duration assets.
            if (double.IsInfinity(timeline.duration))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Rejects Timeline bindings that target CameraDirector event-slot cameras.
    /// Dedicated Timeline vcams are allowed.
    /// </summary>
    public static bool TryFindCameraDirectorConflict(PlayableDirector d, out string reason)
    {
        reason = null;
        if (d == null || d.playableAsset == null)
            return false;

        var camDirector = CameraDirector.Existing ?? CameraDirector.Ensure();
        if (camDirector == null)
            return false;

        Transform host = camDirector.transform;
        if (d.playableAsset is not TimelineAsset timeline)
            return false;

        foreach (var track in timeline.GetOutputTracks())
        {
            if (track == null || track.muted)
                continue;

            var binding = d.GetGenericBinding(track);
            if (binding == null)
                continue;

            Transform boundTransform = null;
            if (binding is Component component)
                boundTransform = component.transform;
            else if (binding is GameObject go)
                boundTransform = go.transform;

            if (boundTransform == null)
                continue;

            if (boundTransform == host || boundTransform.IsChildOf(host))
            {
                reason =
                    $"Timeline '{d.name}' binds '{boundTransform.name}' under CameraDirector. " +
                    "Use a dedicated Timeline vcam, not CameraDirector event slots.";
                return true;
            }
        }

        return false;
    }
}
