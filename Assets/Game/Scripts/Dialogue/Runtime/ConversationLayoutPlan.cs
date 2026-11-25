using System.Collections.Generic;
using UnityEngine;

public enum ConversationSlotSide
{
    /// <summary>Chatbox to the left of the speaker (tail points right).</summary>
    Left = 0,
    /// <summary>Chatbox to the right of the speaker (tail points left).</summary>
    Right = 1,
    /// <summary>Chatbox above the speaker head / speechAnchor (ambient-style).</summary>
    Above = 2,
    /// <summary>Chatbox below the speaker feet (avoids stacking on Above).</summary>
    Below = 3,
}

/// <summary>
/// Per-speaker reserved region for an interactive conversation.
/// Assigned once up front before the first line; followed without re-sorting.
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

    /// <summary>Distance from dialogue camera at plan time (CL vs FR).</summary>
    public float cameraDistance;

    /// <summary>False for a branch speaker. Rebuild looks at core slots only.</summary>
    public bool isCore = true;
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
