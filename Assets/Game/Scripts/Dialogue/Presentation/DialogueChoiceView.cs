using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Choice menu overlay. Not a speaker bubble. A prefab can replace the placeholder
/// as long as it exposes the same Show / SetSelected / Hide surface.
/// </summary>
public class DialogueChoiceView : MonoBehaviour
{
    public const float RowHeight = 64f;
    public const float PromptHeight = 52f;
    public const float Pad = 16f;
    public const float LabelFontSize = 36f;

    public RectTransform rectTransform;
    public TMP_Text promptLabel;

    readonly List<Image> rowBackgrounds = new List<Image>();
    readonly List<TMP_Text> rowLabels = new List<TMP_Text>();
    RectTransform rowsRoot;
    RectTransform rowsContent;
    int selectedIndex = -1;
    float rowViewportHeight;

    public bool IsShown => gameObject.activeSelf && rowLabels.Count > 0;

    public int OptionCount => rowLabels.Count;

    public void Show(IList<string> labels, int selected, string prompt)
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        bool hasPrompt = !string.IsNullOrEmpty(prompt);
        if (promptLabel != null)
        {
            promptLabel.gameObject.SetActive(hasPrompt);
            promptLabel.text = hasPrompt ? prompt : "";
        }

        EnsureRowsRoot();
        ApplyRowInsets(hasPrompt);
        ClearRows();

        int count = labels != null ? labels.Count : 0;
        for (int i = 0; i < count; i++)
            CreateRow(i, labels[i] ?? "");

        gameObject.SetActive(true);
        SetSelected(selected);
    }

    public void SetSelected(int index)
    {
        if (rowBackgrounds.Count == 0)
        {
            selectedIndex = -1;
            return;
        }

        selectedIndex = Mathf.Clamp(index, 0, rowBackgrounds.Count - 1);
        for (int i = 0; i < rowBackgrounds.Count; i++)
        {
            bool on = i == selectedIndex;
            rowBackgrounds[i].color = on
                ? new Color(0.32f, 0.55f, 0.95f, 1f)
                : new Color(0.12f, 0.12f, 0.16f, 0.95f);
            if (rowLabels[i] != null)
                rowLabels[i].fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
        }

        KeepSelectedVisible();
    }

    public void Hide()
    {
        selectedIndex = -1;
        gameObject.SetActive(false);
    }

    public Rect GetScreenRect()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
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

    public Vector2 PreferredSize(float maxWidth, float maxHeight)
    {
        float width = maxWidth > 8f ? maxWidth : 720f;
        float cap = maxHeight > 8f ? maxHeight : 420f;
        float prompt = promptLabel != null && promptLabel.gameObject.activeSelf ? PromptHeight : 0f;
        float body = Mathf.Max(RowHeight, rowLabels.Count * RowHeight);
        float height = Mathf.Clamp(Pad * 2f + prompt + body, RowHeight + Pad * 2f, cap);
        rowViewportHeight = Mathf.Max(RowHeight, height - Pad * 2f - prompt);
        return new Vector2(width, height);
    }

    void EnsureRowsRoot()
    {
        if (rowsRoot != null)
            return;

        var go = new GameObject("Rows", typeof(RectTransform), typeof(RectMask2D));
        go.transform.SetParent(transform, false);
        rowsRoot = go.GetComponent<RectTransform>();
        rowsRoot.anchorMin = new Vector2(0f, 0f);
        rowsRoot.anchorMax = new Vector2(1f, 1f);
        rowsRoot.offsetMin = new Vector2(Pad, Pad);
        ApplyRowInsets(promptLabel != null && promptLabel.gameObject.activeSelf);

        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(rowsRoot, false);
        rowsContent = contentGo.GetComponent<RectTransform>();
        rowsContent.anchorMin = new Vector2(0f, 1f);
        rowsContent.anchorMax = new Vector2(1f, 1f);
        rowsContent.pivot = new Vector2(0.5f, 1f);
        rowsContent.offsetMin = Vector2.zero;
        rowsContent.offsetMax = Vector2.zero;
    }

    void ApplyRowInsets(bool hasPrompt)
    {
        if (rowsRoot == null)
            return;
        float top = hasPrompt ? Pad + PromptHeight : Pad;
        rowsRoot.offsetMax = new Vector2(-Pad, -top);
    }

    void CreateRow(int index, string label)
    {
        var go = new GameObject("Option_" + index, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(rowsContent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, RowHeight);
        rt.anchoredPosition = new Vector2(0f, -index * RowHeight);

        var bg = go.GetComponent<Image>();
        bg.raycastTarget = false;
        rowBackgrounds.Add(bg);

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = LabelFontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        tmp.text = label;
        var tr = tmp.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(16f, 4f);
        tr.offsetMax = new Vector2(-16f, -4f);
        rowLabels.Add(tmp);
    }

    void ClearRows()
    {
        for (int i = 0; i < rowBackgrounds.Count; i++)
        {
            if (rowBackgrounds[i] != null)
                Destroy(rowBackgrounds[i].gameObject);
        }

        rowBackgrounds.Clear();
        rowLabels.Clear();
    }

    void KeepSelectedVisible()
    {
        if (rowsContent == null || selectedIndex < 0)
            return;

        float content = rowLabels.Count * RowHeight;
        float viewport = rowViewportHeight > 1f ? rowViewportHeight : content;
        if (content <= viewport + 0.5f)
        {
            rowsContent.anchoredPosition = Vector2.zero;
            return;
        }

        float rowTop = selectedIndex * RowHeight;
        float rowBottom = rowTop + RowHeight;
        float scrolled = rowsContent.anchoredPosition.y;
        if (rowTop < scrolled)
            scrolled = rowTop;
        else if (rowBottom > scrolled + viewport)
            scrolled = rowBottom - viewport;

        float maxScroll = content - viewport;
        rowsContent.anchoredPosition = new Vector2(0f, Mathf.Clamp(scrolled, 0f, maxScroll));
    }
}
