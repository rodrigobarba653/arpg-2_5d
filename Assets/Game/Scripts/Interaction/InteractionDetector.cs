using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Central player-side interaction detector (FFIX-style).
/// Selects the input target and drives the active character's PlayerInteractionPrompt.
/// Prompt always stays above the player head — never at the interactable.
/// </summary>
public class InteractionDetector : MonoBehaviour
{
    [Tooltip("Optional override. Empty = InteractInput defaults (E / Enter / South).")]
    public InputActionReference interactAction;

    [Tooltip("Extra padding when scanning registry (max of radii already per-object).")]
    public float scanPadding = 0.5f;

    public bool debugLog;

    IInteractable inputFocus;
    IInteractable promptFocus;
    InteractPromptState promptState = InteractPromptState.None;

    PlayerInteractionPrompt boundPrompt;
    PlayerHealth boundMember;

    public IInteractable InputFocus => inputFocus;
    public InteractPromptState CurrentPromptState => promptState;

    void OnDisable()
    {
        ApplyPrompt(InteractPromptState.None);
        inputFocus = null;
        promptFocus = null;
        boundPrompt = null;
        boundMember = null;
    }

    void Update()
    {
        ResolveActivePrompt();
        RefreshSelection();
        ApplyPrompt(promptState);

        if (inputFocus == null)
            return;

        if (GameplayLock.IsLocked(GameplayLockChannels.Interact) ||
            GameplayLock.IsPlayerControlLocked)
            return;

        if (!InteractInput.WasPressedThisFrame(interactAction))
            return;

        var target = inputFocus;
        if (debugLog)
            Debug.Log($"[InteractionDetector] Interact → {(target as Component)?.name}", this);

        target.TryInteract();
    }

    void ResolveActivePrompt()
    {
        var active = Party.Active;
        if (active == boundMember && boundPrompt != null)
            return;

        // Hide previous member's prompt when swapping Jyn ↔ Ax.
        if (boundPrompt != null && boundMember != active)
            boundPrompt.SetPromptState(InteractPromptState.None);

        boundMember = active;
        boundPrompt = active != null
            ? active.GetComponentInChildren<PlayerInteractionPrompt>(true)
            : null;
    }

    void RefreshSelection()
    {
        inputFocus = null;
        promptFocus = null;
        promptState = InteractPromptState.None;

        if (boundMember == null)
            return;

        Vector3 origin = boundMember.transform.position;
        var items = InteractableRegistry.Items;

        IInteractable bestBang = null;
        float bestBangDist = float.MaxValue;
        IInteractable bestHidden = null;
        float bestHiddenDist = float.MaxValue;

        IInteractable bestPromptBang = null;
        float bestPromptBangDist = float.MaxValue;
        IInteractable bestPromptQ = null;
        float bestPromptQDist = float.MaxValue;

        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it == null)
                continue;

            // Skip destroyed Unity objects behind the interface.
            if (it is Object uo && uo == null)
                continue;

            // Planar (XZ) distance — ignore height so tall NPCs / speech anchors
            // don't push the player outside a tight interaction radius.
            float dist = PlanarDistance(origin, it.InteractionPosition);
            float maxR = Mathf.Max(it.DiscoveryRadius, it.InteractionRadius) + scanPadding;
            if (dist > maxR)
                continue;

            bool inInteraction = dist <= it.InteractionRadius;

            // --- Input candidates ---
            if (inInteraction && it.IsAvailable)
            {
                if (it.WouldShowInteractionPrompt(dist))
                {
                    if (dist < bestBangDist ||
                        (Mathf.Approximately(dist, bestBangDist) && IsPreferable(it, bestBang)))
                    {
                        bestBang = it;
                        bestBangDist = dist;
                    }
                }
                else if (it.PromptMode == InteractPromptMode.Hidden)
                {
                    if (dist < bestHiddenDist ||
                        (Mathf.Approximately(dist, bestHiddenDist) && IsPreferable(it, bestHidden)))
                    {
                        bestHidden = it;
                        bestHiddenDist = dist;
                    }
                }
            }

            // --- Prompt candidates (never Hidden) ---
            if (it.PromptMode == InteractPromptMode.Hidden)
                continue;

            if (it.WouldShowInteractionPrompt(dist))
            {
                if (dist < bestPromptBangDist ||
                    (Mathf.Approximately(dist, bestPromptBangDist) && IsPreferable(it, bestPromptBang)))
                {
                    bestPromptBang = it;
                    bestPromptBangDist = dist;
                }
            }
            else if (it.WouldShowDiscoveryPrompt(dist))
            {
                if (dist < bestPromptQDist ||
                    (Mathf.Approximately(dist, bestPromptQDist) && IsPreferable(it, bestPromptQ)))
                {
                    bestPromptQ = it;
                    bestPromptQDist = dist;
                }
            }
        }

        // Input: prefer ! objects, else Hidden.
        inputFocus = bestBang != null ? bestBang : bestHidden;

        if (bestPromptBang != null)
        {
            promptFocus = bestPromptBang;
            promptState = InteractPromptState.Interaction;
        }
        else if (bestPromptQ != null)
        {
            promptFocus = bestPromptQ;
            promptState = InteractPromptState.Discovery;
        }
    }

    static float PlanarDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static bool IsPreferable(IInteractable candidate, IInteractable current)
    {
        if (current == null)
            return true;
        var ca = candidate as Object;
        var cu = current as Object;
        if (ca == null || cu == null)
            return false;
        return ca.GetInstanceID() < cu.GetInstanceID();
    }

    void ApplyPrompt(InteractPromptState state)
    {
        if (boundPrompt == null)
            return;
        boundPrompt.SetPromptState(state);
    }
}
