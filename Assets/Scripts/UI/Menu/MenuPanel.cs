using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Base class for any panel inside the main menu (Status, Items, Weapons, etc).
/// MenuManager activates/deactivates panels and calls Open / Close so they can
/// refresh their content.
/// </summary>
public abstract class MenuPanel : MonoBehaviour
{
    [Tooltip("Optional CanvasGroup for smooth show/hide. If null, the GameObject is just SetActive().")]
    [SerializeField] protected CanvasGroup canvasGroup;

    public virtual void Open()
    {
        gameObject.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        Refresh();
    }

    public virtual void Close()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
    }

    /// <summary>Pull latest data and refresh the visuals. Called by MenuManager
    /// on Open() and whenever GameState changes.</summary>
    public abstract void Refresh();

    /// <summary>The Selectable to focus when the panel is opened (so keyboard /
    /// gamepad navigation lands inside the panel instead of staying on the
    /// sidebar). Override in concrete panels.</summary>
    public virtual Selectable GetFirstSelectable() => null;
}
