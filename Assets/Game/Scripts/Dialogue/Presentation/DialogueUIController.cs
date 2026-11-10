using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the Overlay canvas and spawns placeholder chatbox views.
/// Prefabs can replace Build* later without changing DialogueService.
/// </summary>
public class DialogueUIController : MonoBehaviour
{
    public Canvas canvas;
    public RectTransform ambientRoot;
    public RectTransform interactiveRoot;

    [Header("Optional prefabs (null = runtime placeholder)")]
    public DialogueChatboxView ambientPrefab;
    public DialogueChatboxView interactivePrefab;
    public DialogueChoiceView choicePrefab;

    public static DialogueUIController CreateRuntime(Transform parent = null)
    {
        var go = new GameObject("DialogueUI");
        if (parent != null)
            go.transform.SetParent(parent, false);
        else
            DontDestroyOnLoad(go);

        var ui = go.AddComponent<DialogueUIController>();
        ui.BuildCanvas();
        return ui;
    }

    public void BuildCanvas()
    {
        if (canvas != null)
            return;

        var cgo = new GameObject("DialogueCanvas");
        cgo.transform.SetParent(transform, false);
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // above HUD-ish; notifications Sprint 07 can use 250+
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        cgo.AddComponent<GraphicRaycaster>();

        ambientRoot = CreateLayer("AmbientRoot", cgo.transform);
        interactiveRoot = CreateLayer("InteractiveRoot", cgo.transform);
    }

    static RectTransform CreateLayer(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    public DialogueChatboxView SpawnAmbient()
    {
        BuildCanvas();
        var view = Spawn(ambientPrefab, ambientRoot, "AmbientChatbox", small: true);
        view.ConfigurePriority(DialoguePresentationPriority.Ambient);
        return view;
    }

    public DialogueChatboxView SpawnInteractive()
    {
        BuildCanvas();
        var view = Spawn(interactivePrefab, interactiveRoot, "InteractiveChatbox", small: false);
        view.ConfigurePriority(DialoguePresentationPriority.Interactive);
        return view;
    }

    public DialogueChoiceView SpawnChoice()
    {
        BuildCanvas();
        if (choicePrefab != null)
        {
            var view = Instantiate(choicePrefab, interactiveRoot);
            view.name = "ChoiceMenu";
            view.Hide();
            return view;
        }

        return BuildChoicePlaceholder(interactiveRoot);
    }

    public void ReleaseChoice(DialogueChoiceView view)
    {
        if (view == null)
            return;
        Destroy(view.gameObject);
    }

    public void Release(DialogueChatboxView view)
    {
        if (view == null)
            return;
        Destroy(view.gameObject);
    }

    DialogueChatboxView Spawn(DialogueChatboxView prefab, RectTransform parent, string name, bool small)
    {
        DialogueChatboxView view;
        if (prefab != null)
        {
            view = Instantiate(prefab, parent);
            view.name = name;
            return view;
        }

        view = BuildPlaceholder(parent, name, small);
        return view;
    }

    DialogueChatboxView BuildPlaceholder(RectTransform parent, string name, bool small)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = small ? new Vector2(540f, 210f) : new Vector2(760f, 340f);

        var bg = go.AddComponent<Image>();
        bg.color = small
            ? new Color(0.08f, 0.08f, 0.12f, 0.88f)
            : new Color(0.05f, 0.05f, 0.1f, 0.92f);

        var cg = go.AddComponent<CanvasGroup>();

        // Portrait (interactive)
        GameObject portraitRoot = null;
        Image portraitImage = null;
        if (!small)
        {
            portraitRoot = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitRoot.transform.SetParent(go.transform, false);
            var prt = portraitRoot.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0f, 0.5f);
            prt.anchorMax = new Vector2(0f, 0.5f);
            prt.pivot = new Vector2(0f, 0.5f);
            prt.anchoredPosition = new Vector2(20f, 0f);
            prt.sizeDelta = new Vector2(280f, 280f);
            portraitImage = portraitRoot.GetComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitImage.color = Color.white;
            portraitRoot.SetActive(false);
        }

        // Header band for speaker name; body starts strictly below it (no overlap).
        float textLeft = small ? 20f : 320f;
        float headerHeight = small ? 48f : 56f;
        float headerPad = 8f;
        float bodyTop = headerHeight + headerPad;

        var speakerGo = CreateTmp("Speaker", go.transform, small ? 34f : 40f, FontStyles.Bold);
        speakerGo.enableWordWrapping = false;
        speakerGo.overflowMode = TextOverflowModes.Ellipsis;
        var speakerRt = speakerGo.rectTransform;
        speakerRt.anchorMin = new Vector2(0f, 1f);
        speakerRt.anchorMax = new Vector2(1f, 1f);
        speakerRt.pivot = new Vector2(0f, 1f);
        speakerRt.offsetMin = new Vector2(textLeft, -headerHeight);
        speakerRt.offsetMax = new Vector2(-28f, -4f);

        var bodyGo = CreateTmp("Body", go.transform, small ? 44f : 50f, FontStyles.Normal);
        var bodyRt = bodyGo.rectTransform;
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(textLeft, small ? 22f : 28f);
        bodyRt.offsetMax = new Vector2(-28f, -bodyTop);

        var view = go.AddComponent<DialogueChatboxView>();
        view.rectTransform = rt;
        view.canvasGroup = cg;
        view.speakerLabel = speakerGo;
        view.bodyLabel = bodyGo;
        view.portraitImage = portraitImage;
        view.portraitRoot = portraitRoot;
        view.bubbleBackground = bg;
        return view;
    }

    static TMP_Text CreateTmp(string name, Transform parent, float size, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        tmp.text = "";
        return tmp;
    }

    DialogueChoiceView BuildChoicePlaceholder(RectTransform parent)
    {
        var go = new GameObject("ChoiceMenu", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(720f, 280f);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.04f, 0.08f, 0.94f);
        bg.raycastTarget = false;

        var promptGo = new GameObject("Prompt", typeof(RectTransform));
        promptGo.transform.SetParent(go.transform, false);
        var prompt = promptGo.AddComponent<TextMeshProUGUI>();
        prompt.fontSize = 32f;
        prompt.fontStyle = FontStyles.Bold;
        prompt.color = new Color(0.85f, 0.9f, 1f, 1f);
        prompt.alignment = TextAlignmentOptions.MidlineLeft;
        prompt.enableWordWrapping = false;
        prompt.overflowMode = TextOverflowModes.Ellipsis;
        prompt.raycastTarget = false;
        var pr = prompt.rectTransform;
        pr.anchorMin = new Vector2(0f, 1f);
        pr.anchorMax = new Vector2(1f, 1f);
        pr.pivot = new Vector2(0.5f, 1f);
        pr.sizeDelta = new Vector2(0f, DialogueChoiceView.PromptHeight);
        pr.anchoredPosition = new Vector2(0f, -12f);
        pr.offsetMin = new Vector2(20f, pr.offsetMin.y);
        pr.offsetMax = new Vector2(-20f, pr.offsetMax.y);

        var view = go.AddComponent<DialogueChoiceView>();
        view.rectTransform = rt;
        view.promptLabel = prompt;
        view.Hide();
        return view;
    }
}
