using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Interactive conversation. Phase A: Line + End.
/// Conversation-level layout: speakers/slots are planned before the first line.
/// Active-player identity is frozen at Start; world anchors are followed live.
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
    DialogueChoiceView choiceView;
    string choiceDestination;
    readonly List<DialogueChoiceOption> choiceScratch = new List<DialogueChoiceOption>(8);

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
        this.autoAdvanceHoldSeconds = autoAdvanceHoldSeconds > 0.05f ? autoAdvanceHoldSeconds : 1.75f;
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

                if (node is DialogueLineNode line)
                {
                    yield return PlayLine(line);
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
                    yield return PlayChoice(choice);
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

                Fail("unsupported-node", node.nodeId, $"type='{node.GetType().Name}'");
                break;
            }
        }

        Finish();
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

        if (plan == null || !plan.TryGet(speakerKey, out _))
            BuildOrRebuildConversationPlan(force: true);

        var view = GetOrCreateBubble(speakerKey);
        View = view;
        Transform anchor = ResolveAnchorForResolvedId(line.speakerId, resolvedId);
        anchorBySpeaker[speakerKey] = anchor;
        if (plan != null && plan.TryGet(speakerKey, out var slot))
        {
            slot.anchor = anchor;
            slot.visible = true;
        }

        string speaker = line.speakerDisplayName;
        if (string.IsNullOrEmpty(speaker) && roster != null && roster.FindCharacter(resolvedId) != null)
            speaker = roster.FindDisplayName(resolvedId);
        if (string.IsNullOrEmpty(speaker))
            speaker = DialogueActiveCharacter.FallbackDisplayName(resolvedId);

        view.SetSpeaker(speaker);

        Sprite portrait = null;
        if (roster != null)
            portrait = roster.FindPortrait(resolvedId, line.portraitExpression);
        view.SetPortrait(portrait);

        string fullText = DialogueTextResolver.Resolve(
            line.text,
            line.textVariants,
            SessionActiveCharacterId);
        int visibleChars = 0;
        bool lineComplete = string.IsNullOrEmpty(fullText);
        view.PrepareBodyFit(fullText);
        view.SetBodyVisibleText("");
        view.SetVisible(true);

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
                    Cancel("user-cancel", stopRoutine: false);
                    yield break;
                }

                // Optional: Confirm skips the remainder of the hold.
                if (DialogueInput.ConfirmPressedThisFrame())
                {
                    DialogueInput.SuppressConfirmUntilReleased();
                    yield break;
                }

                t += Time.deltaTime;
                yield return null;
            }
            yield break;
        }

        while (!WasCancelled)
        {
            RelayoutAll();

            if (allowCancel && DialogueInput.CancelPressedThisFrame())
            {
                Cancel("user-cancel", stopRoutine: false);
                yield break;
            }

            if (DialogueInput.ConfirmPressedThisFrame())
            {
                DialogueInput.SuppressConfirmUntilReleased();
                yield break;
            }

            yield return null;
        }
    }

    void BuildOrRebuildConversationPlan(bool force)
    {
        service.SyncLayoutFromUI();
        var layout = service.Layout;

        CollectSpeakersIntoOrder();
        draftScratch.Clear();

        for (int i = 0; i < speakerOrder.Count; i++)
        {
            string key = speakerOrder[i];
            Transform anchor = null;
            if (!anchorBySpeaker.TryGetValue(key, out anchor) || !IsAlive(anchor))
                anchor = ResolveAnchorForResolvedId(key, key);
            if (IsAlive(anchor))
                anchorBySpeaker[key] = anchor;

            Vector2 screen = IsAlive(anchor)
                ? WorldToScreen(anchor.position)
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            draftScratch.Add(new ConversationSpeakerSlot
            {
                speakerId = key,
                anchor = anchor,
                screenAnchor0 = screen,
                isPlayer = IsSessionPlayerKey(key),
            });
        }

        if (!force && plan != null && !layout.ShouldRebuildConversationPlan(plan, draftScratch))
        {
            // Refresh live anchors on existing slots only.
            for (int i = 0; i < plan.speakers.Count; i++)
            {
                var s = plan.speakers[i];
                if (s == null) continue;
                if (anchorBySpeaker.TryGetValue(s.speakerId, out var a) && IsAlive(a))
                    s.anchor = a;
            }
            return;
        }

        // Preserve visibility flags across rebuild.
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

        Vector2 sizeHint = GetInteractiveSizeHint(layout);
        plan = layout.BuildConversationPlan(draftScratch, sizeHint);

        for (int i = 0; i < plan.speakers.Count; i++)
        {
            var s = plan.speakers[i];
            if (s == null) continue;
            if (wasVisible.TryGetValue(s.speakerId, out bool v))
                s.visible = v;
        }

        if (service.debugLog)
            Debug.Log(
                $"[Dialogue] Conversation layout plan built for {plan.speakers.Count} speaker(s).",
                runnerHost);
    }

    void CollectSpeakersIntoOrder()
    {
        speakerOrder.Clear();
        var seen = new HashSet<string>();

        if (Asset == null || Asset.nodes == null)
            return;

        // Reachable walk from entry (Phase A linear + simple branches via next ids).
        var queue = new Queue<string>();
        string entry = Asset.entryNodeId;
        if (string.IsNullOrEmpty(entry) && Asset.nodes.Count > 0 && Asset.nodes[0] != null)
            entry = Asset.nodes[0].nodeId;
        if (!string.IsNullOrEmpty(entry))
            queue.Enqueue(entry);

        var visitedNodes = new HashSet<string>();
        while (queue.Count > 0)
        {
            string id = queue.Dequeue();
            if (string.IsNullOrEmpty(id) || !visitedNodes.Add(id))
                continue;

            var node = Asset.FindNode(id);
            if (node == null)
                continue;

            if (node is DialogueLineNode line)
            {
                string resolved = DialogueActiveCharacter.ResolveSpeakerId(line.speakerId, SessionActiveCharacterId);
                string key = string.IsNullOrEmpty(resolved) ? "_default" : resolved;
                if (seen.Add(key))
                    speakerOrder.Add(key);

                if (!string.IsNullOrEmpty(line.nextNodeId))
                    queue.Enqueue(line.nextNodeId);
            }
            else if (node is DialogueEndNode)
            {
                // stop branch
            }
            else if (node is DialogueChoiceNode choiceNode)
            {
                // Follow authored destinations so the existing plan already
                // contains every speaker on those branches. Opening the menu
                // does not rebuild. Step 4 replaces this with core/branch slots.
                if (choiceNode.options != null)
                {
                    for (int i = 0; i < choiceNode.options.Count; i++)
                    {
                        var option = choiceNode.options[i];
                        if (option != null && !string.IsNullOrEmpty(option.nextNodeId))
                            queue.Enqueue(option.nextNodeId);
                    }
                }

                if (!string.IsNullOrEmpty(choiceNode.fallbackNodeId))
                    queue.Enqueue(choiceNode.fallbackNodeId);
            }
            else
            {
                // Unknown node types have no edges.
            }
        }

        // Also ensure any already-created bubbles remain in the plan.
        foreach (var kvp in bubbles)
        {
            if (kvp.Value != null && seen.Add(kvp.Key))
                speakerOrder.Add(kvp.Key);
        }
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
            Transform anchor = GetLiveAnchor(key);
            Vector2 screen = IsAlive(anchor)
                ? WorldToScreen(anchor.position)
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            draftScratch.Add(new ConversationSpeakerSlot
            {
                speakerId = key,
                anchor = anchor,
                screenAnchor0 = screen,
                isPlayer = IsSessionPlayerKey(key),
            });
        }

        if (plan == null || layout.ShouldRebuildConversationPlan(plan, draftScratch))
            BuildOrRebuildConversationPlan(force: true);

        Vector2 sizeHint = GetInteractiveSizeHint(layout);

        // Follow reserved slots only — no reactive sticky search.
        for (int i = 0; i < speakerOrder.Count; i++)
        {
            string key = speakerOrder[i];
            if (!bubbles.TryGetValue(key, out var view) || view == null)
                continue;
            if (!view.gameObject.activeInHierarchy)
                continue;
            // Skip fully hidden bubbles (alpha 0) but keep slot reserved.
            if (view.canvasGroup != null && view.canvasGroup.alpha < 0.01f)
                continue;

            if (plan == null || !plan.TryGet(key, out var slot))
                continue;

            Transform anchor = GetLiveAnchor(key);
            if (IsAlive(anchor))
                slot.anchor = anchor;

            Vector2 screen = IsAlive(slot.anchor)
                ? WorldToScreen(slot.anchor.position)
                : (IsAlive(anchor) ? WorldToScreen(anchor.position) : slot.screenAnchor0);

            layout.FollowConversationSlot(view, slot, screen, sizeHint, manualOffsetRef);
        }
    }

    Vector2 GetInteractiveSizeHint(DialogueLayoutSolver layout)
    {
        float widthRef = maxWidthRef > 1f
            ? maxWidthRef
            : (service.LayoutSettings != null ? service.LayoutSettings.interactiveMaxWidthRef : 560f);
        float heightRef = service.LayoutSettings != null ? service.LayoutSettings.interactiveHeightRef : 220f;
        return new Vector2(layout.RefToScreen(widthRef), layout.RefToScreen(heightRef));
    }

    DialogueChatboxView GetOrCreateBubble(string speakerKey)
    {
        if (bubbles.TryGetValue(speakerKey, out var existing) && existing != null)
            return existing;

        var view = service.UI.SpawnInteractive();
        if (scale > 0.01f)
            view.rectTransform.localScale = Vector3.one * scale;

        float widthRef = maxWidthRef > 1f
            ? maxWidthRef
            : (service.LayoutSettings != null ? service.LayoutSettings.interactiveMaxWidthRef : 560f);
        float heightRef = service.LayoutSettings != null ? service.LayoutSettings.interactiveHeightRef : 220f;
        view.rectTransform.sizeDelta = new Vector2(widthRef, heightRef);

        if (view.bubbleBackground != null)
        {
            var c = view.bubbleBackground.color;
            c.a = Mathf.Min(c.a, 0.92f);
            view.bubbleBackground.color = c;
        }

        bubbles[speakerKey] = view;
        if (!speakerOrder.Contains(speakerKey))
            speakerOrder.Add(speakerKey);
        return view;
    }

    Transform ResolveAnchorForResolvedId(string authoredSpeakerId, string resolvedId)
    {
        if (DialogueActiveCharacter.IsActivePlayerAlias(authoredSpeakerId))
            return GetSessionPlayerTransform();

        if (!string.IsNullOrEmpty(SessionActiveCharacterId) &&
            string.Equals(resolvedId, SessionActiveCharacterId, System.StringComparison.OrdinalIgnoreCase))
            return GetSessionPlayerTransform();

        var byId = Party.FindByCharacterId(resolvedId);
        if (byId != null)
            return byId.transform;

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

    static Vector2 WorldToScreen(Vector3 world)
    {
        var cam = Camera.main;
        if (cam == null)
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
        var sp = cam.WorldToScreenPoint(world);
        if (sp.z < 0f)
            sp.z = 0f;
        return sp;
    }

    IEnumerator PlayChoice(DialogueChoiceNode choice)
    {
        choiceDestination = null;
        CollectAuthoredOptions(choice, choiceScratch);

        if (choiceScratch.Count == 0)
        {
            if (string.IsNullOrEmpty(choice.fallbackNodeId))
            {
                Fail("empty-choice", choice.nodeId, "no options and no fallback");
                yield break;
            }

            choiceDestination = choice.fallbackNodeId;
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
            labels[i] = string.IsNullOrEmpty(option.text) ? option.optionId : option.text;
        }

        var menu = GetOrCreateChoice();
        menu.Show(labels, index, choice.promptText);
        PlaceChoiceMenu();
        menu.SetSelected(index);

        if (service.debugLog)
        {
            Debug.Log(
                $"[Dialogue] Choice '{choice.nodeId}' options={choiceScratch.Count} asset='{Asset?.dialogueId}'.",
                runnerHost);
        }

        while (Outcome == DialogueSessionOutcome.None)
        {
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
                choiceDestination = choiceScratch[index].nextNodeId ?? "";
                break;
            }

            yield return null;
        }

        HideChoice();
    }

    static void CollectAuthoredOptions(DialogueChoiceNode choice, List<DialogueChoiceOption> into)
    {
        into.Clear();
        if (choice == null || choice.options == null)
            return;

        for (int i = 0; i < choice.options.Count; i++)
        {
            if (choice.options[i] != null)
                into.Add(choice.options[i]);
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
        if (choiceView == null || !choiceView.IsShown)
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
        Vector2 pivot = DialogueChoiceLayout.ResolveBottomCenter(
            presentation,
            safe,
            sizeLocal * scale,
            scale);

        var rt = choiceView.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.position = new Vector3(pivot.x, pivot.y, 0f);
    }

    void Finish()
    {
        if (IsFinished)
            return;

        // Intentional end (End node or empty next) leaves Outcome unset.
        // Cancel and Fail assign their own outcome and must not be replaced.
        if (Outcome == DialogueSessionOutcome.None)
            Outcome = DialogueSessionOutcome.Completed;

        IsFinished = true;
        routine = null;

        foreach (var kvp in bubbles)
        {
            if (kvp.Value != null)
                service.UI.Release(kvp.Value);
        }
        bubbles.Clear();
        anchorBySpeaker.Clear();
        speakerOrder.Clear();
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
