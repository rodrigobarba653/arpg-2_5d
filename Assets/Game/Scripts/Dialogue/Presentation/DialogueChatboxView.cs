using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Base screen-space chatbox. Style is placeholder; swap prefab later without logic changes.
/// </summary>
public class DialogueChatboxView : MonoBehaviour
{
    public RectTransform rectTransform;
    public CanvasGroup canvasGroup;
    public TMP_Text speakerLabel;
    public TMP_Text bodyLabel;
    public Image portraitImage;
    public GameObject portraitRoot;
    public Image bubbleBackground;

    public DialoguePresentationPriority Priority { get; private set; }

    public void ConfigurePriority(DialoguePresentationPriority priority)
    {
        Priority = priority;
    }

    public void SetSpeaker(string displayName)
    {
        if (speakerLabel == null)
            return;
        bool has = !string.IsNullOrEmpty(displayName);
        speakerLabel.gameObject.SetActive(has);
        if (has)
            speakerLabel.text = displayName;
    }

    public void SetPortrait(Sprite sprite)
    {
        bool show = sprite != null && portraitImage != null;
        if (portraitRoot != null)
            portraitRoot.SetActive(show);
        else if (portraitImage != null)
            portraitImage.gameObject.SetActive(show);

        if (show)
        {
            portraitImage.sprite = sprite;
            portraitImage.enabled = true;
        }
    }

    public void SetBodyVisibleText(string text)
    {
        if (bodyLabel != null)
            bodyLabel.text = text ?? "";
    }

    public void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }
        else
        {
            gameObject.SetActive(visible);
        }
    }

    /// <summary>Screen-space AABB after layout rebuild.</summary>
    public Rect GetScreenRect()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        Canvas.ForceUpdateCanvases();
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        // Overlay canvas: world == screen.
        float xmin = corners[0].x;
        float xmax = corners[0].x;
        float ymin = corners[0].y;
        float ymax = corners[0].y;
        for (int i = 1; i < 4; i++)
        {
            xmin = Mathf.Min(xmin, corners[i].x);
            xmax = Mathf.Max(xmax, corners[i].x);
            ymin = Mathf.Min(ymin, corners[i].y);
            ymax = Mathf.Max(ymax, corners[i].y);
        }

        return Rect.MinMaxRect(xmin, ymin, xmax, ymax);
    }
}
