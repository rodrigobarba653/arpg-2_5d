using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Base screen-space chatbox. Style is placeholder; swap prefab later without logic changes.
/// When no portrait is shown, text insets collapse so the body uses the full bubble width
/// (same idea as <c>display:none</c> on the portrait column).
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

    [Tooltip("Left inset for speaker/body when a portrait is visible.")]
    public float textLeftWithPortrait = 320f;

    [Tooltip("Left inset for speaker/body when portrait is hidden / null.")]
    public float textLeftWithoutPortrait = 28f;

    public DialoguePresentationPriority Priority { get; private set; }

    string bodyFitText;
    bool bodyFitReady;
    bool portraitVisible;

    bool hasPinnedScreenPosition;
    Vector2 pinnedScreenPosition;
    Vector2 pinnedPivot;

    public bool HasPinnedScreenPosition => hasPinnedScreenPosition;

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
            portraitImage.preserveAspect = true;
            portraitImage.color = Color.white;
        }

        portraitVisible = show;
        ApplyTextColumnInsets(show);
    }

    public void SetBodyVisibleText(string text)
    {
        if (bodyLabel != null)
            bodyLabel.text = text ?? "";
    }

    /// <summary>
    /// Remember the full line and grow the bubble so that text fits the current width.
    /// Typewriter still reveals a substring; the box is sized for the whole line.
    /// </summary>
    public void PrepareBodyFit(string fullText)
    {
        bodyFitText = fullText ?? "";
        bodyFitReady = true;
        FitHeightToBody();
    }

    public void FitHeightToBody()
    {
        if (!bodyFitReady || rectTransform == null || bodyLabel == null)
            return;

        float width = rectTransform.sizeDelta.x;
        if (width < 8f)
            return;

        float left = bodyLabel.rectTransform.offsetMin.x;
        float right = Mathf.Max(0f, -bodyLabel.rectTransform.offsetMax.x);
        float bottom = Mathf.Max(0f, bodyLabel.rectTransform.offsetMin.y);
        float top = Mathf.Max(0f, -bodyLabel.rectTransform.offsetMax.y);
        float column = Mathf.Max(48f, width - left - right);

        Vector2 preferred = bodyLabel.GetPreferredValues(bodyFitText, column, 0f);
        if (preferred.y < 1f)
            return;

        float slack = Mathf.Max(18f, bodyLabel.fontSize * 0.5f);
        float height = top + Mathf.Max(preferred.y, bodyLabel.fontSize) + bottom + slack;

        if (portraitVisible && portraitRoot != null && portraitRoot.activeSelf)
        {
            var portraitRect = portraitRoot.transform as RectTransform;
            if (portraitRect != null)
                height = Mathf.Max(height, portraitRect.sizeDelta.y + 48f);
        }

        const float maxHeight = 780f;
        bool capped = height > maxHeight;
        float minHeight = top + bodyLabel.fontSize + bottom + slack;
        height = Mathf.Clamp(height, minHeight, maxHeight);
        bodyLabel.overflowMode = capped ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
        rectTransform.sizeDelta = new Vector2(width, height);
    }

    public void SetVisible(bool visible)
    {
        if (!visible)
            ClearPinnedScreenPosition();

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

    public void ClearPinnedScreenPosition()
    {
        hasPinnedScreenPosition = false;
    }

    public void PinCurrentScreenPosition()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;
        if (rectTransform == null)
            return;

        pinnedScreenPosition = rectTransform.position;
        pinnedPivot = rectTransform.pivot;
        hasPinnedScreenPosition = true;
    }

    public void ApplyPinnedScreenPosition()
    {
        if (!hasPinnedScreenPosition)
            return;
        if (rectTransform == null)
            rectTransform = transform as RectTransform;
        if (rectTransform == null)
            return;

        rectTransform.pivot = pinnedPivot;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.position = pinnedScreenPosition;
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

    /// <summary>
    /// Collapses or restores the left text column so hidden portraits do not leave empty space.
    /// </summary>
    public void ApplyTextColumnInsets(bool showPortrait)
    {
        float left = showPortrait ? textLeftWithPortrait : textLeftWithoutPortrait;
        SetLeftInset(speakerLabel != null ? speakerLabel.rectTransform : null, left);
        SetLeftInset(bodyLabel != null ? bodyLabel.rectTransform : null, left);

        if (bodyFitReady)
            FitHeightToBody();
    }

    static void SetLeftInset(RectTransform rt, float left)
    {
        if (rt == null)
            return;

        Vector2 min = rt.offsetMin;
        if (Mathf.Abs(min.x - left) < 0.01f)
            return;
        min.x = left;
        rt.offsetMin = min;
    }
}
