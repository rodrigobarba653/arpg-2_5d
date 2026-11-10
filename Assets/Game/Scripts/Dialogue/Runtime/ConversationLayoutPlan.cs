using System.Collections.Generic;
using UnityEngine;

public enum ConversationSlotSide
{
    Left = 0,
    Right = 1,
    Above = 2,
    Below = 3,
}

/// <summary>
/// Per-speaker reserved region for an interactive conversation.
/// Assigned once up front; followed each frame without re-sorting.
/// </summary>
public class ConversationSpeakerSlot
{
    public string speakerId;
    public Transform anchor;
    public ConversationSlotSide side;
    public Vector2 screenAnchor0;
    public Vector2 localOffsetScreen;
    public bool isPlayer;
    public bool visible;
}

/// <summary>
/// Shared interactive layout for an entire dialogue session.
/// </summary>
public class ConversationLayoutPlan
{
    public readonly List<ConversationSpeakerSlot> speakers = new List<ConversationSpeakerSlot>(4);

    public bool TryGet(string speakerId, out ConversationSpeakerSlot slot)
    {
        slot = null;
        if (string.IsNullOrEmpty(speakerId))
            return false;
        for (int i = 0; i < speakers.Count; i++)
        {
            var s = speakers[i];
            if (s == null) continue;
            if (string.Equals(s.speakerId, speakerId, System.StringComparison.OrdinalIgnoreCase))
            {
                slot = s;
                return true;
            }
        }
        return false;
    }

    public void Clear()
    {
        speakers.Clear();
    }
}
