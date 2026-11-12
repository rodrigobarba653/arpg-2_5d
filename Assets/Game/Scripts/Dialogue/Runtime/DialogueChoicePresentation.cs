using System;
using UnityEngine;

/// <summary>
/// Where the choice panel sits. NearPlayerChatbox reads the player chatbox and does not move it.
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

    [Tooltip("Menu width cap, in canvas reference units.")]
    public float maxWidthRef;

    [Tooltip("Menu height cap, in canvas reference units. Extra rows scroll inside the menu.")]
    public float maxHeightRef;

    [Tooltip("Gap between the player chatbox and the menu, in reference units. NearPlayerChatbox only.")]
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
