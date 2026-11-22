using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-of-screen modal for chest / event rewards. Own dark translucent panel
/// (not the pause menu). Scale in → typewriter → wait Confirm (South / X) →
/// scale out. Freezes the world while open.
/// </summary>
[DisallowMultipleComponent]
public class RewardNoticePanel : MonoBehaviour
{
    public static RewardNoticePanel Instance { get; private set; }

    [Header("Timing")]
    [Min(0.05f)]
    public float scaleInSeconds = 0.22f;

    [Min(0.05f)]
    public float scaleOutSeconds = 0.18f;

    [Min(1f)]
    public float typewriterCharsPerSecond = 48f;

    [Header("Look")]
    public Color panelColor = new Color(0.08f, 0.08f, 0.1f, 0.82f);
    public Color textColor = Color.white;
    public Color hintColor = new Color(1f, 1f, 1f, 0.55f);
    public Vector2 panelPadding = new Vector2(28f, 18f);
    public float panelMaxWidth = 520f;
    public float topMargin = 36f;
    public int fontSize = 22;
    public int hintFontSize = 14;

    [Header("Optional prefab hooks (auto-built if empty)")]
    public RectTransform panelRoot;
    public Image panelBackground;
    public Image iconImage;
    public TMP_Text bodyLabel;
    public TMP_Text hintLabel;
    public CanvasGroup canvasGroup;

    struct Notice
    {
        public string text;
        public Sprite icon;
    }

    readonly Queue<Notice> queue = new Queue<Notice>();
    bool running;
    bool lockHeld;
    bool pauseHeld;
    bool wePausedTime;
    Coroutine routine;
    TMP_FontAsset fontAsset;

    public static bool IsBusy =>
        Instance != null && (Instance.running || Instance.queue.Count > 0);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null)
            return;
        var go = new GameObject(nameof(RewardNoticePanel));
        DontDestroyOnLoad(go);
        go.AddComponent<RewardNoticePanel>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent != null)
            transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
        EnsureUi();
        SetVisible(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        ReleaseWorldHold();
    }

    public static void Enqueue(string text, Sprite icon = null)
    {
        var inst = Instance != null ? Instance : EnsureInstance();
        if (inst == null || string.IsNullOrEmpty(text))
            return;
        inst.queue.Enqueue(new Notice { text = text, icon = icon });
        inst.Kick();
    }

    public static void EnqueueMany(IList<string> lines, Sprite icon = null)
    {
        if (lines == null || lines.Count == 0)
            return;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            if (string.IsNullOrEmpty(lines[i]))
                continue;
            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append(lines[i]);
        }

        if (sb.Length == 0)
            return;
        Enqueue(sb.ToString(), icon);
    }

    public static IEnumerator WaitUntilIdle()
    {
        var inst = Instance != null ? Instance : EnsureInstance();
        if (inst == null)
            yield break;
        while (inst.running || inst.queue.Count > 0)
            yield return null;
    }

    /// <summary>Drops the queue and closes the panel (event cancel).</summary>
    public static void CancelAll()
    {
        var inst = Instance;
        if (inst == null)
            return;
        inst.queue.Clear();
        if (inst.routine != null)
        {
            inst.StopCoroutine(inst.routine);
            inst.routine = null;
        }

        inst.running = false;
        inst.SetVisible(false);
        inst.ReleaseWorldHold();
    }

    static RewardNoticePanel EnsureInstance()
    {
        if (Instance != null)
            return Instance;
        Bootstrap();
        return Instance;
    }

    void Kick()
    {
        if (running)
            return;
        routine = StartCoroutine(RunQueue());
    }

    IEnumerator RunQueue()
    {
        running = true;
        while (queue.Count > 0)
        {
            var notice = queue.Dequeue();
            yield return PlayOne(notice);
        }

        running = false;
        routine = null;
        ReleaseWorldHold();
    }

    IEnumerator PlayOne(Notice notice)
    {
        EnsureUi();
        HoldWorld();

        if (bodyLabel != null)
        {
            bodyLabel.text = notice.text ?? "";
            bodyLabel.maxVisibleCharacters = 0;
            bodyLabel.ForceMeshUpdate();
        }

        if (iconImage != null)
        {
            bool has = notice.icon != null;
            iconImage.enabled = has;
            iconImage.gameObject.SetActive(has);
            if (has)
                iconImage.sprite = notice.icon;
        }

        if (hintLabel != null)
            hintLabel.gameObject.SetActive(false);

        SetVisible(true);
        if (panelRoot != null)
            panelRoot.localScale = Vector3.zero;

        DialogueInput.SuppressConfirmForCurrentPress();

        // Scale in (transform only — text stays undistorted at full size after).
        yield return ScalePanel(0f, 1f, scaleInSeconds);

        // Typewriter
        int total = bodyLabel != null ? bodyLabel.textInfo.characterCount : 0;
        float cps = Mathf.Max(1f, typewriterCharsPerSecond);
        float shown = 0f;
        while (shown < total)
        {
            if (DialogueInput.ConfirmPressedThisFrame())
            {
                shown = total;
                break;
            }

            shown += cps * Time.unscaledDeltaTime;
            if (bodyLabel != null)
                bodyLabel.maxVisibleCharacters = Mathf.Clamp(Mathf.FloorToInt(shown), 0, total);
            yield return null;
        }

        if (bodyLabel != null)
            bodyLabel.maxVisibleCharacters = total;

        if (hintLabel != null)
        {
            hintLabel.gameObject.SetActive(true);
            hintLabel.text = "Press × to continue";
        }

        DialogueInput.SuppressConfirmForCurrentPress();
        while (!DialogueInput.ConfirmPressedThisFrame())
            yield return null;

        if (hintLabel != null)
            hintLabel.gameObject.SetActive(false);

        yield return ScalePanel(1f, 0f, scaleOutSeconds);
        SetVisible(false);
    }

    IEnumerator ScalePanel(float from, float to, float seconds)
    {
        if (panelRoot == null)
            yield break;

        seconds = Mathf.Max(0.01f, seconds);
        float t = 0f;
        panelRoot.localScale = Vector3.one * from;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / seconds);
            // Smoothstep
            u = u * u * (3f - 2f * u);
            float s = Mathf.Lerp(from, to, u);
            panelRoot.localScale = new Vector3(s, s, s);
            yield return null;
        }

        panelRoot.localScale = Vector3.one * to;
    }

    void HoldWorld()
    {
        if (!lockHeld)
        {
            GameplayLock.Acquire(this, GameplayLockChannels.PlayerControl);
            lockHeld = true;
        }

        if (!pauseHeld)
        {
            if (!GamePause.IsPaused)
            {
                GamePause.SetPaused(true);
                wePausedTime = true;
            }

            pauseHeld = true;
        }
    }

    void ReleaseWorldHold()
    {
        if (lockHeld)
        {
            GameplayLock.Release(this);
            lockHeld = false;
        }

        if (pauseHeld)
        {
            if (wePausedTime)
            {
                GamePause.SetPaused(false);
                wePausedTime = false;
            }

            pauseHeld = false;
        }
    }

    void SetVisible(bool on)
    {
        if (panelRoot != null)
            panelRoot.gameObject.SetActive(on);
        if (canvasGroup != null)
            canvasGroup.alpha = on ? 1f : 0f;
    }

    void EnsureUi()
    {
        if (panelRoot != null && bodyLabel != null)
            return;

        fontAsset = ResolveFont();

        var canvasGo = new GameObject("RewardNoticeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = (int)DialoguePresentationPriority.Notification + 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelGo.transform.SetParent(canvasGo.transform, false);
        panelRoot = panelGo.GetComponent<RectTransform>();
        panelRoot.anchorMin = new Vector2(0.5f, 1f);
        panelRoot.anchorMax = new Vector2(0.5f, 1f);
        panelRoot.pivot = new Vector2(0.5f, 1f);
        panelRoot.anchoredPosition = new Vector2(0f, -topMargin);
        panelRoot.sizeDelta = new Vector2(panelMaxWidth, 0f);

        panelBackground = panelGo.GetComponent<Image>();
        panelBackground.color = panelColor;
        panelBackground.raycastTarget = false;

        canvasGroup = panelGo.GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        var vlg = panelGo.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(
            Mathf.RoundToInt(panelPadding.x),
            Mathf.RoundToInt(panelPadding.x),
            Mathf.RoundToInt(panelPadding.y),
            Mathf.RoundToInt(panelPadding.y));
        vlg.spacing = 10f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;

        var fitter = panelGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(panelGo.transform, false);
        var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 14f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlHeight = true;
        hlg.childControlWidth = true;
        hlg.childForceExpandHeight = false;
        hlg.childForceExpandWidth = false;

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconGo.transform.SetParent(rowGo.transform, false);
        iconImage = iconGo.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        var iconLe = iconGo.GetComponent<LayoutElement>();
        iconLe.preferredWidth = 40f;
        iconLe.preferredHeight = 40f;
        iconLe.minWidth = 40f;
        iconLe.minHeight = 40f;

        var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        bodyGo.transform.SetParent(rowGo.transform, false);
        bodyLabel = bodyGo.GetComponent<TextMeshProUGUI>();
        ConfigureTmp(bodyLabel, fontSize, textColor, TextAlignmentOptions.Left);
        bodyLabel.enableWordWrapping = true;
        var bodyLe = bodyGo.GetComponent<LayoutElement>();
        bodyLe.flexibleWidth = 1f;
        bodyLe.preferredWidth = panelMaxWidth - panelPadding.x * 2f - 60f;

        var hintGo = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
        hintGo.transform.SetParent(panelGo.transform, false);
        hintLabel = hintGo.GetComponent<TextMeshProUGUI>();
        ConfigureTmp(hintLabel, hintFontSize, hintColor, TextAlignmentOptions.Center);
        hintLabel.gameObject.SetActive(false);

        // Cap preferred width on panel via LayoutElement
        var panelLe = panelGo.AddComponent<LayoutElement>();
        panelLe.preferredWidth = panelMaxWidth;
    }

    void ConfigureTmp(TMP_Text tmp, int size, Color color, TextAlignmentOptions align)
    {
        if (tmp == null)
            return;
        if (fontAsset != null)
            tmp.font = fontAsset;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
    }

    static TMP_FontAsset ResolveFont()
    {
        // Prefer the project dogica SDF used by ItemPickupPopup / dialogue.
        var loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < loaded.Length; i++)
        {
            if (loaded[i] != null && loaded[i].name.IndexOf("dogica", StringComparison.OrdinalIgnoreCase) >= 0)
                return loaded[i];
        }

        return TMP_Settings.defaultFontAsset;
    }
}
