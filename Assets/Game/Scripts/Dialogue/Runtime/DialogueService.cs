using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central dialogue director. Interactive has priority over ambient.
/// Safe-area / priority conventions leave room for Sprint 07 notifications.
/// </summary>
[DefaultExecutionOrder(-50)]
public class DialogueService : MonoBehaviour
{
    public static DialogueService Instance { get; private set; }

    [Header("Presentation")]
    public DialogueUIController ui;
    public CharacterPortraitRoster roster;
    public DialogueUiSafeArea safeArea = new DialogueUiSafeArea();

    [Header("Layout (global defaults)")]
    public DialogueLayoutSettings layoutSettings = new DialogueLayoutSettings();

    [Min(0)]
    public int maxAmbientChatboxes = 2;

    public bool debugLog = true;

    public DialogueLayoutSettings LayoutSettings => layoutSettings;

    public DialogueUIController UI
    {
        get
        {
            EnsureUI();
            return ui;
        }
    }

    public DialogueLayoutSolver Layout { get; private set; }

    public bool HasInteractive => interactive != null && !interactive.IsFinished;
    public DialogueSession InteractiveSession => interactive;

    readonly List<AmbientDialogueHandle> ambients = new List<AmbientDialogueHandle>(4);
    DialogueSession interactive;
    readonly List<System.Action> deferredOnInteractiveEnd = new List<System.Action>(4);

    /// <summary>
    /// Phase C hook: queue work until interactive dialogue ends (e.g. deferred EventRunners).
    /// CancelDialogue clears these — never silently drop without policy.
    /// </summary>
    public void EnqueueAfterInteractive(System.Action action)
    {
        if (action == null)
            return;
        if (!HasInteractive)
        {
            action.Invoke();
            return;
        }
        deferredOnInteractiveEnd.Add(action);
    }

    public void ClearDeferredAfterInteractive(string reason)
    {
        if (deferredOnInteractiveEnd.Count == 0)
            return;
        if (debugLog)
            Debug.Log($"[Dialogue] Clearing {deferredOnInteractiveEnd.Count} deferred actions ({reason}).", this);
        deferredOnInteractiveEnd.Clear();
    }

    /// <summary>Lazy create for TestLab / play without a pre-placed service.</summary>
    public static DialogueService Ensure()
    {
        if (Instance != null)
            return Instance;

        var existing = Object.FindFirstObjectByType<DialogueService>();
        if (existing != null)
            return existing;

        var go = new GameObject("DialogueService");
        return go.AddComponent<DialogueService>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Layout = new DialogueLayoutSolver
        {
            safeArea = safeArea,
            settings = layoutSettings
        };
        EnsureUI();
        SyncLayoutFromUI();
    }

    /// <summary>Push safe-area, layout settings, and Canvas scale into the shared solver.</summary>
    public void SyncLayoutFromUI()
    {
        if (Layout == null)
            Layout = new DialogueLayoutSolver();

        Layout.safeArea = safeArea;
        Layout.settings = layoutSettings != null ? layoutSettings : new DialogueLayoutSettings();

        EnsureUI();
        float scale = 1f;
        if (ui != null && ui.canvas != null)
            scale = ui.canvas.scaleFactor;
        Layout.canvasScaleFactor = scale > 0.01f ? scale : 1f;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (interactive != null && !interactive.IsFinished)
            interactive.Cancel("service-destroy");

        for (int i = ambients.Count - 1; i >= 0; i--)
            ambients[i]?.Dismiss(suppressCooldown: true);

        ambients.Clear();
        ClearDeferredAfterInteractive("service-destroy");
    }

    void LateUpdate()
    {
        SyncLayoutFromUI();

        var interactiveRects = new List<Rect>(4);
        if (HasInteractive)
            interactive.CollectScreenRects(interactiveRects);

        if (interactiveRects.Count > 0)
            Layout.BeginFrame(interactiveRects[0], useDefaultReserveBand: false);
        else
            Layout.BeginFrame(null, useDefaultReserveBand: true);

        for (int r = 0; r < interactiveRects.Count; r++)
            Layout.RegisterOccupied(interactiveRects[r]);

        // Ambient views re-place themselves via handles.
        for (int i = ambients.Count - 1; i >= 0; i--)
        {
            var h = ambients[i];
            if (h == null || h.IsDead)
            {
                ambients.RemoveAt(i);
                continue;
            }

            h.TickLayout(Layout, HasInteractive);
        }
    }

    public bool TryStartInteractive(
        DialogueAsset asset,
        Transform npcAnchor,
        string npcSpeakerId,
        Vector2 manualOffsetRef,
        DialogueChatboxPlacement preferred,
        float scale,
        float maxWidthRef = 0f,
        bool? lockGameplayOverride = null,
        bool allowCancel = true,
        string initiatorCharacterId = null,
        Transform initiatorTransform = null,
        DialogueProgressionMode progressionMode = DialogueProgressionMode.Manual,
        float autoAdvanceHoldSeconds = 1.75f)
    {
        if (asset == null)
        {
            Debug.LogWarning("[Dialogue] TryStartInteractive — asset is null.", this);
            return false;
        }

        if (HasInteractive)
        {
            if (debugLog)
                Debug.Log("[Dialogue] Interactive already running — reject start.", this);
            return false;
        }

        // Interactive takes priority: dismiss ambients that would fight for attention.
        DismissAllAmbient("interactive-start");

        float width = maxWidthRef > 1f
            ? maxWidthRef
            : (layoutSettings != null ? layoutSettings.interactiveMaxWidthRef : 560f);

        bool lockGp = lockGameplayOverride ?? asset.lockGameplay;
        interactive = new DialogueSession(
            this,
            asset,
            npcAnchor,
            npcSpeakerId,
            roster,
            lockGp,
            manualOffsetRef,
            preferred,
            scale,
            width,
            allowCancel,
            initiatorCharacterId,
            initiatorTransform,
            progressionMode,
            autoAdvanceHoldSeconds);

        interactive.Start(this);
        if (debugLog)
            Debug.Log($"[Dialogue] Interactive start '{asset.dialogueId}'.", this);
        return true;
    }

    /// <summary>Cancel the running interactive session if any (e.g. narrative Interrupt policy).</summary>
    public void CancelInteractive(string reason = "cancel")
    {
        if (interactive == null || interactive.IsFinished)
            return;
        interactive.Cancel(reason);
    }

    public void NotifyInteractiveFinished(DialogueSession session)
    {
        if (interactive != session)
            return;

        interactive = null;

        // Deferred work runs for every terminal outcome (Completed, Cancelled, Failed).
        // Phase C consumers that grant narrative consequences must check
        // session.Outcome == DialogueSessionOutcome.Completed before applying them.
        var copy = deferredOnInteractiveEnd.ToArray();
        deferredOnInteractiveEnd.Clear();
        for (int i = 0; i < copy.Length; i++)
        {
            try { copy[i]?.Invoke(); }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    public AmbientDialogueHandle TryBeginAmbient(
        NpcDialogueHost host,
        DialogueAsset asset,
        DialogueLineNode line,
        Transform anchor,
        Vector2 manualOffset,
        DialogueChatboxPlacement preferred,
        float scale,
        float holdSeconds)
    {
        if (host == null || asset == null || line == null)
            return null;

        if (HasInteractive)
            return null; // postpone — host will retry

        int living = 0;
        for (int i = 0; i < ambients.Count; i++)
            if (ambients[i] != null && !ambients[i].IsDead)
                living++;

        if (living >= maxAmbientChatboxes)
            return null;

        var handle = new AmbientDialogueHandle(
            this, host, asset, line, anchor, manualOffset, preferred, scale, holdSeconds);
        if (!handle.TryShow(Layout))
        {
            handle.Dismiss(suppressCooldown: true);
            return null; // no valid placement
        }

        ambients.Add(handle);
        return handle;
    }

    public void UnregisterAmbient(AmbientDialogueHandle handle)
    {
        ambients.Remove(handle);
    }

    public void DismissAllAmbient(string reason)
    {
        for (int i = ambients.Count - 1; i >= 0; i--)
            ambients[i]?.Dismiss(suppressCooldown: false);
        ambients.Clear();
        if (debugLog)
            Debug.Log($"[Dialogue] Dismiss all ambient ({reason}).", this);
    }

    void EnsureUI()
    {
        if (ui == null)
            ui = GetComponentInChildren<DialogueUIController>();
        if (ui == null)
            ui = DialogueUIController.CreateRuntime(transform);
    }
}
