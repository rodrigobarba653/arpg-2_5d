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
    public bool WasCancelled { get; private set; }
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

        WasCancelled = true;
        if (stopRoutine && runnerHost != null && routine != null)
        {
            var r = routine;
            routine = null;
            runnerHost.StopCoroutine(r);
        }

        Finish();
        if (service.debugLog)
            Debug.Log($"[Dialogue] Session cancelled ({reason}) asset='{Asset?.dialogueId}'.", runnerHost);
    }

    IEnumerator Run()
    {
        var node = Asset != null ? Asset.GetEntry() : null;
        if (node == null)
        {
            Debug.LogWarning("[Dialogue] Interactive session has no entry node.", runnerHost);
            Finish();
            yield break;
        }

        // Plan the full conversation BEFORE any chatbox is shown.
        BuildOrRebuildConversationPlan(force: true);

        while (node != null && !WasCancelled)
        {
            if (node is DialogueEndNode)
                break;

            if (node is DialogueLineNode line)
            {
                yield return PlayLine(line);
                if (WasCancelled)
                    break;

                node = string.IsNullOrEmpty(line.nextNodeId)
                    ? null
                    : Asset.FindNode(line.nextNodeId);
                continue;
            }

            Debug.LogWarning(
                $"[Dialogue] Phase A ignoring unsupported node '{node.GetType().Name}' id='{node.nodeId}'.",
                runnerHost);
            break;
        }

        Finish();
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

        string fullText = line.text ?? "";
        int visibleChars = 0;
        bool lineComplete = string.IsNullOrEmpty(fullText);
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
            else
            {
                // Future node types: no edges known in Phase A.
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

    void Finish()
    {
        if (IsFinished)
            return;

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

        if (holdsLock)
        {
            GameplayLock.Release(this);
            holdsLock = false;
        }

        service.NotifyInteractiveFinished(this);
    }
}
