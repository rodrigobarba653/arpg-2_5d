using UnityEngine;

public class CombatAnimEventsRelay : MonoBehaviour
{
    [Tooltip("Drag the object that has PlayerCombatController (usually Player root). If empty, we auto-find in parents.")]
    public PlayerCombatController combat;

    void Awake()
    {
        if (!combat)
            combat = GetComponentInParent<PlayerCombatController>();
    }

    // ---- Attack events ----
    public void TryAdvanceCombo() { if (combat) combat.TryAdvanceCombo(); }
    public void EndAttack()       { if (combat) combat.EndAttack(); }
    public void DisableHitbox()   { if (combat) combat.DisableHitbox(); }

    // Animation Event: Function = EnableHitboxInt, Int = 1/2/3
    public void EnableHitboxInt(int step) { if (combat) combat.EnableHitboxInt(step); }

    // ---- Roll events ----
    // Add this as an Animation Event at the LAST frame of the Roll clip (recommended).
    public void EndRoll()         { if (combat) combat.EndRoll(); }
}