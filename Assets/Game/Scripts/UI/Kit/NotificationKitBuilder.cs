using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds UI Kit v2 item notification banners (icon left, text right). No pointer.
/// </summary>
public static class NotificationKitBuilder
{
    public static RectTransform Build(
        RectTransform parent,
        string name,
        UiKitSprites kit,
        Vector2 size,
        string text,
        Sprite icon,
        TMP_FontAsset font = null)
    {
        if (kit == null)
            throw new System.ArgumentNullException(nameof(kit));

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;

        var bg = CreateSliced("Background", go.transform, kit.notificationBackground);
        StretchFull(bg.rectTransform);

        var frame = CreateSliced("Frame", go.transform, kit.notificationFrame);
        StretchFull(frame.rectTransform);

        // Icon cluster (left bulb of the notification frame).
        var iconRoot = new GameObject("IconRoot", typeof(RectTransform));
        iconRoot.transform.SetParent(go.transform, false);
        var iconRt = iconRoot.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        float iconSize = Mathf.Min(size.y * 0.72f, 72f);
        iconRt.sizeDelta = new Vector2(iconSize, iconSize);
        iconRt.anchoredPosition = new Vector2(size.y * 0.42f, 0f);

        if (kit.notificationIconFrame != null)
        {
            var iconFrame = CreateSimple("IconFrame", iconRoot.transform, kit.notificationIconFrame);
            StretchFull(iconFrame.rectTransform);
        }

        var iconImg = CreateSimple("Icon", iconRoot.transform, icon);
        var iRt = iconImg.rectTransform;
        iRt.anchorMin = iRt.anchorMax = new Vector2(0.5f, 0.5f);
        iRt.pivot = new Vector2(0.5f, 0.5f);
        iRt.sizeDelta = new Vector2(iconSize * 0.62f, iconSize * 0.62f);
        iconImg.preserveAspect = true;
        iconImg.enabled = icon != null;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        if (font != null)
            label.font = font;
        label.fontSize = Mathf.Clamp(size.y * 0.28f, 18f, 32f);
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        label.text = text ?? "";

        var lRt = label.rectTransform;
        lRt.anchorMin = new Vector2(0f, 0f);
        lRt.anchorMax = new Vector2(1f, 1f);
        float leftPad = size.y * 0.95f;
        lRt.offsetMin = new Vector2(leftPad, 8f);
        lRt.offsetMax = new Vector2(-28f, -8f);

        return rt;
    }

    static Image CreateSliced(string name, Transform parent, Sprite sprite)
    {
        var img = CreateSimple(name, parent, sprite);
        img.type = Image.Type.Sliced;
        return img;
    }

    static Image CreateSimple(string name, Transform parent, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        img.type = Image.Type.Simple;
        img.raycastTarget = false;
        return img;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
