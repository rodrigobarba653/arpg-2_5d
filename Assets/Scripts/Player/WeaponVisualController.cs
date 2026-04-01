using UnityEngine;

public class WeaponVisualController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCombatController combatController;
    [SerializeField] private SpriteRenderer weaponRenderer;

    [Header("Sorting")]
    [SerializeField] private int sortingOrder = 11;

    [Header("Visibility")]
    [SerializeField] private bool showOnlyInCombat = true;

    private void Reset()
    {
        weaponRenderer = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (weaponRenderer == null)
            weaponRenderer = GetComponent<SpriteRenderer>();

        weaponRenderer.sortingOrder = sortingOrder;
    }

    private void LateUpdate()
    {
        if (weaponRenderer == null)
            return;

        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (!showOnlyInCombat)
        {
            weaponRenderer.enabled = true;
            return;
        }

        bool shouldShow = combatController != null && combatController.IsInCombat();
        weaponRenderer.enabled = shouldShow;
    }

    public void ShowWeapon()
    {
        if (weaponRenderer != null)
            weaponRenderer.enabled = true;
    }

    public void HideWeapon()
    {
        if (weaponRenderer != null)
            weaponRenderer.enabled = false;
    }
}