using UnityEngine;

/// <summary>
/// Closed and open visuals for a chest. Interaction stays on
/// <see cref="WorldInteractable"/>. The claim flag is <see cref="persistentId"/>
/// in <see cref="WorldState"/>.
/// </summary>
public class ChestView : MonoBehaviour
{
    [Tooltip("WorldState flag written by ClaimChestAction. Empty never counts as claimed.")]
    public string persistentId = "";

    public GameObject closedVisual;
    public GameObject openVisual;

    public bool IsClaimed =>
        !string.IsNullOrEmpty(persistentId) && WorldState.GetFlag(persistentId);

    void OnEnable()
    {
        WorldState.OnRestored += ApplyState;
        ApplyState();
    }

    void OnDisable()
    {
        WorldState.OnRestored -= ApplyState;
    }

    public void ApplyState()
    {
        bool claimed = IsClaimed;
        if (closedVisual != null)
            closedVisual.SetActive(!claimed);
        if (openVisual != null)
            openVisual.SetActive(claimed);
    }
}
