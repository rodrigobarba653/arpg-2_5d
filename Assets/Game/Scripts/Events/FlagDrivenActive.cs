using UnityEngine;

/// <summary>
/// Shows or hides a GameObject from a <see cref="WorldState"/> flag.
/// Put this on an always-active host (empty parent / manager) and assign
/// <see cref="target"/> to the soldier. If this component disables its own
/// GameObject it can no longer hear flag changes.
///
/// Applies in Awake (early) so a child that should start hidden never enables
/// NavMeshAgent / etc. before being turned off.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-500)]
public class FlagDrivenActive : MonoBehaviour
{
    [Tooltip("WorldState flag id. Missing flag reads as false.")]
    public string flagId = "";

    [Tooltip("If true, target is active when the flag is true. If false, active when flag is false.")]
    public bool activeWhenFlagTrue = true;

    [Tooltip("Object to show/hide. Prefer a child — keep this host active so it keeps listening.")]
    public GameObject target;

    [Tooltip("Log flag value and apply result to the Console.")]
    public bool debugLog = true;

    void Awake()
    {
        // Before children OnEnable (parent Awake runs first) — avoids NavMeshAgent
        // "no valid NavMesh" when the soldier starts active in the scene.
        Apply();
    }

    void OnEnable()
    {
        WorldState.OnRestored += Apply;
        WorldState.OnFlagChanged += OnFlagChanged;
        Apply();
    }

    void OnDisable()
    {
        WorldState.OnRestored -= Apply;
        WorldState.OnFlagChanged -= OnFlagChanged;
    }

    void OnFlagChanged(string id, bool value)
    {
        if (string.IsNullOrEmpty(flagId) || id != flagId)
            return;
        if (debugLog)
            Debug.Log($"[FlagDrivenActive:{name}] OnFlagChanged '{flagId}' = {value}", this);
        Apply();
    }

    public void Apply()
    {
        bool flag = WorldState.GetFlag(flagId);
        bool known = WorldState.HasFlag(flagId);
        bool shouldBeActive = activeWhenFlagTrue ? flag : !flag;

        if (debugLog)
        {
            Debug.Log(
                $"[FlagDrivenActive:{name}] Apply flagId='{flagId}' " +
                $"value={flag} hasFlag={known} activeWhenFlagTrue={activeWhenFlagTrue} " +
                $"→ shouldBeActive={shouldBeActive} target={(target != null ? target.name : "NULL")}",
                this);
        }

        if (target == null)
        {
            Debug.LogWarning(
                $"[FlagDrivenActive:{name}] Target is None — nothing to show/hide. " +
                "Assign the soldier (usually a child). Keep this host GameObject active.",
                this);
            return;
        }

        if (target == gameObject && !shouldBeActive)
        {
            Debug.LogWarning(
                $"[FlagDrivenActive:{name}] Refusing to deactivate self — " +
                "move this component to a parent and set Target to the soldier.",
                this);
            return;
        }

        if (target.activeSelf != shouldBeActive)
        {
            target.SetActive(shouldBeActive);
            if (debugLog)
                Debug.Log($"[FlagDrivenActive:{name}] SetActive '{target.name}' = {shouldBeActive}", this);
        }
        else if (debugLog)
        {
            Debug.Log($"[FlagDrivenActive:{name}] '{target.name}' already activeSelf={target.activeSelf}", this);
        }
    }
}
