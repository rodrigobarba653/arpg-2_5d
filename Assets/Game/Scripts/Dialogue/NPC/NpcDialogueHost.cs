using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC dialogue configuration: Ambient / Interactive / Both.
/// Prompt Mode is independent (configured here for IInteractable when interactive).
/// </summary>
[DisallowMultipleComponent]
public class NpcDialogueHost : MonoBehaviour, IInteractable
{
    public enum AmbientRepeatMode
    {
        Once = 0,
        Repeat = 1,
    }

    [Header("Mode")]
    public NpcDialogueMode mode = NpcDialogueMode.Both;

    [Header("Ambient")]
    public DialogueAsset ambientDialogue;
    public float ambientProximityRadius = 3.5f;
    public AmbientRepeatMode ambientRepeat = AmbientRepeatMode.Repeat;
    public float ambientCooldownSeconds = 4f;
    public bool dismissAmbientOnExit = true;
    public float ambientHoldSeconds = 2f;
    public DialogueChatboxPlacement ambientPreferredPlacement = DialogueChatboxPlacement.Above;
    public Vector2 ambientManualOffset;
    public float ambientScale = 1f;

    [SerializeReference]
    public List<EventCondition> ambientConditions = new List<EventCondition>();

    [Header("Interactive")]
    [Tooltip("Shared / fallback interactive dialogue when no character override matches.")]
    public DialogueAsset interactiveDialogue;

    [Tooltip("Optional per-character assets (CharacterDefinition.id → DialogueAsset). " +
             "Resolved via DialogueCharacterAssetResolver; reused later by StartDialogueAction.")]
    public List<DialogueCharacterAssetOverride> interactiveByCharacter =
        new List<DialogueCharacterAssetOverride>();

    [Tooltip("speakerId used in DialogueAsset lines for this NPC (e.g. villager_a). " +
             "Use speakerId 'active_player' for the controlling party member.")]
    public string npcSpeakerId = "";

    public bool interactiveLockGameplay = true;
    public bool allowCancelInteractive = true;

    [Tooltip("Manual waits for Confirm. Automatic advances lines and still waits on a choice.")]
    public DialogueProgressionMode interactiveProgression = DialogueProgressionMode.Manual;

    [Tooltip("Auto = Above → Right → Left → Below. Uses global DialogueService layout settings.")]
    public DialogueChatboxPlacement interactivePreferredPlacement = DialogueChatboxPlacement.Auto;

    [Tooltip("Extra offset in Canvas reference units.")]
    public Vector2 interactiveManualOffset;

    public float interactiveScale = 1f;

    [Tooltip("<= 0 uses DialogueService.layoutSettings.interactiveMaxWidthRef.")]
    public float interactiveMaxWidthOverride = 0f;

    [Tooltip("Reserved for a future 2.5D-facing gate. Phase A ignores this — interaction is always 360° around the NPC.")]
    [Range(0f, 180f)]
    public float requireFacingWithinDegrees = 0f;

    [Header("Interaction (Sprint 04 — independent of dialogue mode)")]
    public InteractPromptMode promptMode = InteractPromptMode.InteractionOnly;
    public float discoveryRadius = 4.5f;
    public float interactionRadius = 2.5f;
    public Transform interactionPoint;
    public Transform speechAnchor;

    [Header("Availability")]
    public bool available = true;

    AmbientDialogueHandle ambientHandle;
    float ambientCooldownUntil;
    bool ambientSpent;
    string ambientNextNodeId;
    bool playerInAmbientRange;

    public InteractPromptMode PromptMode => promptMode;
    public float DiscoveryRadius => discoveryRadius;
    public float InteractionRadius => interactionRadius;

    /// <summary>
    /// Horizontal center of the NPC (ignores speech-anchor height). Always 360° planar.
    /// </summary>
    public Vector3 InteractionPosition
    {
        get
        {
            // Prefer explicit volume/point XZ; fall back to this transform.
            Vector3 p = transform.position;
            if (interactionPoint != null)
            {
                p.x = interactionPoint.position.x;
                p.z = interactionPoint.position.z;
            }
            return p;
        }
    }

    Transform Anchor => speechAnchor != null ? speechAnchor : transform;

    public bool IsAvailable
    {
        get
        {
            if (!isActiveAndEnabled || !available)
                return false;
            if (mode == NpcDialogueMode.AmbientOnly)
                return false;
            if (interactiveDialogue == null)
                return false;
            if (DialogueService.Instance != null && DialogueService.Instance.HasInteractive)
                return false;
            // 360° — do not gate on transform.forward (billboards face the camera).
            return true;
        }
    }

    public bool IsDiscoverable => IsAvailable;

    void Awake()
    {
        EnsureInteractionVolume();
    }

    void OnEnable()
    {
        EnsureInteractionVolume();
        if (mode != NpcDialogueMode.AmbientOnly)
            InteractableRegistry.Register(this);
    }

    void OnDisable()
    {
        InteractableRegistry.Unregister(this);
        ambientHandle?.Dismiss(suppressCooldown: true);
        ambientHandle = null;
    }

    void OnValidate()
    {
        if (discoveryRadius < interactionRadius)
            discoveryRadius = interactionRadius;
    }

    void Update()
    {
        if (mode == NpcDialogueMode.InteractiveOnly)
            return;

        UpdateAmbient();
    }

    void UpdateAmbient()
    {
        if (ambientDialogue == null)
            return;

        var player = Party.Active;
        if (player == null)
            return;

        Vector3 a = player.transform.position;
        Vector3 b = Anchor.position;
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        float dist = Mathf.Sqrt(dx * dx + dz * dz);
        bool inRange = dist <= ambientProximityRadius;
        playerInAmbientRange = inRange;

        if (!inRange)
        {
            if (dismissAmbientOnExit && ambientHandle != null && !ambientHandle.IsDead)
                ambientHandle.Dismiss(suppressCooldown: false);
            return;
        }

        if (ambientHandle != null && !ambientHandle.IsDead)
            return;

        if (DialogueService.Instance != null && DialogueService.Instance.HasInteractive)
            return;

        if (Time.time < ambientCooldownUntil)
            return;

        if (ambientRepeat == AmbientRepeatMode.Once && ambientSpent && string.IsNullOrEmpty(ambientNextNodeId))
            return;

        if (!EvaluateAmbientConditions())
            return;

        TryStartAmbientBurst();
    }

    bool EvaluateAmbientConditions()
    {
        if (ambientConditions == null)
            return true;
        for (int i = 0; i < ambientConditions.Count; i++)
        {
            var c = ambientConditions[i];
            if (c != null && !c.Evaluate())
                return false;
        }
        return true;
    }

    void TryStartAmbientBurst()
    {
        var service = DialogueService.Ensure();
        if (service == null)
            return;

        DialogueLineNode line = ResolveAmbientLine();
        if (line == null)
            return;

        ambientHandle = service.TryBeginAmbient(
            this,
            ambientDialogue,
            line,
            Anchor,
            ambientManualOffset,
            ambientPreferredPlacement,
            ambientScale,
            ambientHoldSeconds);

        if (ambientHandle == null)
            return; // postponed/suppressed — retry next frames

        // Advance chain pointer for multi-line ambient.
        ambientNextNodeId = line.nextNodeId;
        if (string.IsNullOrEmpty(ambientNextNodeId) ||
            ambientDialogue.FindNode(ambientNextNodeId) is DialogueEndNode ||
            !(ambientDialogue.FindNode(ambientNextNodeId) is DialogueLineNode))
        {
            ambientNextNodeId = null;
            if (ambientRepeat == AmbientRepeatMode.Once)
                ambientSpent = true;
        }
    }

    DialogueLineNode ResolveAmbientLine()
    {
        if (!string.IsNullOrEmpty(ambientNextNodeId))
        {
            var n = ambientDialogue.FindNode(ambientNextNodeId) as DialogueLineNode;
            if (n != null)
                return n;
            ambientNextNodeId = null;
        }

        var entry = ambientDialogue.GetEntry();
        return entry as DialogueLineNode;
    }

    public void NotifyAmbientFinished(bool suppressCooldown)
    {
        ambientHandle = null;
        if (!suppressCooldown)
            ambientCooldownUntil = Time.time + Mathf.Max(0f, ambientCooldownSeconds);
    }

    public bool WouldShowInteractionPrompt(float distanceToPlayer)
    {
        if (promptMode == InteractPromptMode.Hidden)
            return false;
        if (!IsAvailable)
            return false;
        return distanceToPlayer <= interactionRadius;
    }

    public bool WouldShowDiscoveryPrompt(float distanceToPlayer)
    {
        if (promptMode != InteractPromptMode.DiscoveryAndInteraction)
            return false;
        if (!IsDiscoverable)
            return false;
        if (distanceToPlayer > discoveryRadius)
            return false;
        if (IsAvailable && distanceToPlayer <= interactionRadius)
            return false;
        return true;
    }

    public bool TryInteract()
    {
        if (!IsAvailable)
            return false;

        var service = DialogueService.Ensure();
        if (service == null)
        {
            Debug.LogWarning("[NpcDialogueHost] No DialogueService.", this);
            return false;
        }

        // Dismiss own ambient before interactive.
        ambientHandle?.Dismiss(suppressCooldown: false);

        var asset = DialogueCharacterAssetResolver.ResolveForActivePartyMember(
            interactiveDialogue,
            interactiveByCharacter);

        if (asset == null)
        {
            Debug.LogWarning(
                "[NpcDialogueHost] No interactive DialogueAsset for active character (and no fallback).",
                this);
            return false;
        }

        return service.TryStartInteractive(
            asset,
            Anchor,
            npcSpeakerId,
            interactiveManualOffset,
            interactivePreferredPlacement,
            interactiveScale,
            interactiveMaxWidthOverride,
            interactiveLockGameplay,
            allowCancelInteractive,
            progressionMode: interactiveProgression);
    }

    void EnsureInteractionVolume()
    {
        // Dedicated horizontal volume so range is a clear cylinder/sphere around the NPC.
        Transform vol = transform.Find("InteractionVolume");
        if (vol == null)
        {
            var go = new GameObject("InteractionVolume");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            vol = go.transform;
            var sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
        }

        var col = vol.GetComponent<SphereCollider>();
        if (col != null)
        {
            col.isTrigger = true;
            col.radius = Mathf.Max(0.5f, interactionRadius);
        }

        if (interactionPoint == null)
            interactionPoint = vol;

        // Body collider must not block approach from any side.
        var bodyCols = GetComponents<Collider>();
        for (int i = 0; i < bodyCols.Length; i++)
        {
            if (bodyCols[i] != null && bodyCols[i].transform == transform)
                bodyCols[i].isTrigger = true;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 p = InteractionPosition;
        p.y = transform.position.y;

        if (mode != NpcDialogueMode.InteractiveOnly)
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, ambientProximityRadius);
        }

        if (mode != NpcDialogueMode.AmbientOnly)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.5f);
            Gizmos.DrawWireSphere(p, interactionRadius);
        }
    }
#endif
}
