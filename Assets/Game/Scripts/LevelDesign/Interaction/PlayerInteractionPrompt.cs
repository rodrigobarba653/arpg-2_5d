using UnityEngine;
using System.Collections;

/// <summary>
/// FFIX-style prompt owned by the player (Prompt under SpriteBody).
/// InteractionDetector only sets the display state; this component owns
/// position, billboard, animation, and visibility. Never reparented.
///
/// ? uses QuestionMark_* states. ! uses ExclamationMark_* states on the same
/// AnimatorController (sprite curves — do not fight them with code swaps).
/// </summary>
public class PlayerInteractionPrompt : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;
    public SpriteRenderer spriteRenderer;

    [Header("Discovery (?)")]
    public string discoveryIntroStateName = "QuestionMark_Intro";
    public string discoveryIdleStateName = "QuestionMark_Idle";
    public string discoveryOutroStateName = "QuestionMark_Outro";

    [Header("Interaction (!)")]
    public string interactionIntroStateName = "ExclamationMark_Intro";
    public string interactionIdleStateName = "ExclamationMark_Idle";
    public string interactionOutroStateName = "ExclamationMark_Outro";

    [Header("Legacy aliases (kept for existing prefab serialization)")]
    public string introStateName = "QuestionMark_Intro";
    public string idleStateName = "QuestionMark_Idle";
    public string outroStateName = "QuestionMark_Outro";

    [Header("Timing")]
    public float outroDuration = 0.25f;

    Coroutine hideRoutine;
    bool isShowing;
    InteractPromptState currentState = InteractPromptState.None;
    InteractPromptState visualState = InteractPromptState.None;

    void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        // Prefabs serialized only the old QuestionMark names — keep them as discovery defaults.
        if (!string.IsNullOrEmpty(introStateName))
            discoveryIntroStateName = introStateName;
        if (!string.IsNullOrEmpty(idleStateName))
            discoveryIdleStateName = idleStateName;
        if (!string.IsNullOrEmpty(outroStateName))
            discoveryOutroStateName = outroStateName;

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Drive prompt from InteractionDetector. Prompt stays on this transform
    /// (player head) — never moved to an interactable.
    /// </summary>
    public void SetPromptState(InteractPromptState state)
    {
        if (state == currentState)
            return;

        if (state == InteractPromptState.None)
        {
            currentState = InteractPromptState.None;
            HidePrompt();
            return;
        }

        bool wasHidden = currentState == InteractPromptState.None;
        currentState = state;
        visualState = state;

        if (wasHidden || !isShowing)
            ShowPrompt();
        else
            PlayIntro(state);
    }

    public void ShowPrompt()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        isShowing = true;
        PlayIntro(currentState != InteractPromptState.None ? currentState : InteractPromptState.Discovery);
    }

    public void HidePrompt()
    {
        if (!isShowing)
        {
            if (gameObject.activeSelf && currentState == InteractPromptState.None)
                gameObject.SetActive(false);
            return;
        }

        isShowing = false;

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        hideRoutine = StartCoroutine(HideAfterOutro());
    }

    void PlayIntro(InteractPromptState state)
    {
        if (animator == null)
            return;

        string intro = state == InteractPromptState.Interaction
            ? interactionIntroStateName
            : discoveryIntroStateName;

        animator.Play(intro, 0, 0f);
    }

    string OutroStateFor(InteractPromptState state)
    {
        return state == InteractPromptState.Interaction
            ? interactionOutroStateName
            : discoveryOutroStateName;
    }

    IEnumerator HideAfterOutro()
    {
        if (animator != null)
            animator.Play(OutroStateFor(visualState), 0, 0f);

        yield return new WaitForSeconds(outroDuration);

        if (currentState == InteractPromptState.None)
            gameObject.SetActive(false);

        hideRoutine = null;
    }
}
