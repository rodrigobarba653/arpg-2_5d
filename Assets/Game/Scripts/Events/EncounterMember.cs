using UnityEngine;

/// <summary>
/// Registers this enemy with an <see cref="EncounterKillGate"/> on enable.
/// Use for spawns / prefabs that are not in the gate's seed list.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public class EncounterMember : MonoBehaviour
{
    [Tooltip("Kill gate that should count this enemy's death.")]
    public EncounterKillGate gate;

    EnemyHealth health;

    void Awake()
    {
        health = GetComponent<EnemyHealth>();
    }

    void OnEnable()
    {
        if (gate == null || health == null)
            return;
        gate.Register(health);
    }

    void OnDisable()
    {
        // Despawn / pool without a kill — do not leave a ghost member that can never die.
        // If already dead, keep the kill counted on the gate.
        if (gate == null || health == null || health.IsDead)
            return;
        gate.Unregister(health);
    }
}
