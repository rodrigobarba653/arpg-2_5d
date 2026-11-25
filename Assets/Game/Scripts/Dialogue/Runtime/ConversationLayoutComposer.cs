using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pre-computes interactive conversation slots from speaker screen positions + first-speak order.
/// Placement is relative to each NPC (Above / Below / Left / Right), like ambient — not screen columns.
/// </summary>
public static class ConversationLayoutComposer
{
    const float HorizontalBias = 1.25f;
    const float VerticalBias = 1.25f;
    const float SameRowYFrac = 0.22f; // of bubble height — "aligned horizontally"
    const float SameColXFrac = 0.28f;

    public struct Metrics
    {
        public float sideGap;
        public float lift;
        public float soloLift;
        public float belowGap;
        public float width;
        public float height;
        public Rect safe;
        public float sideSpaceMin;
    }

    public static void Compose(
        ConversationLayoutPlan plan,
        IList<ConversationSpeakerSlot> drafts,
        IList<string> speakOrder,
        Metrics m)
    {
        if (plan == null)
            return;
        plan.Clear();
        if (drafts == null || drafts.Count == 0)
            return;

        var list = new List<Work>(drafts.Count);
        for (int i = 0; i < drafts.Count; i++)
        {
            var d = drafts[i];
            if (d == null || string.IsNullOrEmpty(d.speakerId))
                continue;
            list.Add(new Work
            {
                id = d.speakerId,
                anchor = d.anchor,
                screen = d.screenAnchor0,
                depth = d.cameraDistance,
                isPlayer = d.isPlayer,
                speakIndex = SpeakIndex(speakOrder, d.speakerId),
            });
        }

        if (list.Count == 0)
            return;

        list.Sort((a, b) =>
        {
            int c = a.speakIndex.CompareTo(b.speakIndex);
            return c != 0 ? c : a.screen.x.CompareTo(b.screen.x);
        });

        if (list.Count == 1)
        {
            Commit(plan, list[0], ConversationSlotSide.Above, new Vector2(0f, m.soloLift));
            return;
        }

        if (list.Count == 2)
        {
            AssignPair(plan, list[0], list[1], m);
            return;
        }

        if (list.Count == 3)
        {
            AssignTrio(plan, list, m);
            return;
        }

        if (list.Count == 4)
        {
            AssignDiamondOrFan(plan, list, m);
            return;
        }

        AssignCrowd(plan, list, m);
    }

    /// <summary>
    /// Two speakers: whoever speaks first goes Above (ambient-style); the other goes Below past the feet.
    /// Same rule when one is close-to-camera and the other far — screen L/R does not flip Above/Below.
    /// </summary>
    static void AssignPair(ConversationLayoutPlan plan, Work first, Work second, Metrics m)
    {
        // Optional: if extremely far apart horizontally AND both have side room, use Left/Right
        // so wide two-shots do not stack Above/Below in the center. Speak-order still decides
        // which side is preferred only for tie-break when depth differs.
        float dx = Mathf.Abs(first.screen.x - second.screen.x);
        float dy = Mathf.Abs(first.screen.y - second.screen.y);
        bool wideApart = dx > m.width * 0.85f && dx > dy * HorizontalBias;
        bool leftHasRoom = Mathf.Min(first.screen.x, second.screen.x) - m.safe.xMin >= m.sideSpaceMin;
        bool rightHasRoom = m.safe.xMax - Mathf.Max(first.screen.x, second.screen.x) >= m.sideSpaceMin;

        if (wideApart && leftHasRoom && rightHasRoom)
        {
            Work left = first.screen.x <= second.screen.x ? first : second;
            Work right = left.id == first.id ? second : first;
            Commit(plan, left, ConversationSlotSide.Left, new Vector2(-(m.sideGap + m.width * 0.15f), m.lift * 0.35f));
            Commit(plan, right, ConversationSlotSide.Right, new Vector2(m.sideGap + m.width * 0.15f, m.lift * 0.35f));
            return;
        }

        // Default / close / depth pair: first speaker Above, second Below (rules 1, 1.1, 6).
        Commit(plan, first, ConversationSlotSide.Above, new Vector2(0f, m.lift));
        Commit(plan, second, ConversationSlotSide.Below, new Vector2(0f, -m.belowGap));
    }

    static void AssignTrio(ConversationLayoutPlan plan, List<Work> list, Metrics m)
    {
        float minX = MinX(list);
        float maxX = MaxX(list);
        float minY = MinY(list);
        float maxY = MaxY(list);
        float rangeX = maxX - minX;
        float rangeY = maxY - minY;

        bool horizontal = rangeX >= rangeY * HorizontalBias && rangeY <= m.height * SameRowYFrac * 3f;
        bool vertical = rangeY >= rangeX * VerticalBias && rangeX <= m.width * SameColXFrac;

        if (horizontal)
        {
            AssignHorizontalTrio(plan, list, m);
            return;
        }

        if (vertical)
        {
            AssignVerticalTrio(plan, list, m);
            return;
        }

        // Mixed (rule 2 / 2.1): lowest speaker Below, other two Above.
        list.Sort((a, b) => a.screen.y.CompareTo(b.screen.y));
        Work down = list[0];
        Work a = list[1];
        Work b = list[2];
        Commit(plan, down, ConversationSlotSide.Below, new Vector2(0f, -m.belowGap));
        Commit(plan, a, ConversationSlotSide.Above, new Vector2(0f, m.lift));
        Commit(plan, b, ConversationSlotSide.Above, new Vector2(0f, m.lift));
    }

    /// <summary>Rule 3 / 3.1 — L · C · R on a row.</summary>
    static void AssignHorizontalTrio(ConversationLayoutPlan plan, List<Work> list, Metrics m)
    {
        list.Sort((a, b) => a.screen.x.CompareTo(b.screen.x));
        Work left = list[0];
        Work center = list[1];
        Work right = list[2];

        bool leftRoom = left.screen.x - m.safe.xMin >= m.sideSpaceMin;
        bool rightRoom = m.safe.xMax - right.screen.x >= m.sideSpaceMin;

        if (leftRoom && rightRoom)
        {
            Commit(plan, left, ConversationSlotSide.Left, new Vector2(-(m.sideGap + m.width * 0.12f), m.lift * 0.25f));
            Commit(plan, center, ConversationSlotSide.Above, new Vector2(0f, m.lift));
            Commit(plan, right, ConversationSlotSide.Right, new Vector2(m.sideGap + m.width * 0.12f, m.lift * 0.25f));
        }
        else
        {
            // No side space: L+R Above, C Below.
            Commit(plan, left, ConversationSlotSide.Above, new Vector2(0f, m.lift));
            Commit(plan, right, ConversationSlotSide.Above, new Vector2(0f, m.lift));
            Commit(plan, center, ConversationSlotSide.Below, new Vector2(0f, -m.belowGap));
        }
    }

    /// <summary>Rule 4 / 4.1 — U · C · D stacked; bias toward open screen side (facing proxy).</summary>
    static void AssignVerticalTrio(ConversationLayoutPlan plan, List<Work> list, Metrics m)
    {
        list.Sort((a, b) => b.screen.y.CompareTo(a.screen.y)); // top → bottom
        Work up = list[0];
        Work mid = list[1];
        Work down = list[2];

        float stackX = (up.screen.x + mid.screen.x + down.screen.x) / 3f;
        bool faceRight = stackX < m.safe.center.x; // more room on the right
        float bias = (faceRight ? 1f : -1f) * (m.sideGap + m.width * 0.08f);
        ConversationSlotSide midSide = faceRight ? ConversationSlotSide.Right : ConversationSlotSide.Left;

        Commit(plan, up, ConversationSlotSide.Above, new Vector2(bias, m.lift));
        Commit(plan, mid, midSide, new Vector2(faceRight ? m.sideGap : -m.sideGap, 0f));
        Commit(plan, down, ConversationSlotSide.Below, new Vector2(bias, -m.belowGap));
    }

    /// <summary>Rule 5 / 5.1 — diamond / four corners.</summary>
    static void AssignDiamondOrFan(ConversationLayoutPlan plan, List<Work> list, Metrics m)
    {
        list.Sort((a, b) => b.screen.y.CompareTo(a.screen.y));
        Work up = list[0];
        Work down = list[3];
        var mid = new List<Work> { list[1], list[2] };
        mid.Sort((a, b) => a.screen.x.CompareTo(b.screen.x));
        Work left = mid[0];
        Work right = mid[1];

        // Prefer Above for everyone first.
        var sides = new Dictionary<string, ConversationSlotSide>(4);
        var offsets = new Dictionary<string, Vector2>(4);
        sides[up.id] = ConversationSlotSide.Above;
        offsets[up.id] = new Vector2(0f, m.lift);
        sides[left.id] = ConversationSlotSide.Above;
        offsets[left.id] = new Vector2(-m.sideGap * 0.35f, m.lift);
        sides[right.id] = ConversationSlotSide.Above;
        offsets[right.id] = new Vector2(m.sideGap * 0.35f, m.lift);
        sides[down.id] = ConversationSlotSide.Above;
        offsets[down.id] = new Vector2(0f, m.lift);

        Rect SizeRect(Work w, ConversationSlotSide side, Vector2 off)
        {
            Vector2 pivot = Pivot(side);
            Vector2 pos = w.screen + off;
            return new Rect(
                pos.x - m.width * pivot.x,
                pos.y - m.height * pivot.y,
                m.width,
                m.height);
        }

        Rect uRect = SizeRect(up, sides[up.id], offsets[up.id]);

        // If D would cover U → send D Below.
        Rect dTry = SizeRect(down, ConversationSlotSide.Above, offsets[down.id]);
        if (dTry.Overlaps(uRect))
        {
            sides[down.id] = ConversationSlotSide.Below;
            offsets[down.id] = new Vector2(0f, -m.belowGap);
        }

        // If L/R cover U → push to the outside (away from U).
        Rect lTry = SizeRect(left, sides[left.id], offsets[left.id]);
        if (lTry.Overlaps(uRect))
        {
            sides[left.id] = ConversationSlotSide.Left;
            offsets[left.id] = new Vector2(-(m.sideGap + m.width * 0.12f), m.lift * 0.2f);
        }

        Rect rTry = SizeRect(right, sides[right.id], offsets[right.id]);
        if (rTry.Overlaps(uRect))
        {
            sides[right.id] = ConversationSlotSide.Right;
            offsets[right.id] = new Vector2(m.sideGap + m.width * 0.12f, m.lift * 0.2f);
        }

        Commit(plan, up, sides[up.id], offsets[up.id]);
        Commit(plan, left, sides[left.id], offsets[left.id]);
        Commit(plan, right, sides[right.id], offsets[right.id]);
        Commit(plan, down, sides[down.id], offsets[down.id]);
    }

    /// <summary>5+ speakers: leftmost/rightmost take sides when possible; others Above/Below by speak order.</summary>
    static void AssignCrowd(ConversationLayoutPlan plan, List<Work> list, Metrics m)
    {
        var byX = new List<Work>(list);
        byX.Sort((a, b) => a.screen.x.CompareTo(b.screen.x));

        Work leftmost = byX[0];
        Work rightmost = byX[byX.Count - 1];
        bool leftRoom = leftmost.screen.x - m.safe.xMin >= m.sideSpaceMin;
        bool rightRoom = m.safe.xMax - rightmost.screen.x >= m.sideSpaceMin;

        var assigned = new HashSet<string>();
        if (leftRoom)
        {
            Commit(plan, leftmost, ConversationSlotSide.Left, new Vector2(-(m.sideGap + m.width * 0.1f), m.lift * 0.25f));
            assigned.Add(leftmost.id);
        }

        if (rightRoom && rightmost.id != leftmost.id)
        {
            Commit(plan, rightmost, ConversationSlotSide.Right, new Vector2(m.sideGap + m.width * 0.1f, m.lift * 0.25f));
            assigned.Add(rightmost.id);
        }

        // Remaining: alternate Above / Below by first-speak order (first Above).
        var rest = new List<Work>();
        for (int i = 0; i < list.Count; i++)
        {
            if (!assigned.Contains(list[i].id))
                rest.Add(list[i]);
        }

        rest.Sort((a, b) => a.speakIndex.CompareTo(b.speakIndex));
        for (int i = 0; i < rest.Count; i++)
        {
            bool above = (i % 2) == 0;
            if (above)
                Commit(plan, rest[i], ConversationSlotSide.Above, new Vector2(0f, m.lift));
            else
                Commit(plan, rest[i], ConversationSlotSide.Below, new Vector2(0f, -m.belowGap));
        }
    }

    static void Commit(ConversationLayoutPlan plan, Work w, ConversationSlotSide side, Vector2 offset)
    {
        plan.speakers.Add(new ConversationSpeakerSlot
        {
            speakerId = w.id,
            anchor = w.anchor,
            side = side,
            screenAnchor0 = w.screen,
            localOffsetScreen = offset,
            cameraDistance = w.depth,
            isPlayer = w.isPlayer,
            visible = false,
            isCore = true,
        });
    }

    static int SpeakIndex(IList<string> order, string id)
    {
        if (order == null || string.IsNullOrEmpty(id))
            return 999;
        for (int i = 0; i < order.Count; i++)
        {
            if (string.Equals(order[i], id, System.StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return 900 + id.GetHashCode() % 50;
    }

    static float MinX(List<Work> list)
    {
        float v = list[0].screen.x;
        for (int i = 1; i < list.Count; i++)
            v = Mathf.Min(v, list[i].screen.x);
        return v;
    }

    static float MaxX(List<Work> list)
    {
        float v = list[0].screen.x;
        for (int i = 1; i < list.Count; i++)
            v = Mathf.Max(v, list[i].screen.x);
        return v;
    }

    static float MinY(List<Work> list)
    {
        float v = list[0].screen.y;
        for (int i = 1; i < list.Count; i++)
            v = Mathf.Min(v, list[i].screen.y);
        return v;
    }

    static float MaxY(List<Work> list)
    {
        float v = list[0].screen.y;
        for (int i = 1; i < list.Count; i++)
            v = Mathf.Max(v, list[i].screen.y);
        return v;
    }

    static Vector2 Pivot(ConversationSlotSide side)
    {
        switch (side)
        {
            case ConversationSlotSide.Left: return new Vector2(1f, 0.5f);
            case ConversationSlotSide.Right: return new Vector2(0f, 0.5f);
            case ConversationSlotSide.Below: return new Vector2(0.5f, 1f);
            default: return new Vector2(0.5f, 0f);
        }
    }

    struct Work
    {
        public string id;
        public Transform anchor;
        public Vector2 screen;
        public float depth;
        public bool isPlayer;
        public int speakIndex;
    }
}
