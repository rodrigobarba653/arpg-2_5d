using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Black-overlay loading screen that auto-builds its own UI in code.
///
/// Usage from anywhere:
///   LoadingScreen.Ensure().BeginTransition();
///   // ...load a scene...
///   // It hides itself automatically once the scene has loaded and a short
///   // settle delay has passed.
///
/// While the loading screen is active:
///  - The screen is fully covered with a solid color (default: black).
///  - GamePause is set to true so player input + physics are frozen.
///  - A small label is shown in a chosen corner.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }

    [Header("Look")]
    public Color overlayColor = Color.black;
    public string loadingText = "Loading...";
    public int labelFontSize = 28;
    public Color labelColor = Color.white;

    [Tooltip("Corner of the screen for the loading label.")]
    public TextAnchor labelCorner = TextAnchor.LowerRight;

    [Tooltip("Pixels of padding from the corner.")]
    public float labelPadding = 40f;

    [Header("Timing")]
    [Tooltip("Extra time the loading screen stays up AFTER the scene is loaded, " +
             "so the player spawn / camera snap finishes before the player sees the world.")]
    public float postLoadHold = 0.6f;

    [Tooltip("If true, freeze Time.timeScale = 0 while loading (full pause).")]
    public bool freezeTimeWhileLoading = true;

    Canvas canvas;
    CanvasGroup group;
    Text label;
    Image overlay;
    bool active;
    bool waitingForSceneLoad;

    public static LoadingScreen Ensure()
    {
        if (Instance != null) return Instance;

        var go = new GameObject("LoadingScreen");
        return go.AddComponent<LoadingScreen>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        BuildUI();
        HideImmediate();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoadedDuringTransition;
    }

    void BuildUI()
    {
        // Canvas
        var cgo = new GameObject("Canvas");
        cgo.transform.SetParent(transform, false);
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        cgo.AddComponent<GraphicRaycaster>();
        group = cgo.AddComponent<CanvasGroup>();

        // Black overlay
        var ogo = new GameObject("Overlay");
        ogo.transform.SetParent(cgo.transform, false);
        overlay = ogo.AddComponent<Image>();
        overlay.color = overlayColor;
        StretchToParent(ogo.GetComponent<RectTransform>());

        // Label
        var lgo = new GameObject("LoadingLabel");
        lgo.transform.SetParent(cgo.transform, false);
        label = lgo.AddComponent<Text>();
        label.text = loadingText;
        label.color = labelColor;
        label.fontSize = labelFontSize;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;

        var lrt = lgo.GetComponent<RectTransform>();
        SetCornerAnchor(lrt, labelCorner, labelPadding);
    }

    static void StretchToParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetCornerAnchor(RectTransform rt, TextAnchor corner, float pad)
    {
        rt.sizeDelta = new Vector2(400f, 60f);

        Vector2 anchor;
        Vector2 pivot;
        Vector2 anchoredPos;

        switch (corner)
        {
            case TextAnchor.UpperLeft:
                anchor = new Vector2(0f, 1f); pivot = new Vector2(0f, 1f);
                anchoredPos = new Vector2(pad, -pad);
                break;
            case TextAnchor.UpperCenter:
                anchor = new Vector2(0.5f, 1f); pivot = new Vector2(0.5f, 1f);
                anchoredPos = new Vector2(0f, -pad);
                break;
            case TextAnchor.UpperRight:
                anchor = new Vector2(1f, 1f); pivot = new Vector2(1f, 1f);
                anchoredPos = new Vector2(-pad, -pad);
                break;
            case TextAnchor.LowerLeft:
                anchor = new Vector2(0f, 0f); pivot = new Vector2(0f, 0f);
                anchoredPos = new Vector2(pad, pad);
                break;
            case TextAnchor.LowerCenter:
                anchor = new Vector2(0.5f, 0f); pivot = new Vector2(0.5f, 0f);
                anchoredPos = new Vector2(0f, pad);
                break;
            case TextAnchor.LowerRight:
            default:
                anchor = new Vector2(1f, 0f); pivot = new Vector2(1f, 0f);
                anchoredPos = new Vector2(-pad, pad);
                break;
        }

        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
    }

    /// <summary>
    /// Show the loading screen, pause the game, and arm an auto-hide that fires
    /// after the next scene finishes loading + a short settle delay.
    /// Call this BEFORE SceneManager.LoadScene / LoadSceneAsync.
    /// </summary>
    public void BeginTransition()
    {
        Show();

        if (!waitingForSceneLoad)
        {
            waitingForSceneLoad = true;
            SceneManager.sceneLoaded += OnSceneLoadedDuringTransition;
        }
    }

    void OnSceneLoadedDuringTransition(Scene s, LoadSceneMode m)
    {
        SceneManager.sceneLoaded -= OnSceneLoadedDuringTransition;
        waitingForSceneLoad = false;
        StartCoroutine(HideAfterDelay(postLoadHold));
    }

    IEnumerator HideAfterDelay(float seconds)
    {
        // Use unscaled time so it works even with Time.timeScale = 0.
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Hide();
    }

    public void Show()
    {
        if (active) return;
        active = true;

        canvas.gameObject.SetActive(true);
        group.alpha = 1f;
        group.blocksRaycasts = true;
        group.interactable = false;

        GamePause.SetPaused(true, freezeTimeWhileLoading);
    }

    public void Hide()
    {
        if (!active) return;
        active = false;

        canvas.gameObject.SetActive(false);
        group.alpha = 0f;
        group.blocksRaycasts = false;

        GamePause.SetPaused(false, freezeTimeWhileLoading);
    }

    void HideImmediate()
    {
        active = false;
        if (canvas != null) canvas.gameObject.SetActive(false);
        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }
    }
}
