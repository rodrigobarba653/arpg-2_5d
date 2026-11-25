using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single layout authority for ambient + interactive chatboxes.
/// Preferred → candidates → clamp → overlap; interactive uses sticky slots.
/// Sizes/offsets are authored in Canvas reference units and scaled via canvasScaleFactor.
/// </summary>
public class DialogueLayoutSolver
{
    public DialogueUiSafeArea safeArea = new DialogueUiSafeArea();
    public DialogueLayoutSettings settings = new DialogueLayoutSettings();

    /// <summary>CanvasScaler.scaleFactor (reference units → screen pixels).</summary>
    public float canvasScaleFactor = 1f;

    readonly List<Rect> blockers = new List<Rect>(8);
    readonly List<DialogueChatboxPlacement> candidateScratch = new List<DialogueChatboxPlacement>(5);

    public float RefToScreen(float refUnits)
    {
        float s = canvasScaleFactor > 0.01f ? canvasScaleFactor : 1f;
        return refUnits * s;
    }

    public float CandidatePaddingScreen => RefToScreen(
        settings != null ? settings.speakerOffsetRef : 24f);

    /// <param name="useDefaultReserveBand">
    /// When true and no explicit interactive blocker, reserve the lower screen band
    /// (ambient must not cover typical interactive UI).
    /// </param>
    public void BeginFrame(Rect? interactiveBlocker, bool useDefaultReserveBand = true)
    {
        blockers.Clear();
        if (interactiveBlocker.HasValue && interactiveBlocker.Value.width > 1f)
            blockers.Add(interactiveBlocker.Value);
        else if (useDefaultReserveBand && safeArea != null)
            blockers.Add(safeArea.GetInteractiveReserveScreenRect());
    }

    public void RegisterOccupied(Rect screenRect)
    {
        blockers.Add(screenRect);
    }

    public Rect GetEffectiveSafeScreenRect()
    {
        Rect normalized = safeArea != null
            ? safeArea.GetSafeScreenRect()
            : new Rect(0f, 0f, Screen.width, Screen.height);

        float margin = RefToScreen(settings != null ? settings.screenMarginRef : 24f);
        Rect marginRect = new Rect(
            margin,
            margin,
            Mathf.Max(1f, Screen.width - margin * 2f),
            Mathf.Max(1f, Screen.height - margin * 2f));

        return Intersect(normalized, marginRect);
    }

    /// <summary>
    /// Builds a stable multi-speaker composition once for the conversation.
    /// Uses first-speak order + screen geometry (rules 1–7). Off-camera speakers
    /// still contribute via projected screen positions.
    /// </summary>
    public ConversationLayoutPlan BuildConversationPlan(
        IList<ConversationSpeakerSlot> draftSpeakers,
        Vector2 sizeHint,
        IList<string> speakOrder = null)
    {
        var plan = new ConversationLayoutPlan();
        float sideGap = RefToScreen(settings != null ? settings.conversationSideGapRef : 36f);
        float lift = RefToScreen(settings != null && settings.conversationLiftRef > 0.01f
            ? settings.conversationLiftRef
            : 72f);
        float soloLift = RefToScreen(settings != null && settings.conversationSoloLiftRef > 0.01f
            ? settings.conversationSoloLiftRef
            : 40f);
        float belowGap = RefToScreen(settings != null && settings.conversationBelowGapRef > 0.01f
            ? settings.conversationBelowGapRef
            : 160f);
        float sideSpaceMin = RefToScreen(settings != null && settings.conversationSideSpaceMinRef > 0.01f
            ? settings.conversationSideSpaceMinRef
            : 280f);
        float width = sizeHint.x > 8f
            ? sizeHint.x
            : RefToScreen(settings != null ? settings.interactiveMaxWidthRef : 760f);
        float height = sizeHint.y > 8f
            ? sizeHint.y
            : RefToScreen(settings != null ? settings.interactiveHeightRef : 280f);

        ConversationLayoutComposer.Compose(
            plan,
            draftSpeakers,
            speakOrder,
            new ConversationLayoutComposer.Metrics
            {
                sideGap = sideGap,
                lift = lift,
                soloLift = soloLift,
                belowGap = belowGap,
                width = width,
                height = height,
                safe = GetEffectiveSafeScreenRect(),
                sideSpaceMin = sideSpaceMin,
            });

        return plan;
    }

    /// <summary>
    /// Legacy no-op: pair layout is owned by <see cref="ConversationLayoutComposer"/> (first Above / second Below).
    /// </summary>
    public void EnsureTwoSpeakerPairSides(ConversationLayoutPlan plan)
    {
        // Intentionally empty — old Left/Right screen columns conflicted with the new rules.
    }

    /// <summary>
    /// Appends branch speakers on frozen offsets. Does not write core slots.
    /// Returns true when a box still overlaps after the safe-area clamp.
    /// </summary>
    public bool AddBranchSlots(
        ConversationLayoutPlan plan,
        IList<ConversationSpeakerSlot> branchDrafts,
        IList<int> regions,
        IList<string[]> pathSpeakers,
        Vector2 sizeHint)
    {
        if (plan == null || branchDrafts == null || branchDrafts.Count == 0)
            return false;

        float sideGap = RefToScreen(settings != null ? settings.conversationSideGapRef : 36f);
        float lift = RefToScreen(settings != null ? settings.conversationLiftRef : 48f);
        float width = sizeHint.x > 8f
            ? sizeHint.x
            : RefToScreen(settings != null ? settings.interactiveMaxWidthRef : 520f);
        float height = sizeHint.y > 8f
            ? sizeHint.y
            : RefToScreen(settings != null ? settings.interactiveHeightRef : 220f);
        float band = sideGap + width * 0.35f;
        Vector2 size = new Vector2(width, height);
        bool clampedOverlap = false;

        for (int i = 0; i < branchDrafts.Count; i++)
        {
            var src = branchDrafts[i];
            if (src == null)
                continue;
            int region = regions != null && i < regions.Count ? regions[i] : i;
            plan.speakers.Add(new ConversationSpeakerSlot
            {
                speakerId = src.speakerId,
                anchor = src.anchor,
                side = ConversationSlotSide.Above,
                screenAnchor0 = src.screenAnchor0,
                localOffsetScreen = BranchRecipe(region, lift, sideGap, band),
                isPlayer = src.isPlayer,
                visible = false,
                isCore = false,
            });
        }

        if (pathSpeakers == null)
            return false;

        for (int p = 0; p < pathSpeakers.Count; p++)
        {
            var path = pathSpeakers[p];
            if (path == null)
                continue;
            for (int s = 0; s < path.Length; s++)
            {
                if (!plan.TryGet(path[s], out var slot) || slot == null || slot.isCore)
                    continue;
                if (SeparateBranchSlot(plan, slot, path, size, lift, band))
                    clampedOverlap = true;
            }
        }

        return clampedOverlap;
    }

    static Vector2 BranchRecipe(int region, float lift, float sideGap, float band)
    {
        float y = (region + 1) * (lift + sideGap);
        float x = 0f;
        if (region > 0)
        {
            int step = (region + 1) / 2;
            float sign = (region % 2 == 1) ? 1f : -1f;
            x = sign * step * band;
        }
        return new Vector2(x, y);
    }

    bool SeparateBranchSlot(
        ConversationLayoutPlan plan,
        ConversationSpeakerSlot slot,
        string[] path,
        Vector2 size,
        float liftStep,
        float band)
    {
        Rect safe = GetEffectiveSafeScreenRect();
        Vector2 pivot = PivotForConversationSide(ConversationSlotSide.Above);
        bool gaveUp = false;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 offset = slot.localOffsetScreen;
            Vector2 pos = slot.screenAnchor0 + offset;
            pos = ClampPivotPosition(pos, size, pivot, safe);
            slot.localOffsetScreen = pos - slot.screenAnchor0;

            Rect mine = PivotRect(pos, size, pivot);
            if (!OverlapsPath(plan, slot.speakerId, path, mine))
                return false;

            float away = 0f;
            for (int i = 0; i < path.Length; i++)
            {
                if (!plan.TryGet(path[i], out var other) || other == null)
                    continue;
                if (string.Equals(other.speakerId, slot.speakerId, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                Rect otherRect = SlotRect(other, size);
                if (!mine.Overlaps(otherRect))
                    continue;
                away = otherRect.center.x >= mine.center.x ? -1f : 1f;
                break;
            }

            slot.localOffsetScreen.y += liftStep;
            if ((attempt % 2) == 1)
                slot.localOffsetScreen.x += away * band;
            gaveUp = true;
        }

        Vector2 finalPos = ClampPivotPosition(
            slot.screenAnchor0 + slot.localOffsetScreen, size, pivot, safe);
        slot.localOffsetScreen = finalPos - slot.screenAnchor0;
        Rect finalRect = PivotRect(finalPos, size, pivot);
        return gaveUp && OverlapsPath(plan, slot.speakerId, path, finalRect);
    }

    static bool OverlapsPath(ConversationLayoutPlan plan, string self, string[] path, Rect mine)
    {
        for (int i = 0; i < path.Length; i++)
        {
            if (string.Equals(path[i], self, System.StringComparison.OrdinalIgnoreCase))
                continue;
            if (!plan.TryGet(path[i], out var other) || other == null)
                continue;
            if (mine.Overlaps(SlotRect(other, new Vector2(mine.width, mine.height))))
                return true;
        }
        return false;
    }

    static Rect SlotRect(ConversationSpeakerSlot slot, Vector2 size)
    {
        Vector2 pivot = PivotForConversationSide(slot.side);
        return PivotRect(slot.screenAnchor0 + slot.localOffsetScreen, size, pivot);
    }

    static Rect PivotRect(Vector2 pivotPos, Vector2 size, Vector2 pivot)
    {
        float left = pivotPos.x - size.x * pivot.x;
        float bottom = pivotPos.y - size.y * pivot.y;
        return new Rect(left, bottom, size.x, size.y);
    }

    /// <summary>
    /// Large interactive banner at the top of the safe area (no world pointer / no tail).
    /// </summary>
    public void PlaceScreenTop(
        DialogueChatboxView view,
        Vector2 sizeHint,
        Vector2 manualOffsetRef)
    {
        if (view == null || view.rectTransform == null)
            return;

        Rect safe = GetEffectiveSafeScreenRect();
        Vector2 size = ResolveSize(view, sizeHint, ambient: false);
        // Use full interactive width band (not half-column pair clamp).
        ApplyInteractiveWidth(view, Mathf.Max(size.x, RefToScreen(
            settings != null ? settings.interactiveMaxWidthRef : 1100f)));
        UseFittedHeight(view, ref size);
        size = ResolveSize(view, size, ambient: false);

        Vector2 manualOffset = manualOffsetRef * (canvasScaleFactor > 0.01f ? canvasScaleFactor : 1f);
        Vector2 pivot = new Vector2(0.5f, 1f);
        float topPad = RefToScreen(8f);
        Vector2 raw = new Vector2(safe.center.x, safe.yMax - topPad) + manualOffset;
        Vector2 clamped = ClampPivotPosition(raw, size, pivot, safe);

        view.rectTransform.pivot = pivot;
        view.rectTransform.position = clamped;
        RegisterOccupied(view.GetScreenRect());
    }

    /// <summary>
    /// Updates fitted width/height for a pinned bubble without moving it.
    /// </summary>
    public void RefreshPinnedBubbleSize(DialogueChatboxView view, Vector2 sizeHint)
    {
        if (view == null || view.rectTransform == null)
            return;

        Rect safe = GetEffectiveSafeScreenRect();
        Vector2 size = ResolveSize(view, sizeHint, ambient: false);
        float pad = RefToScreen(settings != null ? settings.screenMarginRef : 24f) * 0.35f;
        float gap = RefToScreen(settings != null ? settings.conversationSideGapRef : 36f);
        float maxPairWidth = Mathf.Max(120f, (safe.width - gap) * 0.5f - pad);
        if (size.x > maxPairWidth)
            size.x = maxPairWidth;

        ApplyInteractiveWidth(view, size.x);
    }

    /// <summary>
    /// Places a bubble using its reserved conversation slot relative to the live speaker
    /// (Above / Below / Left / Right — same idea as ambient). Slot side + offset are frozen
    /// at plan time; only the anchor follows.
    /// </summary>
    public bool FollowConversationSlot(
        DialogueChatboxView view,
        ConversationSpeakerSlot slot,
        Vector2 anchorScreen,
        Vector2 sizeHint,
        Vector2 manualOffsetRef)
    {
        if (view == null || view.rectTransform == null || slot == null)
            return false;

        Rect safe = GetEffectiveSafeScreenRect();
        Vector2 size = ResolveSize(view, sizeHint, ambient: false);
        Vector2 pivot = PivotForConversationSide(slot.side);
        Vector2 manual = manualOffsetRef * (canvasScaleFactor > 0.01f ? canvasScaleFactor : 1f);

        // Side slots share width so L+R can coexist without covering the whole safe area.
        bool sideSlot = slot.side == ConversationSlotSide.Left ||
                        slot.side == ConversationSlotSide.Right;
        if (sideSlot)
        {
            float gap = RefToScreen(settings != null ? settings.conversationSideGapRef : 36f);
            float pad = RefToScreen(settings != null ? settings.screenMarginRef : 24f) * 0.35f;
            float maxPairWidth = Mathf.Max(120f, (safe.width - gap) * 0.55f - pad);
            if (size.x > maxPairWidth)
                size.x = maxPairWidth;
        }

        ApplyInteractiveWidth(view, size.x);
        UseFittedHeight(view, ref size);

        Vector2 raw = anchorScreen + slot.localOffsetScreen + manual;
        Vector2 pos = ClampPivotPosition(raw, size, pivot, safe);
        Rect avoid = BuildSpeakerAvoidRect(
            anchorScreen,
            slot.isPlayer && settings != null ? settings.playerAvoidScale : 1f);
        pos = NudgeClearOfRect(pos, size, pivot, avoid, slot.side, safe);

        view.rectTransform.pivot = pivot;
        view.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        view.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        view.rectTransform.position = pos;

        Canvas.ForceUpdateCanvases();
        RegisterOccupied(view.GetScreenRect());
        return true;
    }

    /// <summary>
    /// Conversation slots are frozen before the first line. Mid-dialogue rebuilds are off.
    /// </summary>
    public bool ShouldRebuildConversationPlan(
        ConversationLayoutPlan plan,
        IList<ConversationSpeakerSlot> liveSnapshots)
    {
        return plan == null || plan.speakers.Count == 0;
    }

    static bool IsOutsideSafe(Vector2 screen, Rect safe, float margin)
    {
        return screen.x < safe.xMin - margin ||
               screen.x > safe.xMax + margin ||
               screen.y < safe.yMin - margin ||
               screen.y > safe.yMax + margin;
    }

    static Vector2 PivotForConversationSide(ConversationSlotSide side)
    {
        switch (side)
        {
            case ConversationSlotSide.Left: return new Vector2(1f, 0.5f);
            case ConversationSlotSide.Right: return new Vector2(0f, 0.5f);
            case ConversationSlotSide.Below: return new Vector2(0.5f, 1f);
            default: return new Vector2(0.5f, 0f); // Above
        }
    }

    Vector2 NudgeClearOfRect(
        Vector2 pivotPos,
        Vector2 size,
        Vector2 pivot,
        Rect avoid,
        ConversationSlotSide side,
        Rect safe)
    {
        float left = pivotPos.x - size.x * pivot.x;
        float bottom = pivotPos.y - size.y * pivot.y;
        var bounds = new Rect(left, bottom, size.x, size.y);
        if (!bounds.Overlaps(avoid))
            return ClampPivotPosition(pivotPos, size, pivot, safe);

        float gap = RefToScreen(12f);
        switch (side)
        {
            case ConversationSlotSide.Left:
                pivotPos.x -= (bounds.xMax - avoid.xMin) + gap;
                break;
            case ConversationSlotSide.Right:
                pivotPos.x += (avoid.xMax - bounds.xMin) + gap;
                break;
            case ConversationSlotSide.Below:
                pivotPos.y -= (bounds.yMax - avoid.yMin) + gap;
                break;
            default:
                pivotPos.y += (avoid.yMax - bounds.yMin) + gap;
                break;
        }

        return ClampPivotPosition(pivotPos, size, pivot, safe);
    }

    /// <summary>
    /// Positions ambient bubble near world-projected anchor. Returns false if no free slot.
    /// </summary>
    public bool TryPlaceAmbient(
        DialogueChatboxView view,
        Vector2 anchorScreen,
        DialogueChatboxPlacement preferred,
        Vector2 manualOffsetRef,
        Vector2 sizeHint,
        bool avoidCoveringAnchor = false)
    {
        if (view == null || view.rectTransform == null)
            return false;

        Rect safe = GetEffectiveSafeScreenRect();
        Vector2 size = ResolveSize(view, sizeHint, ambient: true);
        Vector2 manualOffset = manualOffsetRef * (canvasScaleFactor > 0.01f ? canvasScaleFactor : 1f);

        BuildCandidateOrder(preferred, avoidCoveringAnchor, candidateScratch);
        // Base lift keeps ambient a bit above the NPC head / speechAnchor when following.
        float liftRef = settings != null && settings.ambientLiftRef > 0.01f
            ? settings.ambientLiftRef
            : 72f;
        float baseLift = RefToScreen(liftRef);
        float[] lifts = avoidCoveringAnchor
            ? new[]
            {
                baseLift,
                baseLift + RefToScreen(48f),
                baseLift + RefToScreen(96f),
                baseLift + RefToScreen(144f),
            }
            : new[]
            {
                baseLift,
                baseLift + RefToScreen(36f),
                baseLift + RefToScreen(72f),
            };

        for (int l = 0; l < lifts.Length; l++)
        {
            for (int i = 0; i < candidateScratch.Count; i++)
            {
                var side = candidateScratch[i];
                if (avoidCoveringAnchor && side == DialogueChatboxPlacement.Below)
                    continue;

                Vector2 pivot = PivotFor(side);
                if (avoidCoveringAnchor)
                    pivot = new Vector2(0.5f, 0f);

                Vector2 raw = AnchorPoint(anchorScreen, side, CandidatePaddingScreen)
                              + manualOffset
                              + new Vector2(0f, lifts[l]);
                Vector2 clamped = ClampPivotPosition(raw, size, pivot, safe);

                view.rectTransform.pivot = pivot;
                view.rectTransform.position = clamped;

                Canvas.ForceUpdateCanvases();
                Rect bounds = view.GetScreenRect();
                if (!OverlapsAny(bounds))
                {
                    RegisterOccupied(bounds);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Interactive placement near speaker. Follow is immediate; lerp only when sticky slot changes.
    /// Other chatboxes are hard blockers — if the sticky slot overlaps them, this bubble moves.
    /// Always leaves the bubble on-screen (fallback) so dialogue stays legible.
    /// </summary>
    public void PlaceInteractiveSticky(
        DialogueChatboxView view,
        Vector2 anchorScreen,
        DialogueChatboxPlacement preferred,
        Vector2 manualOffsetRef,
        Vector2 sizeHint,
        ref StickyPlacementState sticky,
        float deltaTime,
        Rect? speakerAvoidScreen,
        List<Rect> otherCharacterAvoids = null,
        bool hardAvoidOwnSpeaker = true)
    {
        if (view == null || view.rectTransform == null)
            return;

        Rect safe = GetEffectiveSafeScreenRect();
        Vector2 size = ResolveSize(view, sizeHint, ambient: false);
        ApplyInteractiveWidth(view, size.x);
        UseFittedHeight(view, ref size);

        Vector2 manualOffset = manualOffsetRef * (canvasScaleFactor > 0.01f ? canvasScaleFactor : 1f);
        float pad = CandidatePaddingScreen;
        float transition = settings != null ? settings.slotTransitionSeconds : 0.15f;
        float overlapRatio = settings != null ? settings.significantOverlapRatio : 0.18f;
        float speakerCoverReject = settings != null ? settings.speakerCoverRejectRatio : 0.08f;

        // Characters are hard blockers. Own speaker too when hardAvoidOwnSpeaker
        // (fixes large bubbles covering a small sprite — bubble-relative ratios lie).
        int otherBlockerStart = blockers.Count;
        if (otherCharacterAvoids != null)
        {
            for (int i = 0; i < otherCharacterAvoids.Count; i++)
            {
                if (otherCharacterAvoids[i].width > 1f)
                    blockers.Add(otherCharacterAvoids[i]);
            }
        }

        if (hardAvoidOwnSpeaker && speakerAvoidScreen.HasValue && speakerAvoidScreen.Value.width > 1f)
            blockers.Add(speakerAvoidScreen.Value);

        bool keepSticky = false;
        Vector2 stickyPos = default;
        Vector2 stickyPivot = default;
        float stickyLift = 0f;
        float stickyNudgeX = 0f;

        if (sticky.hasSlot)
        {
            stickyPivot = sticky.pivot;
            stickyLift = sticky.liftScreen;
            stickyNudgeX = sticky.xNudgeScreen;
            stickyPos = ClampPivotPosition(
                AnchorPoint(anchorScreen, sticky.side, pad)
                + manualOffset
                + new Vector2(stickyNudgeX, stickyLift),
                size,
                stickyPivot,
                safe);

            if (IsSlotAcceptable(
                    view,
                    stickyPos,
                    stickyPivot,
                    hardAvoidOwnSpeaker ? null : speakerAvoidScreen,
                    hardAvoidOwnSpeaker ? speakerCoverReject : overlapRatio,
                    strictOverlap: true))
                keepSticky = true;
        }

        DialogueChatboxPlacement chosenSide = sticky.side;
        Vector2 chosenPos = stickyPos;
        Vector2 chosenPivot = stickyPivot;
        float chosenLift = stickyLift;
        float chosenNudgeX = stickyNudgeX;
        bool slotChanged = false;

        if (!keepSticky)
        {
            // Prefer not covering the speaker (skip Below when hard-avoiding self).
            BuildCandidateOrder(preferred, avoidCoveringAnchor: hardAvoidOwnSpeaker, candidateScratch);

            float[] lifts =
            {
                RefToScreen(0f),
                RefToScreen(40f),
                RefToScreen(80f),
                RefToScreen(120f),
            };
            float[] xNudges =
            {
                0f,
                RefToScreen(140f),
                RefToScreen(-140f),
                RefToScreen(240f),
                RefToScreen(-240f),
            };

            bool found = false;
            for (int n = 0; n < xNudges.Length && !found; n++)
            {
                for (int l = 0; l < lifts.Length && !found; l++)
                {
                    for (int i = 0; i < candidateScratch.Count; i++)
                    {
                        var side = candidateScratch[i];
                        Vector2 pivot = PivotFor(side);
                        Vector2 raw = AnchorPoint(anchorScreen, side, pad)
                                      + manualOffset
                                      + new Vector2(xNudges[n], lifts[l]);
                        Vector2 clamped = ClampPivotPosition(raw, size, pivot, safe);

                        if (!IsSlotAcceptable(
                                view,
                                clamped,
                                pivot,
                                hardAvoidOwnSpeaker ? null : speakerAvoidScreen,
                                hardAvoidOwnSpeaker ? speakerCoverReject : overlapRatio,
                                strictOverlap: true))
                            continue;

                        chosenSide = side;
                        chosenPos = clamped;
                        chosenPivot = pivot;
                        chosenLift = lifts[l];
                        chosenNudgeX = xNudges[n];
                        found = true;
                        slotChanged = !sticky.hasSlot
                                      || sticky.side != side
                                      || Mathf.Abs(sticky.liftScreen - lifts[l]) > 1f
                                      || Mathf.Abs(sticky.xNudgeScreen - xNudges[n]) > 1f;
                        break;
                    }
                }
            }

            if (!found)
            {
                // Last resort: pick the candidate with least chatbox overlap, then push clear.
                ResolveLeastOverlapFallback(
                    view,
                    anchorScreen,
                    preferred,
                    manualOffset,
                    pad,
                    size,
                    safe,
                    speakerAvoidScreen,
                    speakerCoverReject,
                    hardAvoidOwnSpeaker,
                    out chosenSide,
                    out chosenPos,
                    out chosenPivot,
                    out chosenLift,
                    out chosenNudgeX);
                slotChanged = !sticky.hasSlot
                              || sticky.side != chosenSide
                              || Mathf.Abs(sticky.xNudgeScreen - chosenNudgeX) > 1f;
            }
        }

        sticky.side = chosenSide;
        sticky.pivot = chosenPivot;
        sticky.liftScreen = chosenLift;
        sticky.xNudgeScreen = chosenNudgeX;
        sticky.followTarget = chosenPos;
        sticky.hasSlot = true;

        if (slotChanged)
        {
            Vector2 from = sticky.displayed;
            if (from.sqrMagnitude < 0.01f)
                from = chosenPos;
            sticky.BeginTransition(from, transition);
        }

        // Drop temporary character blockers before registering this chatbox as occupied.
        if (blockers.Count > otherBlockerStart)
            blockers.RemoveRange(otherBlockerStart, blockers.Count - otherBlockerStart);

        Vector2 display = sticky.TickDisplayed(deltaTime);
        view.rectTransform.pivot = chosenPivot;
        view.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        view.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

        // Occupy the settled/follow target so siblings resolve against final slots, not mid-lerp.
        view.rectTransform.position = chosenPos;
        Canvas.ForceUpdateCanvases();
        RegisterOccupied(view.GetScreenRect());
        view.rectTransform.position = display;
    }

    void ResolveLeastOverlapFallback(
        DialogueChatboxView view,
        Vector2 anchorScreen,
        DialogueChatboxPlacement preferred,
        Vector2 manualOffset,
        float pad,
        Vector2 size,
        Rect safe,
        Rect? speakerAvoid,
        float speakerCoverReject,
        bool hardAvoidOwnSpeaker,
        out DialogueChatboxPlacement chosenSide,
        out Vector2 chosenPos,
        out Vector2 chosenPivot,
        out float chosenLift,
        out float chosenNudgeX)
    {
        BuildCandidateOrder(preferred, avoidCoveringAnchor: hardAvoidOwnSpeaker, candidateScratch);

        float bestScore = float.MaxValue;
        chosenSide = DialogueChatboxPlacement.Above;
        chosenPivot = PivotFor(chosenSide);
        chosenLift = RefToScreen(48f);
        chosenNudgeX = 0f;
        chosenPos = ClampPivotPosition(
            AnchorPoint(anchorScreen, chosenSide, pad) + manualOffset + new Vector2(0f, chosenLift),
            size,
            chosenPivot,
            safe);

        float[] lifts = { RefToScreen(0f), RefToScreen(64f), RefToScreen(128f), RefToScreen(-64f) };
        float[] xNudges = { 0f, RefToScreen(180f), RefToScreen(-180f), RefToScreen(300f), RefToScreen(-300f) };

        for (int n = 0; n < xNudges.Length; n++)
        {
            for (int l = 0; l < lifts.Length; l++)
            {
                for (int i = 0; i < candidateScratch.Count; i++)
                {
                    var side = candidateScratch[i];
                    Vector2 pivot = PivotFor(side);
                    Vector2 raw = AnchorPoint(anchorScreen, side, pad)
                                  + manualOffset
                                  + new Vector2(xNudges[n], lifts[l]);
                    Vector2 clamped = ClampPivotPosition(raw, size, pivot, safe);

                    view.rectTransform.pivot = pivot;
                    view.rectTransform.position = clamped;
                    Canvas.ForceUpdateCanvases();
                    Rect bounds = view.GetScreenRect();

                    float speakerPen = 0f;
                    if (speakerAvoid.HasValue)
                    {
                        float cover = CoverRatioOfTarget(bounds, speakerAvoid.Value);
                        speakerPen = OverlapArea(bounds, speakerAvoid.Value);
                        if (hardAvoidOwnSpeaker && cover > speakerCoverReject)
                            speakerPen += size.x * size.y; // heavy reject
                    }

                    float boxPen = TotalBlockerOverlap(bounds);
                    float score = boxPen * 4f + speakerPen * 6f;
                    if (score >= bestScore)
                        continue;

                    bestScore = score;
                    chosenSide = side;
                    chosenPos = clamped;
                    chosenPivot = pivot;
                    chosenLift = lifts[l];
                    chosenNudgeX = xNudges[n];
                }
            }
        }

        // Push away from overlapping chatboxes until clear or iterations exhausted.
        chosenPos = PushClearOfBlockers(view, chosenPos, chosenPivot, size, safe, maxIterations: 12);
    }

    Vector2 PushClearOfBlockers(
        DialogueChatboxView view,
        Vector2 pos,
        Vector2 pivot,
        Vector2 size,
        Rect safe,
        int maxIterations)
    {
        for (int iter = 0; iter < maxIterations; iter++)
        {
            view.rectTransform.pivot = pivot;
            view.rectTransform.position = pos;
            Canvas.ForceUpdateCanvases();
            Rect bounds = view.GetScreenRect();

            Rect hit = default;
            bool any = false;
            for (int i = 0; i < blockers.Count; i++)
            {
                if (!bounds.Overlaps(blockers[i]))
                    continue;
                hit = blockers[i];
                any = true;
                break;
            }

            if (!any)
                break;

            // Prefer vertical separation (FFIX-like stack), then horizontal.
            float boundsCy = bounds.center.y;
            float hitCy = hit.center.y;
            float gap = RefToScreen(16f);
            if (boundsCy >= hitCy)
                pos.y += (hit.yMax - bounds.yMin) + gap;
            else
                pos.y -= (bounds.yMax - hit.yMin) + gap;

            // If still colliding after a vertical push, also nudge sideways.
            pos = ClampPivotPosition(pos, size, pivot, safe);
            view.rectTransform.position = pos;
            Canvas.ForceUpdateCanvases();
            bounds = view.GetScreenRect();
            if (bounds.Overlaps(hit))
            {
                float boundsCx = bounds.center.x;
                float hitCx = hit.center.x;
                if (boundsCx >= hitCx)
                    pos.x += (hit.xMax - bounds.xMin) + gap;
                else
                    pos.x -= (bounds.xMax - hit.xMin) + gap;
                pos = ClampPivotPosition(pos, size, pivot, safe);
            }
        }

        return ClampPivotPosition(pos, size, pivot, safe);
    }

    float TotalBlockerOverlap(Rect bounds)
    {
        float sum = 0f;
        for (int i = 0; i < blockers.Count; i++)
            sum += OverlapArea(bounds, blockers[i]);
        return sum;
    }

    /// <summary>Legacy entry — routes to sticky with a fresh state (no persistence).</summary>
    public void PlaceInteractive(
        DialogueChatboxView view,
        Vector2 anchorScreen,
        Vector2 manualOffsetRef,
        DialogueChatboxPlacement preferred,
        Vector2 sizeHint)
    {
        var sticky = default(StickyPlacementState);
        PlaceInteractiveSticky(
            view,
            anchorScreen,
            preferred,
            manualOffsetRef,
            sizeHint,
            ref sticky,
            deltaTime: 1f,
            speakerAvoidScreen: null);
    }

    public Rect BuildSpeakerAvoidRect(Vector2 anchorScreen, float scaleMul = 1f)
    {
        float mul = Mathf.Max(0.5f, scaleMul);
        float w = RefToScreen(settings != null ? settings.speakerAvoidWidthRef : 88f) * mul;
        float h = RefToScreen(settings != null ? settings.speakerAvoidHeightRef : 140f) * mul;
        return new Rect(anchorScreen.x - w * 0.5f, anchorScreen.y - h * 0.35f, w, h);
    }

    void ApplyInteractiveWidth(DialogueChatboxView view, float widthScreen)
    {
        if (view.rectTransform == null)
            return;

        // sizeDelta is in canvas/local units; divide screen pixels by scaleFactor.
        float inv = 1f / Mathf.Max(0.01f, canvasScaleFactor);
        float widthLocal = widthScreen * inv;

        float minW = settings != null ? settings.interactiveMinWidthRef : 760f;
        float maxW = settings != null ? settings.interactiveMaxWidthRef : 1100f;
        if (view.minWidth > 8f)
            minW = view.minWidth;
        if (view.maxWidth > 8f)
            maxW = view.maxWidth;
        maxW = Mathf.Max(minW, maxW);
        widthLocal = Mathf.Clamp(widthLocal, minW, maxW);

        float heightLocal = settings != null ? settings.interactiveHeightRef : 200f;
        if (view.rectTransform.sizeDelta.y > 8f)
            heightLocal = view.rectTransform.sizeDelta.y;

        view.rectTransform.sizeDelta = new Vector2(widthLocal, heightLocal);
        view.FitHeightToBody();
    }

    void UseFittedHeight(DialogueChatboxView view, ref Vector2 size)
    {
        float scale = canvasScaleFactor > 0.01f ? canvasScaleFactor : 1f;
        float fitted = view.rectTransform.sizeDelta.y * scale;
        if (fitted > 8f)
            size.y = fitted;
    }

    Vector2 ResolveSize(DialogueChatboxView view, Vector2 sizeHint, bool ambient)
    {
        Vector2 size = sizeHint;
        if (size.x < 8f) size.x = view.rectTransform.rect.width;
        if (size.y < 8f) size.y = view.rectTransform.rect.height;

        if (ambient)
        {
            if (size.x < 8f)
                size.x = RefToScreen(settings != null ? settings.ambientWidthRef : 340f);
            if (size.y < 8f)
                size.y = RefToScreen(settings != null ? settings.ambientHeightRef : 120f);
        }
        else
        {
            float minW = RefToScreen(settings != null ? settings.interactiveMinWidthRef : 760f);
            float maxW = RefToScreen(settings != null ? settings.interactiveMaxWidthRef : 1100f);
            maxW = Mathf.Max(minW, maxW);
            if (size.x < 8f) size.x = minW;
            size.x = Mathf.Clamp(size.x, minW, maxW);
            if (size.y < 8f) size.y = RefToScreen(settings != null ? settings.interactiveHeightRef : 200f);
        }

        return size;
    }

    bool IsSlotAcceptable(
        DialogueChatboxView view,
        Vector2 pos,
        Vector2 pivot,
        Rect? speakerAvoid,
        float speakerCoverReject,
        bool strictOverlap)
    {
        view.rectTransform.pivot = pivot;
        view.rectTransform.position = pos;
        Canvas.ForceUpdateCanvases();
        Rect bounds = view.GetScreenRect();

        // Measure cover against the *character* rect, not the bubble — otherwise a large
        // panel can hide the sprite while looking like a small % of bubble area.
        if (speakerAvoid.HasValue && CoverRatioOfTarget(bounds, speakerAvoid.Value) > speakerCoverReject)
            return false;

        if (strictOverlap)
            return !OverlapsAny(bounds);

        return !OverlapsAnySignificant(bounds, speakerCoverReject);
    }

    static float CoverRatioOfTarget(Rect bubble, Rect target)
    {
        float area = OverlapArea(bubble, target);
        float denom = Mathf.Max(1f, target.width * target.height);
        return area / denom;
    }

    bool OverlapsAnySignificant(Rect a, float ratioThreshold)
    {
        for (int i = 0; i < blockers.Count; i++)
        {
            if (!a.Overlaps(blockers[i]))
                continue;
            if (OverlapRatio(a, blockers[i]) >= ratioThreshold)
                return true;
        }
        return false;
    }

    bool OverlapsAny(Rect a)
    {
        for (int i = 0; i < blockers.Count; i++)
        {
            if (a.Overlaps(blockers[i]))
                return true;
        }
        return false;
    }

    static float OverlapRatio(Rect a, Rect b)
    {
        float area = OverlapArea(a, b);
        float denom = Mathf.Max(1f, a.width * a.height);
        return area / denom;
    }

    static float OverlapArea(Rect a, Rect b)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float yMax = Mathf.Min(a.yMax, b.yMax);
        float w = xMax - xMin;
        float h = yMax - yMin;
        if (w <= 0f || h <= 0f)
            return 0f;
        return w * h;
    }

    public static void BuildCandidateOrder(
        DialogueChatboxPlacement preferred,
        bool avoidCoveringAnchor,
        List<DialogueChatboxPlacement> into)
    {
        into.Clear();
        if (preferred == DialogueChatboxPlacement.Auto)
        {
            into.Add(DialogueChatboxPlacement.Above);
            into.Add(DialogueChatboxPlacement.Right);
            into.Add(DialogueChatboxPlacement.Left);
            into.Add(DialogueChatboxPlacement.Below);
        }
        else
        {
            into.Add(preferred);
            AppendIfMissing(into, DialogueChatboxPlacement.Above);
            AppendIfMissing(into, DialogueChatboxPlacement.Right);
            AppendIfMissing(into, DialogueChatboxPlacement.Left);
            AppendIfMissing(into, DialogueChatboxPlacement.Below);
        }

        if (avoidCoveringAnchor)
            into.Remove(DialogueChatboxPlacement.Below);
    }

    static void AppendIfMissing(List<DialogueChatboxPlacement> list, DialogueChatboxPlacement p)
    {
        if (!list.Contains(p))
            list.Add(p);
    }

    static Vector2 PivotFor(DialogueChatboxPlacement p)
    {
        switch (p)
        {
            case DialogueChatboxPlacement.Above: return new Vector2(0.5f, 0f);
            case DialogueChatboxPlacement.Below: return new Vector2(0.5f, 1f);
            case DialogueChatboxPlacement.Left: return new Vector2(1f, 0.5f);
            case DialogueChatboxPlacement.Right: return new Vector2(0f, 0.5f);
            default: return new Vector2(0.5f, 0f);
        }
    }

    Vector2 AnchorPoint(Vector2 anchor, DialogueChatboxPlacement p, float pad)
    {
        switch (p)
        {
            case DialogueChatboxPlacement.Above:
                return anchor + new Vector2(0f, pad);
            case DialogueChatboxPlacement.Below:
                return anchor + new Vector2(0f, -pad);
            case DialogueChatboxPlacement.Left:
                return anchor + new Vector2(-pad, 0f);
            case DialogueChatboxPlacement.Right:
                return anchor + new Vector2(pad, 0f);
            default:
                return anchor + new Vector2(0f, pad);
        }
    }

    /// <summary>
    /// Clamp so the full rect stays inside safe, given pivot (position is pivot point in screen space).
    /// </summary>
    static Vector2 ClampPivotPosition(Vector2 pivotPos, Vector2 size, Vector2 pivot, Rect safe)
    {
        float left = pivotPos.x - size.x * pivot.x;
        float bottom = pivotPos.y - size.y * pivot.y;
        float right = left + size.x;
        float top = bottom + size.y;

        if (left < safe.xMin) pivotPos.x += safe.xMin - left;
        if (right > safe.xMax) pivotPos.x -= right - safe.xMax;
        if (bottom < safe.yMin) pivotPos.y += safe.yMin - bottom;
        if (top > safe.yMax) pivotPos.y -= top - safe.yMax;
        return pivotPos;
    }

    static Rect Intersect(Rect a, Rect b)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMax = Mathf.Min(a.yMax, b.yMax);
        if (xMax <= xMin || yMax <= yMin)
            return a;
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }
}
