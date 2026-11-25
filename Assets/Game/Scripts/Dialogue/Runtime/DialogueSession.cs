using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Interactive conversation. Phase A: Line + End.
/// Conversation-level layout: speakers/slots are planned before the first line.
/// Active-player identity is frozen at Start. Bubbles place from the live anchor
/// once, then pin on screen when <see cref="DialogueAsset.pinBubblesOnShow"/> is on.
/// </summary>
public class DialogueSession
{
    public DialogueAsset Asset { get; }
    public bool IsFinished { get; private set; }
    public DialogueSessionOutcome Outcome { get; private set; } = DialogueSessionOutcome.None;

    /// <summary>True only when this session was cancelled. A failure is not a cancel.</summary>
    public bool WasCancelled => Outcome == DialogueSessionOutcome.Cancelled;

    public DialogueChatboxView View { get; private set; }

    /// <summary>Frozen CharacterDefinition.id captured at session start (may be empty).</summary>
    public string SessionActiveCharacterId { get; private set; }

    readonly DialogueService service;
    readonly Transform npcAnchor;
    readonly string npcSpeakerId;
    readonly CharacterPortraitRoster roster;
    readonly bool lockGameplay;
    readonly Vector2 manualOffsetRef;
    readonly DialogueChatboxPlacement preferredPlacement;
    readonly float scale;
    readonly float maxWidthRef;
    readonly bool allowCancel;
    readonly string initiatorCharacterIdOverride;
    readonly Transform initiatorTransformOverride;
    readonly DialogueProgressionMode progressionMode;
    readonly float autoAdvanceHoldSeconds;

    readonly Dictionary<string, DialogueChatboxView> bubbles =
        new Dictionary<string, DialogueChatboxView>();
    readonly Dictionary<string, Transform> anchorBySpeaker =
        new Dictionary<string, Transform>();
    readonly List<string> speakerOrder = new List<string>(4);
    readonly List<ConversationSpeakerSlot> draftScratch = new List<ConversationSpeakerSlot>(4);
    /// <summary>Speakers ordered by last line spoken (oldest → newest). Used by Keep Recent / One At A Time.</summary>
    readonly List<string> recentSpeakOrder = new List<string>(4);

    ConversationLayoutPlan plan;

    /// <summary>
    /// Transform of the character who was active when the session started.
    /// Position is read live each layout; identity is not rebound if Party.Active changes.
    /// </summary>
    Transform sessionActiveTransform;

    MonoBehaviour runnerHost;
    Coroutine routine;
    bool holdsLock;
    bool warnedMissingActive;
    bool loggedPathLimit;
    bool loggedBranchClamp;
    int transitionsThisFrame;
    int transitionFrame = -1;
    readonly HashSet<string> automaticVisited = new HashSet<string>();
    readonly HashSet<string> immediateVisited = new HashSet<string>();
    DialogueChoiceView choiceView;
    string choiceDestination;
    CancellationTokenSource consequenceCts;
    EventContext consequenceContext;
    EventAction activeConsequence;
    readonly List<DialogueChoiceOption> choiceScratch = new List<DialogueChoiceOption>(8);
    readonly List<Rect> choiceBlockerScratch = new List<Rect>(4);
    bool choicePlacementLogged;

    public DialogueSession(
        DialogueService service,
        DialogueAsset asset,
        Transform npcAnchor,
        string npcSpeakerId,
        CharacterPortraitRoster roster,
        bool lockGameplay,
        Vector2 manualOffsetRef,
        DialogueChatboxPlacement preferredPlacement,
        float scale,
        float maxWidthRef,
        bool allowCancel,
        string initiatorCharacterIdOverride = null,
        Transform initiatorTransformOverride = null,
        DialogueProgressionMode progressionMode = DialogueProgressionMode.Manual,
        float autoAdvanceHoldSeconds = 1.75f)
    {
        this.service = service;
        Asset = asset;
        this.npcAnchor = npcAnchor;
        this.npcSpeakerId = npcSpeakerId ?? "";
        this.roster = roster;
        this.lockGameplay = lockGameplay;
        this.manualOffsetRef = manualOffsetRef;
        this.preferredPlacement = preferredPlacement;
        this.scale = scale;
        this.maxWidthRef = maxWidthRef;
        this.allowCancel = allowCancel;
        this.initiatorCharacterIdOverride = initiatorCharacterIdOverride;
        this.initiatorTransformOverride = initiatorTransformOverride;
        this.progressionMode = progressionMode;
        // 0 is a real immediate advance. Only a negative value falls back to the default hold.
        this.autoAdvanceHoldSeconds = autoAdvanceHoldSeconds < 0f ? 1.75f : autoAdvanceHoldSeconds;
    }

    public void CollectScreenRects(List<Rect> into)
    {
        foreach (var kvp in bubbles)
        {
            if (kvp.Value != null && kvp.Value.gameObject.activeInHierarchy)
                into.Add(kvp.Value.GetScreenRect());
        }

        if (choiceView != null && choiceView.IsShown)
            into.Add(choiceView.GetScreenRect());
    }

    public void Start(MonoBehaviour host)
    {
        runnerHost = host;
        CaptureSessionActiveIdentity();

        if (lockGameplay)
        {
            GameplayLock.Acquire(this, GameplayLockChannels.PlayerControl);
            holdsLock = true;
        }

        DialogueInput.SuppressConfirmForCurrentPress();
        routine = host.StartCoroutine(Run());
    }

    void CaptureSessionActiveIdentity()
    {
        // Triggered narrative / scripted starts may pass the entering character explicitly.
        if (!string.IsNullOrEmpty(initiatorCharacterIdOverride))
            SessionActiveCharacterId = initiatorCharacterIdOverride;
        else if (DialogueActiveCharacter.TryGetActiveCharacterId(out string id))
            SessionActiveCharacterId = id;
        else
            SessionActiveCharacterId = "";

        if (initiatorTransformOverride != null)
            sessionActiveTransform = initiatorTransformOverride;
        else if (DialogueActiveCharacter.TryGetActiveTransform(out Transform t))
            sessionActiveTransform = t;
        else
            sessionActiveTransform = null;

        if (string.IsNullOrEmpty(SessionActiveCharacterId) && !warnedMissingActive)
        {
            warnedMissingActive = true;
            Debug.LogWarning(
                "[Dialogue] No active character id at session start — ActivePlayer lines will use fallback label.",
                runnerHost);
        }
    }

    public void Cancel(string reason = "cancel", bool stopRoutine = true)
    {
        if (IsFinished)
            return;

        // Cancel is the only writer of Cancelled. Do not replace Completed or Failed.
        if (Outcome == DialogueSessionOutcome.None)
            Outcome = DialogueSessionOutcome.Cancelled;

        AbortConsequences();

        if (stopRoutine && runnerHost != null && routine != null)
        {
            var r = routine;
            routine = null;
            runnerHost.StopCoroutine(r);
        }

        Finish();
        if (Outcome == DialogueSessionOutcome.Cancelled && service.debugLog)
            Debug.Log($"[Dialogue] Session cancelled ({reason}) asset='{Asset?.dialogueId}'.", runnerHost);
    }

    IEnumerator Run()
    {
        var node = Asset != null ? Asset.GetEntry() : null;
        if (node == null)
        {
            Fail("null-entry", Asset != null ? Asset.entryNodeId : "", "no entry node");
        }
        else
        {
            // Plan the full conversation BEFORE any chatbox is shown.
            BuildOrRebuildConversationPlan(force: true);

            while (node != null && Outcome == DialogueSessionOutcome.None)
            {
                if (node is DialogueEndNode)
                    break;

                if (!AdmitNode(node))
                {
                    if (Outcome != DialogueSessionOutcome.None)
                        break;
                    yield return null;
                    continue;
                }

                if (node is DialogueLineNode line)
                {
                    var play = PlayLine(line);
                    if (BeganWaiting(play, out var lineWait))
                    {
                        immediateVisited.Clear();
                        yield return lineWait;
                        yield return play;
                    }
                    if (Outcome != DialogueSessionOutcome.None)
                        break;

                    if (string.IsNullOrEmpty(line.nextNodeId))
                    {
                        node = null;
                        break;
                    }

                    var next = Asset.FindNode(line.nextNodeId);
                    if (next == null)
                    {
                        Fail("missing-node", line.nodeId, $"next='{line.nextNodeId}'");
                        break;
                    }

                    node = next;
                    continue;
                }

                if (node is DialogueChoiceNode choice)
                {
                    var menu = PlayChoice(choice);
                    if (BeganWaiting(menu, out var menuWait))
                    {
                        immediateVisited.Clear();
                        yield return menuWait;
                        yield return menu;
                    }
                    if (Outcome != DialogueSessionOutcome.None)
                        break;

                    if (string.IsNullOrEmpty(choiceDestination))
                    {
                        node = null;
                        break;
                    }

                    var chosen = Asset.FindNode(choiceDestination);
                    if (chosen == null)
                    {
                        Fail("missing-node", choice.nodeId, $"next='{choiceDestination}'");
                        break;
                    }

                    node = chosen;
                    continue;
                }

                if (node is DialogueConditionNode condition)
                {
                    bool passed = DialogueChoiceAvailability.ConditionsAllow(condition.conditions);
                    string nextId = passed ? condition.trueNodeId : condition.falseNodeId;
                    if (service.debugLog)
                    {
                        Debug.Log(
                            $"[Dialogue] Condition '{condition.nodeId}' passed={passed} next='{nextId}' asset='{Asset?.dialogueId}'.",
                            runnerHost);
                    }

                    if (string.IsNullOrEmpty(nextId))
                    {
                        node = null;
                        break;
                    }

                    var next = Asset.FindNode(nextId);
                    if (next == null)
                    {
                        Fail("missing-node", condition.nodeId, $"next='{nextId}'");
                        break;
                    }

                    node = next;
                    continue;
                }

                Fail("unsupported-node", node.nodeId, $"type='{node.GetType().Name}'");
                break;
            }
        }

        Finish();
    }

    /// <summary>
    /// True when the routine actually suspended. A routine that finishes on the
    /// first step stays in this frame so the 32-transition fuse can see it.
    /// </summary>
    static bool BeganWaiting(IEnumerator routine, out object yielded)
    {
        yielded = null;
        if (routine == null || !routine.MoveNext())
            return false;
        yielded = routine.Current;
        return true;
    }

    void Fail(string reason, string nodeId, string detail)
    {
        if (Outcome != DialogueSessionOutcome.None)
            return;

        Outcome = DialogueSessionOutcome.Failed;
        string assetId = Asset != null ? Asset.dialogueId : "";
        Debug.LogWarning(
            $"[Dialogue] Session failed ({reason}) asset='{assetId}' node='{nodeId}' {detail}.",
            runnerHost);
    }

    IEnumerator PlayLine(DialogueLineNode line)
    {
        while (!DialogueInput.ConfirmReleased() && !WasCancelled)
            yield return null;
        DialogueInput.ClearConfirmSuppress();

        string resolvedId = DialogueActiveCharacter.ResolveSpeakerId(line.speakerId, SessionActiveCharacterId);
        string speakerKey = string.IsNullOrEmpty(resolvedId) ? "_default" : resolvedId;
        // Placement is independent of who speaks (Player off-camera → Screen + Active Player).
        bool screenPinned = line.chatboxAim == DialogueChatboxAim.Screen ||
                            DialogueActiveCharacter.IsScreenAlias(line.speakerId);
        if (DialogueActiveCharacter.IsScreenAlias(speakerKey))
            speakerKey = DialogueActiveCharacter.ScreenAlias;

        if (!screenPinned)
        {
            if (plan == null)
                BuildOrRebuildConversationPlan(force: true);
            else if (!plan.TryGet(speakerKey, out _))
                AppendUnplannedSpeaker(speakerKey);
        }

        ApplyBubbleLifetimeBeforeLine(speakerKey);

        var view = GetOrCreateBubble(speakerKey, screenPinned);
        View = view;
        // New (or re-shown) line: place from the character once, then pin.
        view.ClearPinnedScreenPosition();
        view.IsScreenBanner = screenPinned;

        Transform anchor = screenPinned
            ? null
            : ResolveAnchorForResolvedId(line.speakerId, resolvedId);
        anchorBySpeaker[speakerKey] = anchor;
        if (!screenPinned && plan != null && plan.TryGet(speakerKey, out var slot))
        {
            slot.anchor = anchor;
            slot.visible = true;
        }

        string speaker = line.speakerDisplayName;
        if (string.IsNullOrEmpty(speaker))
        {
            var binding = DialogueSpeakerBindingLookup.Find(line.speakerId);
            if (binding != null)
                speaker = binding.ResolvedDisplayName;
        }
        if (string.IsNullOrEmpty(speaker) && roster != null && roster.FindCharacter(resolvedId) != null)
            speaker = roster.FindDisplayName(resolvedId);
        if (string.IsNullOrEmpty(speaker))
            speaker = DialogueActiveCharacter.FallbackDisplayName(resolvedId);

        view.SetSpeaker(speaker);

        Sprite portrait = DialoguePortraitResolver.Resolve(line, line.speakerId, resolvedId, roster);
        view.SetPortrait(portrait);

        string fullText = DialogueTextResolver.Resolve(
            line.text,
            line.textVariants,
            SessionActiveCharacterId);
        int visibleChars = 0;
        bool lineComplete = string.IsNullOrEmpty(fullText);
        view.PrepareBodyFit(fullText);
        view.SetBodyVisibleText("");
        view.SetContinuePrompt(false);
        view.SetVisible(true);

        // Voice starts with the line (alongside typewriter). Continue / next line stops it.
        DialogueVoice.PlayLine(line);

        RelayoutAll();

        float cps = line.typewriterCharsPerSecond > 0f
            ? line.typewriterCharsPerSecond
            : (Asset != null ? Asset.defaultTypewriterCharsPerSecond : 40f);
        if (cps < 1f) cps = 1f;

        float accumulator = 0f;

        while (!lineComplete && !WasCancelled)
        {
            RelayoutAll();

            if (allowCancel && DialogueInput.CancelPressedThisFrame())
            {
                view.SetContinuePrompt(false);
                DialogueVoice.Stop();
                Cancel("user-cancel", stopRoutine: false);
                yield break;
            }

            if (DialogueInput.ConfirmPressedThisFrame())
            {
                visibleChars = fullText.Length;
                lineComplete = true;
                view.SetBodyVisibleText(fullText);
                yield return null;
                break;
            }

            float mul = DialogueInput.FastForwardHeld() ? 4f : 1f;
            accumulator += Time.deltaTime * cps * mul;
            int add = Mathf.FloorToInt(accumulator);
            if (add > 0)
            {
                accumulator -= add;
                visibleChars = Mathf.Min(fullText.Length, visibleChars + add);
                view.SetBodyVisibleText(fullText.Substring(0, visibleChars));
                if (visibleChars >= fullText.Length)
                    lineComplete = true;
            }

            yield return null;
        }

        if (progressionMode == DialogueProgressionMode.Automatic)
        {
            float hold = autoAdvanceHoldSeconds;
            float t = 0f;
            while (t < hold && !WasCancelled)
            {
                RelayoutAll();

                if (allowCancel && DialogueInput.CancelPressedThisFrame())
                {
                    view.SetContinuePrompt(false);
                    DialogueVoice.Stop();
                    Cancel("user-cancel", stopRoutine: false);
                    yield break;
                }

                // Optional: Confirm skips the remainder of the hold.
                if (DialogueInput.ConfirmPressedThisFrame())
                {
                    view.SetContinuePrompt(false);
                    DialogueVoice.Stop();
                    DialogueInput.SuppressConfirmUntilReleased();
                    yield break;
                }

                t += Time.deltaTime;
                yield return null;
            }
            view.SetContinuePrompt(false);
            DialogueVoice.Stop();
            yield break;
        }

        // Waiting for Confirm — show classic bottom-right caret.
        view.SetContinuePrompt(true);
        while (!WasCancelled)
        {
            RelayoutAll();

            if (allowCancel && DialogueInput.CancelPressedThisFrame())
            {
                view.SetContinuePrompt(false);
                DialogueVoice.Stop();
                Cancel("user-cancel", stopRoutine: false);
                yield break;
            }

            if (DialogueInput.ConfirmPressedThisFrame())
            {
                view.SetContinuePrompt(false);
                DialogueVoice.Stop();
                DialogueInput.SuppressConfirmUntilReleased();
                yield break;
            }

            yield return null;
        }

        view.SetContinuePrompt(false);
        DialogueVoice.Stop();
    }

    void BuildOrRebuildConversationPlan(bool force)
    {
        service.SyncLayoutFromUI();
        var layout = service.Layout;
        var analysis = DialogueGraph.Analyze(Asset, SessionActiveCharacterId);
        if (analysis.exceededPathLimit && !loggedPathLimit)
        {
            loggedPathLimit = true;
            Debug.LogWarning(
                "[Dialogue] Path enumeration stopped at 1024. Core is the trunk before the first choice; other speakers get unique regions. asset='" +
                (Asset != null ? Asset.dialogueId : "") + "'.",
                runnerHost);
        }

        speakerOrder.Clear();
        for (int i = 0; i < analysis.core.Count; i++)
            speakerOrder.Add(analysis.core[i]);
        for (int i = 0; i < analysis.branch.Count; i++)
            speakerOrder.Add(analysis.branch[i]);
        foreach (var kvp in bubbles)
        {
            if (kvp.Value != null && !ContainsSpeaker(speakerOrder, kvp.Key))
                speakerOrder.Add(kvp.Key);
        }

        FillDraft(speakerOrder);
        if (!force && plan != null && !layout.ShouldRebuildConversationPlan(plan, draftScratch))
        {
            for (int i = 0; i < plan.speakers.Count; i++)
            {
                var s = plan.speakers[i];
                if (s == null) continue;
                if (anchorBySpeaker.TryGetValue(s.speakerId, out var a) && IsAlive(a))
                    s.anchor = a;
            }
            return;
        }

        var wasVisible = new Dictionary<string, bool>(speakerOrder.Count);
        if (plan != null)
        {
            for (int i = 0; i < plan.speakers.Count; i++)
            {
                var s = plan.speakers[i];
                if (s != null)
                    wasVisible[s.speakerId] = s.visible;
            }
        }

        var coreDraft = new List<ConversationSpeakerSlot>(analysis.core.Count);
        for (int i = 0; i < draftScratch.Count; i++)
        {
            if (ContainsSpeaker(analysis.core, draftScratch[i].speakerId))
                coreDraft.Add(draftScratch[i]);
        }

        Vector2 sizeHint = GetInteractiveSizeHint(layout);
        // First-speak order = reachable discovery order (entry walk), then branch ids.
        var speakOrder = new List<string>(analysis.reachable.Count + analysis.branch.Count);
        for (int i = 0; i < analysis.reachable.Count; i++)
            speakOrder.Add(analysis.reachable[i]);
        for (int i = 0; i < analysis.branch.Count; i++)
        {
            if (!ContainsSpeaker(speakOrder, analysis.branch[i]))
                speakOrder.Add(analysis.branch[i]);
        }

        plan = layout.BuildConversationPlan(coreDraft, sizeHint, speakOrder);

        var branchDraft = new List<ConversationSpeakerSlot>(analysis.branch.Count);
        for (int i = 0; i < draftScratch.Count; i++)
        {
            if (ContainsSpeaker(analysis.branch, draftScratch[i].speakerId))
                branchDraft.Add(draftScratch[i]);
        }

        bool clamped = layout.AddBranchSlots(
            plan, branchDraft, analysis.branchRegion, analysis.paths, sizeHint);
        if (clamped && !loggedBranchClamp)
        {
            loggedBranchClamp = true;
            Debug.LogWarning(
                "[Dialogue] Branch chatbox clamped; not all regions fit in the safe area. asset='" +
                (Asset != null ? Asset.dialogueId : "") + "'.",
                runnerHost);
        }

        for (int i = 0; i < speakerOrder.Count; i++)
        {
            if (plan.TryGet(speakerOrder[i], out _))
                continue;
            AppendUnplannedSpeaker(speakerOrder[i]);
        }

        for (int i = 0; i < plan.speakers.Count; i++)
        {
            var s = plan.speakers[i];
            if (s == null) continue;
            if (wasVisible.TryGetValue(s.speakerId, out bool v))
                s.visible = v;
        }

        if (service.debugLog)
        {
            int regions = 0;
            for (int i = 0; i < analysis.branchRegion.Count; i++)
                regions = Mathf.Max(regions, analysis.branchRegion[i] + 1);
            var branchNote = new System.Text.StringBuilder();
            for (int i = 0; i < analysis.branch.Count; i++)
            {
                if (i > 0) branchNote.Append(", ");
                branchNote.Append(analysis.branch[i]);
                branchNote.Append("@");
                branchNote.Append(analysis.branchRegion[i]);
            }
            Debug.Log(
                $"[Dialogue] Conversation layout plan built for {plan.speakers.Count} speaker(s). core={analysis.core.Count} branchRegions={regions} [{branchNote}].",
                runnerHost);
        }
    }

    void FillDraft(List<string> keys)
    {
        draftScratch.Clear();
        Camera cam = DialogueScreenUtility.ResolveCamera();
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            if (DialogueActiveCharacter.IsScreenAlias(key))
                continue;
            Transform anchor = null;
            if (!anchorBySpeaker.TryGetValue(key, out anchor) || !IsAlive(anchor))
                anchor = ResolveAnchorForResolvedId(key, key);
            if (IsAlive(anchor))
                anchorBySpeaker[key] = anchor;

            // Off-camera speakers still project (rule 7); aim at speechAnchor / collider top.
            Vector2 screen = ResolveAimScreen(key, anchor, anchor,
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            float depth = 0f;
            if (IsAlive(anchor) && cam != null)
                depth = Vector3.Distance(cam.transform.position, anchor.position);

            draftScratch.Add(new ConversationSpeakerSlot
            {
                speakerId = key,
                anchor = anchor,
                screenAnchor0 = screen,
                cameraDistance = depth,
                isPlayer = IsSessionPlayerKey(key),
            });
        }
    }

    void AppendUnplannedSpeaker(string speakerKey)
    {
        if (plan == null || string.IsNullOrEmpty(speakerKey) || plan.TryGet(speakerKey, out _))
            return;
        if (DialogueActiveCharacter.IsScreenAlias(speakerKey))
            return;

        int region = 0;
        for (int i = 0; i < plan.speakers.Count; i++)
        {
            var s = plan.speakers[i];
            if (s != null && !s.isCore)
                region++;
        }

        Transform anchor = ResolveAnchorForResolvedId(speakerKey, speakerKey);
        if (IsAlive(anchor))
            anchorBySpeaker[speakerKey] = anchor;
        Vector2 screen = ResolveAimScreen(speakerKey, anchor, anchor,
            new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

        var layout = service.Layout;
        float sideGap = layout.RefToScreen(service.LayoutSettings != null ? service.LayoutSettings.conversationSideGapRef : 36f);
        float lift = layout.RefToScreen(service.LayoutSettings != null ? service.LayoutSettings.conversationLiftRef : 72f);
        float below = layout.RefToScreen(service.LayoutSettings != null && service.LayoutSettings.conversationBelowGapRef > 0.01f
            ? service.LayoutSettings.conversationBelowGapRef
            : 160f);
        // Alternate Below when many Above slots already exist.
        bool preferBelow = (region % 2) == 0;
        var side = preferBelow ? ConversationSlotSide.Below : ConversationSlotSide.Above;
        Vector2 offset = preferBelow
            ? new Vector2(0f, -below - region * sideGap * 0.25f)
            : new Vector2(0f, lift + region * sideGap * 0.25f);
        plan.speakers.Add(new ConversationSpeakerSlot
        {
            speakerId = speakerKey,
            anchor = anchor,
            side = side,
            screenAnchor0 = screen,
            localOffsetScreen = offset,
            isPlayer = IsSessionPlayerKey(speakerKey),
            visible = false,
            isCore = false,
        });

        if (!ContainsSpeaker(speakerOrder, speakerKey))
            speakerOrder.Add(speakerKey);
    }

    static bool ContainsSpeaker(List<string> ids, string key)
    {
        for (int i = 0; i < ids.Count; i++)
        {
            if (string.Equals(ids[i], key, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    void RelayoutAll()
    {
        service.SyncLayoutFromUI();
        var layout = service.Layout;
        layout.BeginFrame(null, useDefaultReserveBand: false);

        // Rare invalidation (camera flip / offscreen / unexpected speaker).
        draftScratch.Clear();
        for (int i = 0; i < speakerOrder.Count; i++)
        {
            string key = speakerOrder[i];
            if (DialogueActiveCharacter.IsScreenAlias(key))
                continue;

            Transform anchor = GetLiveAnchor(key);
            Vector2 screen = ResolveAimScreen(key, anchor, anchor,
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            draftScratch.Add(new ConversationSpeakerSlot
            {
                speakerId = key,
                anchor = anchor,
                screenAnchor0 = screen,
                isPlayer = IsSessionPlayerKey(key),
            });
        }

        // Slots are frozen before the first line. Never recompose mid-dialogue —
        // only append anyone who was not in the original graph analysis.
        if (plan == null)
            BuildOrRebuildConversationPlan(force: true);
        else
        {
            for (int i = 0; i < draftScratch.Count; i++)
            {
                var d = draftScratch[i];
                if (d != null && !plan.TryGet(d.speakerId, out _))
                    AppendUnplannedSpeaker(d.speakerId);
            }
        }

        Vector2 sizeHint = GetInteractiveSizeHint(layout);
        Vector2 screenSizeHint = GetScreenBannerSizeHint(layout);
        bool pin = PinBubblesOnShow;

        // Pass 1 — pinned bubbles keep their screen spot (do not follow walk/move).
        for (int i = 0; i < speakerOrder.Count; i++)
        {
            string key = speakerOrder[i];
            if (!TryGetVisibleBubble(key, out var view))
                continue;
            if (!pin || !view.HasPinnedScreenPosition)
                continue;

            layout.RefreshPinnedBubbleSize(
                view,
                view.IsScreenBanner ? screenSizeHint : sizeHint);
            view.ApplyPinnedScreenPosition();
            layout.RegisterOccupied(view.GetScreenRect());
            AimKitTailAtSpeaker(view, key);
        }

        // Pass 2 — unpinned bubbles place from the live anchor (or screen-top banner).
        for (int i = 0; i < speakerOrder.Count; i++)
        {
            string key = speakerOrder[i];
            if (!TryGetVisibleBubble(key, out var view))
                continue;
            if (pin && view.HasPinnedScreenPosition)
                continue;

            if (view.IsScreenBanner || DialogueActiveCharacter.IsScreenAlias(key))
            {
                layout.PlaceScreenTop(view, screenSizeHint, manualOffsetRef);
                ClearKitTailAim(view);
                if (pin)
                    view.PinCurrentScreenPosition();
                continue;
            }

            if (plan == null || !plan.TryGet(key, out var slot))
                continue;

            Transform anchor = GetLiveAnchor(key);
            if (IsAlive(anchor))
                slot.anchor = anchor;

            Vector2 screen = ResolveAimScreen(key, slot.anchor, anchor, slot.screenAnchor0);

            layout.FollowConversationSlot(view, slot, screen, sizeHint, manualOffsetRef);
            AimKitTailAtSpeaker(view, key);
            if (pin)
                view.PinCurrentScreenPosition();
        }
    }

    void AimKitTailAtSpeaker(DialogueChatboxView view, string speakerKey)
    {
        if (view == null || view.IsScreenBanner)
            return;

        var kit = view.GetComponent<DialogueChatboxKitView>();
        if (kit == null)
            return;

        Transform anchor = GetLiveAnchor(speakerKey);
        Vector2 aim = ResolveAimScreen(speakerKey, anchor, anchor, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        kit.AimTailAtScreen(aim);
    }

    static void ClearKitTailAim(DialogueChatboxView view)
    {
        if (view == null)
            return;
        var kit = view.GetComponent<DialogueChatboxKitView>();
        if (kit != null)
            kit.ClearTailAim();
    }

    Vector2 ResolveAimScreen(string _, Transform primary, Transform fallback, Vector2 fallbackScreen)
    {
        Transform anchor = IsAlive(primary) ? primary : fallback;
        if (!IsAlive(anchor))
            return fallbackScreen;

        Transform root = anchor;
        Transform speech = null;
        var binding = anchor.GetComponent<DialogueSpeakerBinding>()
                      ?? anchor.GetComponentInParent<DialogueSpeakerBinding>();
        if (binding != null)
        {
            root = binding.transform;
            speech = binding.speechAnchor;
        }
        else
        {
            var host = anchor.GetComponent<NpcDialogueHost>()
                       ?? anchor.GetComponentInParent<NpcDialogueHost>();
            if (host != null)
            {
                root = host.transform;
                speech = host.speechAnchor;
            }
        }

        return WorldToScreen(DialogueSpeechAim.ResolveAimWorldPoint(root, speech));
    }

    bool TryGetVisibleBubble(string key, out DialogueChatboxView view)
    {
        view = null;
        if (!bubbles.TryGetValue(key, out view) || view == null)
            return false;
        if (!view.gameObject.activeInHierarchy)
            return false;
        if (view.canvasGroup != null && view.canvasGroup.alpha < 0.01f)
            return false;
        return true;
    }

    bool PinBubblesOnShow => Asset == null || Asset.pinBubblesOnShow;

    DialogueBubbleLifetime ResolveBubbleLifetime()
    {
        return Asset != null ? Asset.bubbleLifetime : DialogueBubbleLifetime.PerSpeakerSticky;
    }

    int ResolveKeepRecentCount()
    {
        int n = Asset != null ? Asset.keepRecentCount : 2;
        return Mathf.Max(1, n);
    }

    /// <summary>
    /// Updates recency and hides bubbles that exceed One At A Time / Keep Recent rules.
    /// Does not hide the speaker about to talk.
    /// </summary>
    void ApplyBubbleLifetimeBeforeLine(string speakerKey)
    {
        TouchRecentSpeak(speakerKey);

        var mode = ResolveBubbleLifetime();
        if (mode == DialogueBubbleLifetime.PerSpeakerSticky)
            return;

        int keep = mode == DialogueBubbleLifetime.OneAtATime ? 1 : ResolveKeepRecentCount();
        HideBubblesOutsideRecent(speakerKey, keep);
    }

    void TouchRecentSpeak(string speakerKey)
    {
        for (int i = recentSpeakOrder.Count - 1; i >= 0; i--)
        {
            if (string.Equals(recentSpeakOrder[i], speakerKey, StringComparison.OrdinalIgnoreCase))
                recentSpeakOrder.RemoveAt(i);
        }

        recentSpeakOrder.Add(speakerKey);
    }

    void HideBubblesOutsideRecent(string currentSpeakerKey, int keepCount)
    {
        keepCount = Mathf.Max(1, keepCount);

        // Newest `keepCount` speakers stay (always includes current after TouchRecentSpeak).
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = recentSpeakOrder.Count - 1; i >= 0 && keep.Count < keepCount; i--)
            keep.Add(recentSpeakOrder[i]);

        keep.Add(currentSpeakerKey);

        var keys = new List<string>(bubbles.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            if (keep.Contains(key))
                continue;
            HideBubble(key);
        }
    }

    void HideBubble(string speakerKey)
    {
        if (!bubbles.TryGetValue(speakerKey, out var view) || view == null)
            return;

        view.SetVisible(false);
        if (plan != null && plan.TryGet(speakerKey, out var slot))
            slot.visible = false;
    }

    Vector2 GetInteractiveSizeHint(DialogueLayoutSolver layout)
    {
        float widthRef = maxWidthRef > 1f
            ? maxWidthRef
            : (service.LayoutSettings != null ? service.LayoutSettings.interactiveMaxWidthRef : 560f);
        float heightRef = service.LayoutSettings != null ? service.LayoutSettings.interactiveHeightRef : 220f;
        return new Vector2(layout.RefToScreen(widthRef), layout.RefToScreen(heightRef));
    }

    DialogueChatboxView GetOrCreateBubble(string speakerKey, bool screenPinned = false)
    {
        if (bubbles.TryGetValue(speakerKey, out var existing) && existing != null)
        {
            // Same speaker can switch Follow ↔ Screen (off-camera); rebuild chrome if needed.
            if (existing.IsScreenBanner == screenPinned)
                return existing;

            service.UI.Release(existing);
            bubbles.Remove(speakerKey);
        }

        var view = screenPinned
            ? service.UI.SpawnInteractive(withTail: false, screenBanner: true)
            : service.UI.SpawnInteractive();
        view.IsScreenBanner = screenPinned;
        if (scale > 0.01f)
            view.rectTransform.localScale = Vector3.one * scale;

        float widthRef = maxWidthRef > 1f
            ? maxWidthRef
            : (service.LayoutSettings != null ? service.LayoutSettings.interactiveMaxWidthRef : 560f);
        float heightRef = service.LayoutSettings != null ? service.LayoutSettings.interactiveHeightRef : 220f;
        if (screenPinned)
        {
            widthRef = Mathf.Max(widthRef, 1100f);
            heightRef = Mathf.Max(heightRef, 320f);
        }
        view.rectTransform.sizeDelta = new Vector2(widthRef, heightRef);

        bubbles[speakerKey] = view;
        if (!speakerOrder.Contains(speakerKey))
            speakerOrder.Add(speakerKey);
        return view;
    }

    Vector2 GetScreenBannerSizeHint(DialogueLayoutSolver layout)
    {
        float widthRef = 1100f;
        float heightRef = service.LayoutSettings != null ? service.LayoutSettings.interactiveHeightRef : 320f;
        heightRef = Mathf.Max(heightRef, 320f);
        return new Vector2(layout.RefToScreen(widthRef), layout.RefToScreen(heightRef));
    }

    Transform ResolveAnchorForResolvedId(string authoredSpeakerId, string resolvedId)
    {
        if (DialogueActiveCharacter.IsScreenAlias(authoredSpeakerId) ||
            DialogueActiveCharacter.IsScreenAlias(resolvedId))
            return null;

        if (DialogueActiveCharacter.IsActivePlayerAlias(authoredSpeakerId))
            return GetSessionPlayerTransform();

        if (!string.IsNullOrEmpty(SessionActiveCharacterId) &&
            string.Equals(resolvedId, SessionActiveCharacterId, System.StringComparison.OrdinalIgnoreCase))
            return GetSessionPlayerTransform();

        var byId = Party.FindByCharacterId(resolvedId);
        if (byId != null)
            return byId.transform;

        // Per-line scene binding (multi-NPC conversations).
        Transform bound = DialogueSpeakerBindingLookup.FindAnchor(authoredSpeakerId);
        if (IsAlive(bound))
            return bound;
        bound = DialogueSpeakerBindingLookup.FindAnchor(resolvedId);
        if (IsAlive(bound))
            return bound;

        // Legacy Start Dialogue / NpcDialogueHost single-anchor fallback.
        if (IsAlive(npcAnchor))
        {
            var marker = npcAnchor.Find(resolvedId);
            if (IsAlive(marker))
                return marker;
        }

        if (!string.IsNullOrEmpty(npcSpeakerId) &&
            string.Equals(resolvedId, npcSpeakerId, System.StringComparison.OrdinalIgnoreCase) &&
            IsAlive(npcAnchor))
            return npcAnchor;

        if (!IsSessionPlayerKey(resolvedId) && IsAlive(npcAnchor))
            return npcAnchor;

        return GetSessionPlayerTransform() ?? (IsAlive(npcAnchor) ? npcAnchor : null);
    }

    Transform GetLiveAnchor(string speakerKey)
    {
        if (anchorBySpeaker.TryGetValue(speakerKey, out var stored) && IsAlive(stored))
            return stored;

        if (plan != null && plan.TryGet(speakerKey, out var slot) && IsAlive(slot.anchor))
        {
            anchorBySpeaker[speakerKey] = slot.anchor;
            return slot.anchor;
        }

        var refreshed = ResolveAnchorForResolvedId(speakerKey, speakerKey);
        if (IsAlive(refreshed))
        {
            anchorBySpeaker[speakerKey] = refreshed;
            return refreshed;
        }

        return null;
    }

    Transform GetSessionPlayerTransform()
    {
        if (IsAlive(sessionActiveTransform))
            return sessionActiveTransform;
        return null;
    }

    bool IsSessionPlayerKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        if (DialogueActiveCharacter.IsActivePlayerAlias(key))
            return true;
        if (!string.IsNullOrEmpty(SessionActiveCharacterId) &&
            string.Equals(key, SessionActiveCharacterId, System.StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    static bool IsAlive(Transform t) => t != null;

    static Vector2 WorldToScreen(Vector3 world) => DialogueScreenUtility.WorldToScreen(world);

    IEnumerator PlayChoice(DialogueChoiceNode choice)
    {
        choiceDestination = null;
        int authored = CountAuthoredOptions(choice);
        CollectVisibleOptions(choice, choiceScratch);

        if (authored == 0)
        {
            if (string.IsNullOrEmpty(choice.fallbackNodeId))
            {
                Fail("empty-choice", choice.nodeId, "no options and no fallback");
                yield break;
            }

            choiceDestination = choice.fallbackNodeId;
            yield break;
        }

        if (choiceScratch.Count == 0)
        {
            if (string.IsNullOrEmpty(choice.fallbackNodeId))
            {
                choiceDestination = "";
                if (service.debugLog)
                {
                    Debug.Log(
                        $"[Dialogue] Choice '{choice.nodeId}' visible=0 completed asset='{Asset?.dialogueId}'.",
                        runnerHost);
                }
                yield break;
            }

            choiceDestination = choice.fallbackNodeId;
            if (service.debugLog)
            {
                Debug.Log(
                    $"[Dialogue] Choice '{choice.nodeId}' visible=0 fallback='{choice.fallbackNodeId}' asset='{Asset?.dialogueId}'.",
                    runnerHost);
            }
            yield break;
        }

        while (!DialogueInput.ConfirmReleased() && Outcome == DialogueSessionOutcome.None)
            yield return null;
        if (Outcome != DialogueSessionOutcome.None)
            yield break;

        DialogueInput.ClearConfirmSuppress();
        DialogueInput.ResetChoiceNavigation();

        int index = 0;
        var labels = new string[choiceScratch.Count];
        for (int i = 0; i < choiceScratch.Count; i++)
        {
            var option = choiceScratch[i];
            string resolved = DialogueTextResolver.Resolve(
                option.text, option.textVariants, SessionActiveCharacterId);
            labels[i] = string.IsNullOrEmpty(resolved) ? option.optionId : resolved;
        }

        var menu = GetOrCreateChoice();
        menu.Show(labels, index, choice.promptText);
        PlaceChoiceMenu();
        menu.SetSelected(index);

        if (service.debugLog)
        {
            Debug.Log(
                $"[Dialogue] Choice '{choice.nodeId}' visible={choiceScratch.Count} authored={authored} asset='{Asset?.dialogueId}'.",
                runnerHost);
        }

        // LowerCenter keeps its existing per-frame placement. NearPlayerChatbox is placed once.
        bool holdPlacement = ResolveChoicePresentation().placement == DialogueChoicePlacement.NearPlayerChatbox;

        while (Outcome == DialogueSessionOutcome.None)
        {
            if (!holdPlacement)
                PlaceChoiceMenu();

            int nav = DialogueInput.ChoiceNavigationThisFrame();
            if (nav != 0 && choiceScratch.Count > 1)
            {
                index = (index + nav) % choiceScratch.Count;
                if (index < 0)
                    index += choiceScratch.Count;
                menu.SetSelected(index);
            }

            if (allowCancel && DialogueInput.CancelPressedThisFrame())
            {
                Cancel("user-cancel", stopRoutine: false);
                yield break;
            }

            if (DialogueInput.ConfirmPressedThisFrame())
            {
                DialogueInput.SuppressConfirmUntilReleased();
                ClearAutomaticCycle();
                DialogueChoiceOption selected = choiceScratch[index];
                HideChoice();
                yield return RunConsequences(choice, selected);
                if (Outcome != DialogueSessionOutcome.None)
                    yield break;
                choiceDestination = selected != null ? selected.nextNodeId ?? "" : "";
                yield break;
            }

            yield return null;
        }

        HideChoice();
    }

    /// <summary>
    /// Runs the confirmed option's actions on a context with no EventRunner.
    /// The destination stays unread until every action finishes without stop or cancel.
    /// </summary>
    IEnumerator RunConsequences(DialogueChoiceNode choice, DialogueChoiceOption option)
    {
        if (option != null && option.rememberChoice)
        {
            string flagId = DialogueRememberFlag.ResolveId(Asset, option);
            if (!string.IsNullOrEmpty(flagId))
            {
                WorldState.SetFlag(flagId, true);
                if (service != null && service.debugLog)
                {
                    Debug.Log(
                        $"[Dialogue] Remembered choice flag='{flagId}' as '{option.rememberAs}' " +
                        $"(runtime WorldState; SaveManager snapshots on save — not auto-disk).",
                        runnerHost);
                }
            }
        }

        var list = option != null ? option.consequences : null;
        if (list == null || list.Count == 0)
            yield break;

        consequenceCts = new CancellationTokenSource();
        consequenceContext = new EventContext(null, consequenceCts.Token);
        try
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (Outcome != DialogueSessionOutcome.None)
                    yield break;

                EventAction action = list[i];
                if (action == null)
                {
                    Debug.LogWarning(
                        $"[Dialogue] Skipping null consequence [{i}] option='{option.optionId}' asset='{Asset?.dialogueId}'.",
                        runnerHost);
                    continue;
                }

                if (action is StartDialogueAction)
                {
                    Fail(
                        "unsupported-consequence",
                        choice != null ? choice.nodeId : "",
                        $"option='{option.optionId}' index={i} StartDialogueAction");
                    yield break;
                }

                if (service.debugLog)
                {
                    Debug.Log(
                        $"[Dialogue] Consequence [{i}] {action.DisplayName} option='{option.optionId}' asset='{Asset?.dialogueId}'.",
                        runnerHost);
                }

                activeConsequence = action;
                IEnumerator step;
                try
                {
                    step = action.Execute(consequenceContext);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, runnerHost);
                    Fail(
                        "consequence-exception",
                        choice != null ? choice.nodeId : "",
                        $"option='{option.optionId}' index={i} {action.DisplayName}");
                    yield break;
                }

                if (step != null)
                {
                    bool pending = true;
                    while (pending)
                    {
                        if (Outcome != DialogueSessionOutcome.None)
                            yield break;

                        if (allowCancel && DialogueInput.CancelPressedThisFrame())
                        {
                            Cancel("user-cancel", stopRoutine: false);
                            yield break;
                        }

                        try
                        {
                            pending = step.MoveNext();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex, runnerHost);
                            Fail(
                                "consequence-exception",
                                choice != null ? choice.nodeId : "",
                                $"option='{option.optionId}' index={i} {action.DisplayName}");
                            yield break;
                        }

                        if (!pending)
                            break;

                        yield return step.Current;
                    }
                }

                activeConsequence = null;

                if (consequenceContext != null && consequenceContext.StopRequested)
                {
                    Fail(
                        "consequence-stop",
                        choice != null ? choice.nodeId : "",
                        $"option='{option.optionId}' index={i} {consequenceContext.StopReason}");
                    yield break;
                }
            }
        }
        finally
        {
            ReleaseConsequenceScope();
        }
    }

    void AbortConsequences()
    {
        if (consequenceCts != null && !consequenceCts.IsCancellationRequested)
            consequenceCts.Cancel();

        EventAction action = activeConsequence;
        EventContext ctx = consequenceContext;
        activeConsequence = null;
        if (action == null || ctx == null)
            return;

        try
        {
            action.Cancel(ctx);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, runnerHost);
        }
    }

    void ReleaseConsequenceScope()
    {
        activeConsequence = null;
        consequenceContext = null;
        if (consequenceCts == null)
            return;

        CancellationTokenSource cts = consequenceCts;
        consequenceCts = null;
        cts.Dispose();
    }

    const int MaxTransitionsPerFrame = 32;

    /// <summary>
    /// Counts this node entry. Automatic mode fails if a line id was already entered
    /// since the last confirmed choice. A condition node fails the same way in every
    /// mode when it repeats before the player has to wait. More than 32 entries in
    /// one frame yields and retries the same node next frame.
    /// </summary>
    bool AdmitNode(DialogueNode node)
    {
        int frame = Time.frameCount;
        if (frame != transitionFrame)
        {
            transitionFrame = frame;
            transitionsThisFrame = 0;
        }

        if (transitionsThisFrame >= MaxTransitionsPerFrame)
        {
            if (service.debugLog)
            {
                Debug.Log(
                    $"[Dialogue] Frame fuse yielded after 32 transitions. asset='{Asset?.dialogueId}'.",
                    runnerHost);
            }
            return false;
        }

        transitionsThisFrame++;

        if (node == null)
            return true;

        if (node is DialogueConditionNode)
            return AdmitImmediate(node.nodeId);

        if (progressionMode != DialogueProgressionMode.Automatic)
            return true;

        return AdmitAutomatic(node.nodeId);
    }

    bool AdmitImmediate(string nodeId)
    {
        string id = nodeId ?? "";
        if (immediateVisited.Add(id))
            return true;

        Fail("automatic-cycle", id, "revisited without a player wait");
        return false;
    }

    bool AdmitAutomatic(string nodeId)
    {
        string id = nodeId ?? "";
        if (automaticVisited.Add(id))
            return true;

        Fail("automatic-cycle", id, "revisited without a choice confirmation");
        return false;
    }

    void ClearAutomaticCycle()
    {
        automaticVisited.Clear();
        immediateVisited.Clear();
    }

    static int CountAuthoredOptions(DialogueChoiceNode choice)
    {
        if (choice == null || choice.options == null)
            return 0;

        int count = 0;
        for (int i = 0; i < choice.options.Count; i++)
        {
            if (choice.options[i] != null)
                count++;
        }
        return count;
    }

    void CollectVisibleOptions(DialogueChoiceNode choice, List<DialogueChoiceOption> into)
    {
        into.Clear();
        if (choice == null || choice.options == null)
            return;

        for (int i = 0; i < choice.options.Count; i++)
        {
            DialogueChoiceOption option = choice.options[i];
            if (option == null)
                continue;
            if (!DialogueChoiceAvailability.IsAvailable(option, SessionActiveCharacterId))
                continue;
            into.Add(option);
        }
    }

    DialogueChoiceView GetOrCreateChoice()
    {
        if (choiceView != null)
            return choiceView;

        choiceView = service.UI.SpawnChoice();
        return choiceView;
    }

    void HideChoice()
    {
        choicePlacementLogged = false;
        if (choiceView != null)
            choiceView.Hide();
    }

    void ReleaseChoice()
    {
        if (choiceView == null)
            return;

        service.UI.ReleaseChoice(choiceView);
        choiceView = null;
    }

    DialogueChoicePresentation ResolveChoicePresentation()
    {
        if (Asset != null && Asset.overrideChoicePresentation)
            return Asset.choicePresentation;

        if (service.LayoutSettings != null)
            return service.LayoutSettings.choicePresentation;

        return DialogueChoicePresentation.LowerCenterDefault;
    }

    void PlaceChoiceMenu()
    {
        if (choiceView == null || choiceView.OptionCount == 0 || choiceView.rectTransform == null)
            return;

        service.SyncLayoutFromUI();
        var layout = service.Layout;
        var presentation = ResolveChoicePresentation();
        float scale = layout != null && layout.canvasScaleFactor > 0.01f
            ? layout.canvasScaleFactor
            : 1f;

        float maxWidth = presentation.maxWidthRef > 8f ? presentation.maxWidthRef : 720f;
        float maxHeight = presentation.maxHeightRef > 8f ? presentation.maxHeightRef : 420f;
        Vector2 sizeLocal = choiceView.PreferredSize(maxWidth, maxHeight);
        choiceView.rectTransform.sizeDelta = sizeLocal;

        Rect safe = layout != null
            ? layout.GetEffectiveSafeScreenRect()
            : new Rect(0f, 0f, Screen.width, Screen.height);

        bool nearPlayer = presentation.placement == DialogueChoicePlacement.NearPlayerChatbox;
        Rect playerRect = default;
        bool hasPlayer = nearPlayer && TryGetPlayerChatboxRect(out playerRect);
        if (!nearPlayer)
            choiceBlockerScratch.Clear();

        var decision = DialogueChoiceLayout.Resolve(
            presentation,
            safe,
            sizeLocal * scale,
            scale,
            hasPlayer,
            playerRect,
            choiceBlockerScratch);

        var rt = choiceView.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.position = new Vector3(decision.pivotScreen.x, decision.pivotScreen.y, 0f);

        if (!choicePlacementLogged && service.debugLog)
        {
            choicePlacementLogged = true;
            string assetId = Asset != null ? Asset.dialogueId : "";
            Debug.Log(
                $"[Dialogue] Choice placement requested={presentation.placement} used={decision.used} fallback={(decision.fellBackToLowerCenter ? "true" : "false")} asset='{assetId}'.",
                runnerHost);
        }

        choiceView.Reveal();
    }

    /// <summary>
    /// Reads the visible chatbox of the frozen session character. Other visible bubbles
    /// are collected so the menu can avoid them. Does not move any bubble.
    /// </summary>
    bool TryGetPlayerChatboxRect(out Rect playerRect)
    {
        playerRect = default;
        choiceBlockerScratch.Clear();

        DialogueChatboxView playerView = null;
        foreach (var kvp in bubbles)
        {
            var view = kvp.Value;
            if (!IsVisibleBubble(view))
                continue;

            if (playerView == null && IsSessionPlayerKey(kvp.Key))
            {
                playerView = view;
                continue;
            }

            choiceBlockerScratch.Add(view.GetScreenRect());
        }

        if (playerView == null)
            return false;

        playerRect = playerView.GetScreenRect();
        return playerRect.width > 1f && playerRect.height > 1f;
    }

    static bool IsVisibleBubble(DialogueChatboxView view)
    {
        if (view == null || !view.gameObject.activeInHierarchy)
            return false;
        if (view.canvasGroup != null && view.canvasGroup.alpha < 0.01f)
            return false;
        return true;
    }

    void Finish()
    {
        if (IsFinished)
            return;

        // Intentional end (End node or empty next) leaves Outcome unset.
        // Cancel and Fail assign their own outcome and must not be replaced.
        if (Outcome == DialogueSessionOutcome.None)
            Outcome = DialogueSessionOutcome.Completed;

        if (service.debugLog && Outcome == DialogueSessionOutcome.Completed)
            Debug.Log($"[Dialogue] Session completed asset='{Asset?.dialogueId}'.", runnerHost);

        IsFinished = true;
        routine = null;
        DialogueVoice.Stop();
        ReleaseConsequenceScope();

        foreach (var kvp in bubbles)
        {
            if (kvp.Value != null)
                service.UI.Release(kvp.Value);
        }
        bubbles.Clear();
        anchorBySpeaker.Clear();
        speakerOrder.Clear();
        recentSpeakOrder.Clear();
        plan = null;
        View = null;
        sessionActiveTransform = null;
        ReleaseChoice();

        if (holdsLock)
        {
            GameplayLock.Release(this);
            holdsLock = false;
        }

        service.NotifyInteractiveFinished(this);
    }
}
