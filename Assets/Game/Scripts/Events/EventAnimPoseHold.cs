using UnityEngine;

/// <summary>
/// Keeps a forced Animator state while an EventRunner owns the pose.
/// Re-applies every LateUpdate so controller transitions (e.g. InCombat→Idle)
/// cannot yank the sprite back.
/// </summary>
[DisallowMultipleComponent]
public sealed class EventAnimPoseHold : MonoBehaviour
{
    public Animator animator;
    public int layer;
    public string stateName;
    public bool setInCombat;
    public int inCombatHash;

    void LateUpdate()
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        if (setInCombat && inCombatHash != 0)
            animator.SetBool(inCombatHash, true);

        animator.SetBool("IsMoving", false);

        var info = animator.GetCurrentAnimatorStateInfo(layer);
        if (!info.IsName(stateName))
            animator.Play(stateName, layer, 0f);
    }

    public void ApplyNow()
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        if (setInCombat && inCombatHash != 0)
            animator.SetBool(inCombatHash, true);

        animator.SetBool("IsMoving", false);
        animator.Play(stateName, layer, 0f);
        animator.Update(0f);
    }
}
