using UnityEngine;

/// <summary>
/// Global interactive/ambient layout defaults (reference UI units @ CanvasScaler reference resolution).
/// Convert to screen pixels via <see cref="DialogueLayoutSolver.RefToScreen"/>.
/// </summary>
[System.Serializable]
public class DialogueLayoutSettings
{
    [Header("Defaults")]
    public DialogueChatboxPlacement defaultPreferred = DialogueChatboxPlacement.Auto;

    [Tooltip("Inset from each screen edge, in Canvas reference units.")]
    public float screenMarginRef = 24f;

    [Tooltip("Gap between speaker anchor and chatbox edge, in Canvas reference units.")]
    public float speakerOffsetRef = 24f;

    [Tooltip("Interactive chatbox max width in Canvas reference units.")]
    public float interactiveMaxWidthRef = 760f;

    [Tooltip("Interactive chatbox height hint in Canvas reference units.")]
    public float interactiveHeightRef = 340f;

    [Tooltip("Ambient chatbox width in Canvas reference units.")]
    public float ambientWidthRef = 540f;

    [Tooltip("Ambient chatbox height in Canvas reference units.")]
    public float ambientHeightRef = 210f;

    [Tooltip("Seconds to lerp when the chosen placement slot changes.")]
    [Min(0f)]
    public float slotTransitionSeconds = 0.15f;

    [Header("Speaker avoidance")]
    public float speakerAvoidWidthRef = 96f;
    public float speakerAvoidHeightRef = 160f;

    [Tooltip("Multiplier applied to avoid rects for the player party member (keep bubbles off Jyn/Ax).")]
    [Min(1f)]
    public float playerAvoidScale = 1.75f;

    [Tooltip("Reject a slot if it covers this fraction of the character avoid rect (0-1).")]
    [Range(0.01f, 1f)]
    public float speakerCoverRejectRatio = 0.08f;

    [Tooltip("Overlap area / bubble area above this forces a slot change (0-1).")]
    [Range(0.01f, 1f)]
    public float significantOverlapRatio = 0.18f;

    [Header("Conversation layout")]
    [Tooltip("Horizontal gap from speaker toward their reserved side (ref units).")]
    public float conversationSideGapRef = 36f;

    [Tooltip("Upward lift from speaker anchor for multi-speaker slots (ref units).")]
    public float conversationLiftRef = 48f;

    [Tooltip("Upward lift when only one speaker is in the conversation (keep box closer).")]
    public float conversationSoloLiftRef = 28f;

    [Tooltip("Rebuild plan if any speaker's screen X order flips and moves more than this (ref units).")]
    public float planRebuildDeltaRef = 160f;

    [Header("Choice menu")]
    [Tooltip("Default choice-menu placement. LowerCenter unless an asset overrides it.")]
    public DialogueChoicePresentation choicePresentation = DialogueChoicePresentation.LowerCenterDefault;
}
