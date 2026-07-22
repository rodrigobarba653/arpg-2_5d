using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Wires the title-screen buttons: New Game / Load Game / Settings / Exit Game.
/// Place on the main menu Canvas in the title scene and assign the buttons.
/// Load Game and Settings open their own panels (which you build alongside).
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    public Button newGameButton;
    public Button loadGameButton;
    public Button settingsButton;
    public Button exitGameButton;

    [Header("Panels (optional)")]
    [Tooltip("Panel shown when Load Game is clicked. Will be SetActive(true). Disable in scene by default.")]
    public GameObject loadGamePanel;

    [Tooltip("Panel shown when Settings is clicked.")]
    public GameObject settingsPanel;

    void Awake()
    {
        if (newGameButton  != null) newGameButton.onClick.AddListener(OnNewGameClicked);
        if (loadGameButton != null) loadGameButton.onClick.AddListener(OnLoadGameClicked);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
        if (exitGameButton != null) exitGameButton.onClick.AddListener(OnExitGameClicked);

        // Load Game is unavailable if there are no save slots.
        if (loadGameButton != null)
            loadGameButton.interactable = HasAnySave();

        if (loadGamePanel != null) loadGamePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    void Start()
    {
        DiagnoseUiInput();

        // Give the EventSystem an initial selection so keyboard / gamepad
        // navigation works without requiring a mouse click first.
        SelectFirstInteractable();
    }

    void DiagnoseUiInput()
    {
        var es = EventSystem.current;
        if (es == null)
        {
            Debug.LogError("[MainMenu] No EventSystem in scene. " +
                           "Create one: GameObject → UI → Event System.", this);
            return;
        }

        var inputModule = es.currentInputModule;
        if (inputModule == null)
        {
            Debug.LogWarning("[MainMenu] EventSystem has no current Input Module. " +
                             "Make sure it has either StandaloneInputModule (legacy) " +
                             "or InputSystemUIInputModule (new Input System).", es);
            return;
        }

#if ENABLE_INPUT_SYSTEM
        var isModule = inputModule as UnityEngine.InputSystem.UI.InputSystemUIInputModule;
        if (isModule != null)
        {
            if (isModule.actionsAsset == null)
            {
                Debug.LogError("[MainMenu] InputSystemUIInputModule has NO Actions Asset assigned. " +
                               "Click the EventSystem in the hierarchy and assign " +
                               "DefaultInputActions (package: Input System) to the Actions Asset field.", es);
            }
            else
            {
                Debug.Log($"[MainMenu] UI input module OK. Actions: {isModule.actionsAsset.name}", this);
            }
        }
        else
        {
            Debug.LogWarning("[MainMenu] EventSystem is NOT using InputSystemUIInputModule. " +
                             "If the project uses the new Input System, in the EventSystem inspector " +
                             "click 'Replace with InputSystemUIInputModule'.", es);
        }
#endif
    }

    void Update()
    {
        if (EventSystem.current == null) return;

        // A side panel (Load / Settings) owns the focus while it's open.
        if (IsAnyPanelOpen()) return;

        // If for any reason the selection got lost (mouse click on empty area,
        // returning from a closed panel, etc), re-select a main menu button.
        if (EventSystem.current.currentSelectedGameObject == null)
            SelectFirstInteractable();
    }

    void SelectFirstInteractable()
    {
        if (EventSystem.current == null) return;

        Button target = FirstInteractable();
        if (target == null)
        {
            Debug.LogWarning("[MainMenu] No interactable buttons found to focus.", this);
            return;
        }

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target.gameObject);

        Debug.Log($"[MainMenu] Focused button: {target.name}", target);
    }

    Button FirstInteractable()
    {
        // Preferred order: New Game → Load Game → Settings → Exit.
        if (newGameButton  != null && newGameButton.interactable  && newGameButton.gameObject.activeInHierarchy)  return newGameButton;
        if (loadGameButton != null && loadGameButton.interactable && loadGameButton.gameObject.activeInHierarchy) return loadGameButton;
        if (settingsButton != null && settingsButton.interactable && settingsButton.gameObject.activeInHierarchy) return settingsButton;
        if (exitGameButton != null && exitGameButton.interactable && exitGameButton.gameObject.activeInHierarchy) return exitGameButton;
        return null;
    }

    bool IsAnyPanelOpen()
    {
        return (loadGamePanel != null && loadGamePanel.activeInHierarchy)
            || (settingsPanel != null && settingsPanel.activeInHierarchy);
    }

    // ============================================================
    // Button handlers
    // ============================================================
    public void OnNewGameClicked()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogWarning("[MainMenu] No SaveManager in scene. Add one to bootstrap.");
            return;
        }

        SaveManager.Instance.StartNewGame();
    }

    public void OnLoadGameClicked()
    {
        if (loadGamePanel != null)
            loadGamePanel.SetActive(true);
        else
            Debug.Log("[MainMenu] Load Game clicked but no panel assigned yet.");
    }

    public void OnSettingsClicked()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
        else
            Debug.Log("[MainMenu] Settings clicked but no panel assigned yet.");
    }

    public void OnExitGameClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static bool HasAnySave()
    {
        for (int i = 0; i < SaveSystem.MaxSlots; i++)
            if (SaveSystem.HasSlot(i)) return true;
        return false;
    }
}
