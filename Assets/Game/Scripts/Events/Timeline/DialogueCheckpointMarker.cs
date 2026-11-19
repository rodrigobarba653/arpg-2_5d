using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Timeline marker that asks <see cref="TimelineDialogueBridge"/> to start an
/// existing <see cref="DialogueAsset"/> (Pause &amp; Wait or Continue).
/// </summary>
[Serializable]
[CustomStyle("SignalEmitter")]
public class DialogueCheckpointMarker : Marker, INotification, INotificationOptionProvider
{
    public enum Mode
    {
        PauseAndWait = 0,
        Continue = 1,
    }

    [Tooltip("Existing dialogue asset. Uses DialogueService — no second dialogue engine.")]
    public DialogueAsset dialogueAsset;

    public Mode mode = Mode.PauseAndWait;

    [Tooltip("Passed to DialogueService.TryStartInteractive.")]
    public bool allowCancel = true;

    public DialogueProgressionMode progressionMode = DialogueProgressionMode.Manual;

    [Tooltip("Automatic mode only. Negative uses the session default.")]
    public float autoAdvanceHoldSeconds = 1.75f;

    [SerializeField]
    bool retroactive;

    [SerializeField]
    bool emitOnce = true;

    PropertyName INotification.id => new PropertyName("TOM.DialogueCheckpoint");

    NotificationFlags INotificationOptionProvider.flags =>
        (retroactive ? NotificationFlags.Retroactive : default) |
        (emitOnce ? NotificationFlags.TriggerOnce : default);
}
