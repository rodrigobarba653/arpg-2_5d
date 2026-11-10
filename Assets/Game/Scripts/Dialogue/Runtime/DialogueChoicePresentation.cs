using System;
using UnityEngine;

/// <summary>
/// Where the choice panel sits. NearPlayerChatbox is reserved for Step 7.
/// </summary>
public enum DialogueChoicePlacement
{
    LowerCenter = 0,
    NearPlayerChatbox = 1,
}

/// <summary>
/// Choice-panel placement in Canvas reference units. Does not move speaker bubbles.
/// </summary>
[Serializable]
public struct DialogueChoicePresentation
{
    public DialogueChoicePlacement placement;

    [Tooltip("Extra inset inside the dialogue safe rect, in reference units.")]
    public float marginRef;

    [Tooltip("Designer nudge, in reference units.")]
    public Vector2 offsetRef;

    public float maxWidthRef;
    public float maxHeightRef;

    [Tooltip("Step 7. Stored now, unused while placement stays LowerCenter.")]
    public float gapFromBubbleRef;

    public static DialogueChoicePresentation LowerCenterDefault => new DialogueChoicePresentation
    {
        placement = DialogueChoicePlacement.LowerCenter,
        marginRef = 24f,
        offsetRef = Vector2.zero,
        maxWidthRef = 720f,
        maxHeightRef = 420f,
        gapFromBubbleRef = 24f,
    };
}
