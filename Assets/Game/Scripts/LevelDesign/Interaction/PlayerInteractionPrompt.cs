using UnityEngine;
using System.Collections;

public class PlayerInteractionPrompt : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;

    [Header("Animation State Names")]
    public string introStateName = "QuestionMark_Intro";
    public string idleStateName = "QuestionMark_Idle";
    public string outroStateName = "QuestionMark_Outro";

    [Header("Timing")]
    public float outroDuration = 0.25f;

    Coroutine hideRoutine;
    bool isShowing;

    void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        gameObject.SetActive(false);
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

        if (isShowing)
            return;

        isShowing = true;

        if (animator != null)
            animator.Play(introStateName, 0, 0f);
    }

    public void HidePrompt()
    {
        if (!isShowing)
            return;

        isShowing = false;

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        hideRoutine = StartCoroutine(HideAfterOutro());
    }

    IEnumerator HideAfterOutro()
    {
        if (animator != null)
            animator.Play(outroStateName, 0, 0f);

        yield return new WaitForSeconds(outroDuration);

        gameObject.SetActive(false);
        hideRoutine = null;
    }
}