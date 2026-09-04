using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Owns the main pause menu: open/close, panel switching, sidebar navigation.
/// Place on the Canvas of the menu. Panels are MenuPanel children registered
/// in the inspector and aligned to sidebar buttons (one button per panel).
/// </summary>
public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance { get; private set; }

    [Header("Root")]
    [Tooltip("The root GameObject of the menu UI (Canvas or its first child). Activated on open.")]
    public GameObject menuRoot;

    [Header("Panels & Sidebar Buttons (matched 1:1, same order)")]
    public MenuPanel[] panels;
    public Button[] sidebarButtons;

    [Header("Save")]
    public Button saveButton;

    [Tooltip("Optional. If assigned, clicking Save opens this panel instead of " +
             "calling the placeholder log. The panel is responsible for its own " +
             "show/hide and modal flag.")]
    public SaveGamePanel saveGamePanel;

    [Header("Input")]
    [Tooltip("Optional InputAction to toggle the menu. If empty, defaults to Esc / Start.")]
    public InputActionReference toggleAction;

    [Header("Behavior")]
    [Tooltip("If true, sets Time.timeScale = 0 while the menu is open.")]
    public bool pauseGameWhileOpen = true;

    [Tooltip("Index of the panel to show when the menu opens.")]
    public int defaultPanelIndex = 0;

    [Header("Persistence")]
    [Tooltip("Keep this MenuManager alive across scene loads. Place it on the " +
             "title scene so the same Canvas / panels live everywhere.")]
    public bool persistAcrossScenes = true;

    [Tooltip("Scene names where the pause menu is disabled (title screen, " +
             "cinematics, etc). The toggle key is ignored and any open menu " +
             "closes when transitioning to one of these scenes.")]
    public List<string> blockedScenes = new List<string>();

    [Header("Active Panel Indicator (optional)")]
    [Tooltip("One GameObject per sidebar button. The one matching CurrentPanelIndex " +
             "is set active; the rest are hidden. Use a small highlight Image, border, " +
             "underline bar, etc — whatever you want as the 'active panel' indicator.")]
    public GameObject[] sidebarHighlights;

    [Header("Audio (optional)")]
    public AudioClip openSound;
    public AudioClip closeSound;
    public AudioClip selectSound;
    public AudioClip cancelSound;

    [Tooltip("Played whenever the focused button changes (hover / D-pad nav). " +
             "Suppressed for a brief moment after select/cancel so the sounds " +
             "don't stack on panel switches.")]
    public AudioClip navigateSound;

    public static void PlayCancelSound()
    {
        if (Instance == null) return;
        if (Instance.cancelSound != null)
            AudioManager.Play2DOrFallback(Instance.cancelSound);
        Instance.suppressNavUntil = Time.unscaledTime + 0.1f;
    }

    public static void PlaySelectSound()
    {
        if (Instance == null) return;
        if (Instance.selectSound != null)
            AudioManager.Play2DOrFallback(Instance.selectSound);
        Instance.suppressNavUntil = Time.unscaledTime + 0.1f;
    }

    public static void PlayNavigateSound()
    {
        if (Instance == null) return;
        if (Instance.navigateSound != null)
            AudioManager.Play2DOrFallback(Instance.navigateSound);
    }

    GameObject lastSelectedForNav;
    float suppressNavUntil;

    public bool IsOpen { get; private set; }
    public int CurrentPanelIndex { get; private set; } = -1;
    public bool IsBlockedInCurrentScene { get; private set; }

    /// <summary>
    /// True while any modal (target selector, dialog, etc) is open inside the menu.
    /// MenuManager skips toggle / back input while this is true, so the modal can
    /// handle Esc / Circle on its own.
    /// </summary>
    public static bool IsModalOpen;

    public event Action OnOpened;
    public event Action OnClosed;
    public event Action<int> OnPanelChanged;

    InputAction defaultToggle;
    InputAction backAction;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);

        WireSidebarButtons();

        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveClicked);

        if (menuRoot != null)
        {
            if (menuRoot == gameObject)
            {
                Debug.LogError("[MenuManager] 'Menu Root' is the same GameObject as the " +
                               "MenuManager itself. Move MenuManager to a parent (the Canvas) " +
                               "and assign the menu container as Menu Root.", this);
            }
            else
            {
                menuRoot.SetActive(false);
            }
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        UpdateBlockedState(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (defaultToggle != null) defaultToggle.Disable();
        if (Instance == this) Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateBlockedState(scene.name);
    }

    void UpdateBlockedState(string sceneName)
    {
        IsBlockedInCurrentScene = blockedScenes != null && blockedScenes.Contains(sceneName);

        if (IsBlockedInCurrentScene && IsOpen)
            Close();
    }

    void OnEnable()
    {
        if (toggleAction != null && toggleAction.action != null)
            toggleAction.action.Enable();
        else
        {
            defaultToggle = new InputAction("MenuToggle", InputActionType.Button);
            defaultToggle.AddBinding("<Keyboard>/escape");
            defaultToggle.AddBinding("<Keyboard>/tab");
            defaultToggle.AddBinding("<Gamepad>/start");
            defaultToggle.Enable();
        }

        // Separate "back" action — only does contextual back when the menu is
        // open. Never opens the menu (Circle outside the menu would conflict
        // with gameplay Cancel input).
        if (backAction == null)
        {
            backAction = new InputAction("MenuBack", InputActionType.Button);
            backAction.AddBinding("<Gamepad>/buttonEast");
            backAction.Enable();
        }
    }

    void OnDisable()
    {
        defaultToggle?.Disable();
        backAction?.Disable();
    }

    void Update()
    {
        UpdateNavSound();

        // Self-heal: several nested panels/modals (WeaponSlotsPanel,
        // PartSelectorPanel, MagicSpellSelectorPanel, SettingsPanel's sub-tabs)
        // restore focus on close via a guard like
        // "if (returnFocus != null && returnFocus.gameObject.activeInHierarchy)".
        // If that target ever became inactive/destroyed in the meantime (e.g. a
        // dynamically-spawned list row that got rebuilt), the guard silently
        // fails: EventSystem.current.SetSelectedGameObject(null) was already
        // called and NEVER gets a follow-up reselect. With no active selection,
        // keyboard/gamepad Move has nothing to navigate FROM, so the whole menu
        // looks "stuck" (mouse clicks still work, stick/dpad/arrows do nothing)
        // until the player clicks something to reseed the selection.
        //
        // Recover automatically: whenever the menu is open, no modal is mid-
        // transition (IsModalOpen is false — that flag stays true through the
        // exact frame a legitimate restore attempt happens, so this never
        // fights a normal close), and the EventSystem selection is null, pick
        // something sane to reselect.
        if (IsOpen && !IsModalOpen && EventSystem.current != null
            && EventSystem.current.currentSelectedGameObject == null)
        {
            RecoverLostFocus();
        }

        bool togglePressed =
            (toggleAction != null && toggleAction.action != null && toggleAction.action.WasPressedThisFrame())
            || (defaultToggle != null && defaultToggle.WasPressedThisFrame());

        bool backPressed = backAction != null && backAction.WasPressedThisFrame();

        if (!togglePressed && !backPressed) return;

        // A modal popup (target selector, dialog, etc) is open — let it handle input.
        if (IsModalOpen) return;

        // Blocked in this scene (title, cinematic, etc).
        if (IsBlockedInCurrentScene) return;

        // Toggle key (Esc/Tab/Start): opens the menu when closed; otherwise
        // does the contextual back below.
        if (togglePressed && !IsOpen)
        {
            Open();
            return;
        }

        // Back key (Circle/B): never opens the menu — only acts when open.
        if (backPressed && !IsOpen) return;

        // Already open → contextual back. Two ways to detect "we're in a panel":
        //   1. Focus is inside the active panel's hierarchy.
        //   2. The displayed panel is not the default one — the user picked it
        //      deliberately, so going back makes sense even if Unity's UI
        //      navigation kept focus on the sidebar button.
        bool inPanelByFocus = IsFocusInsidePanel();
        bool inPanelByDisplay = CurrentPanelIndex >= 0 && CurrentPanelIndex != defaultPanelIndex;

        if (inPanelByFocus || inPanelByDisplay)
        {
            int sourceButtonIndex = CurrentPanelIndex;

            // Switch the visible panel to the default one (e.g. Status) but
            // don't move focus into it — focus the sidebar button instead.
            ShowPanelSilently(defaultPanelIndex);
            FocusSidebarButton(sourceButtonIndex);

            PlayCancelSound();
        }
        else
        {
            Close();
        }
    }

    /// <summary>
    /// Like ShowPanel but doesn't move focus, doesn't play the select sound,
    /// and doesn't fire OnPanelChanged. Used by the contextual back so the
    /// right-side panel reverts to the default while keeping focus on the
    /// sidebar button the user came from.
    /// </summary>
    void ShowPanelSilently(int index)
    {
        if (panels == null || index < 0 || index >= panels.Length) return;

        for (int i = 0; i < panels.Length; i++)
            if (panels[i] != null && i != index) panels[i].Close();

        if (panels[index] != null) panels[index].Open();

        CurrentPanelIndex = index;
        ApplySidebarHighlight();
    }

    void FocusSidebarButton(int index)
    {
        if (EventSystem.current == null) return;
        if (sidebarButtons == null || index < 0 || index >= sidebarButtons.Length) return;

        var btn = sidebarButtons[index];
        if (btn == null) return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(btn.gameObject);
    }

    /// <summary>Called from Update() when the menu is open but the EventSystem
    /// has no current selection (see the self-heal comment in Update()).
    /// Prefers the active panel's own first selectable; falls back to that
    /// panel's sidebar button if the panel has nothing selectable of its own.</summary>
    void RecoverLostFocus()
    {
        if (EventSystem.current == null) return;

        Selectable target = null;

        if (CurrentPanelIndex >= 0 && panels != null && CurrentPanelIndex < panels.Length
            && panels[CurrentPanelIndex] != null)
        {
            target = panels[CurrentPanelIndex].GetFirstSelectable();
        }

        if (target == null && sidebarButtons != null
            && CurrentPanelIndex >= 0 && CurrentPanelIndex < sidebarButtons.Length)
        {
            target = sidebarButtons[CurrentPanelIndex];
        }

        if (target != null)
            EventSystem.current.SetSelectedGameObject(target.gameObject);
    }

    bool IsFocusInsidePanel()
    {
        if (EventSystem.current == null) return false;
        if (CurrentPanelIndex < 0 || CurrentPanelIndex >= panels.Length) return false;

        var panel = panels[CurrentPanelIndex];
        if (panel == null) return false;

        var sel = EventSystem.current.currentSelectedGameObject;
        if (sel == null) return false;

        return sel.transform.IsChildOf(panel.transform);
    }

    void FocusActiveSidebarButton()
    {
        if (EventSystem.current == null) return;
        if (sidebarButtons == null) return;
        if (CurrentPanelIndex < 0 || CurrentPanelIndex >= sidebarButtons.Length) return;

        var btn = sidebarButtons[CurrentPanelIndex];
        if (btn == null) return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(btn.gameObject);
    }

    void WireSidebarButtons()
    {
        if (sidebarButtons == null) return;

        for (int i = 0; i < sidebarButtons.Length; i++)
        {
            int idx = i; // capture for closure
            if (sidebarButtons[i] != null)
                sidebarButtons[i].onClick.AddListener(() => ShowPanel(idx));
        }
    }

    // ============================================================
    // OPEN / CLOSE
    // ============================================================
    public void Open()
    {
        if (IsOpen) return;
        if (IsBlockedInCurrentScene) return;

        EnsureEventSystem();

        IsOpen = true;

        if (menuRoot != null) menuRoot.SetActive(true);

        if (pauseGameWhileOpen)
            GamePause.SetPaused(true);

        AudioManager.Play2DOrFallback(openSound);

        ShowPanel(defaultPanelIndex);
        SelectFirstSidebarButton();
        OnOpened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;

        for (int i = 0; i < panels.Length; i++)
            if (panels[i] != null) panels[i].Close();

        if (menuRoot != null) menuRoot.SetActive(false);

        if (pauseGameWhileOpen)
            GamePause.SetPaused(false);

        AudioManager.Play2DOrFallback(closeSound);

        // Clear EventSystem selection so the game doesn't keep focus on a hidden button.
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        // Defensive: any modal that was open is implicitly closed now too.
        IsModalOpen = false;

        CurrentPanelIndex = -1;
        OnClosed?.Invoke();
    }

    void SelectFirstSidebarButton()
    {
        if (EventSystem.current == null)
        {
            Debug.LogWarning("[MenuManager] EventSystem.current is null — UI navigation will not work.", this);
            return;
        }
        if (sidebarButtons == null || sidebarButtons.Length == 0) return;

        int idx = Mathf.Clamp(defaultPanelIndex, 0, sidebarButtons.Length - 1);
        var btn = sidebarButtons[idx];

        if (btn == null || !btn.gameObject.activeInHierarchy) return;

        // Clear and reselect so it actually highlights even if the same button
        // was focused before.
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(btn.gameObject);
    }

    /// <summary>
    /// Ensure there's an EventSystem in the scene. When MenuManager persists
    /// across scenes, the title scene's EventSystem may have been destroyed; if
    /// the new scene didn't have its own, UI navigation stops working until we
    /// create a fresh one.
    /// </summary>
    void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        // Try to find an existing one (may be inactive due to scene transition).
        var existing = FindObjectOfType<EventSystem>();
        if (existing != null)
        {
            if (!existing.gameObject.activeSelf)
                existing.gameObject.SetActive(true);

            Debug.Log("[MenuManager] Re-activated existing EventSystem.", existing);
            return;
        }

        // Otherwise build one from scratch
        var go = new GameObject("EventSystem (auto-created)");
        go.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
        var inputModule = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        // Try to load a UI actions asset from Resources/UIInputActions.asset so
        // the InputSystemUIInputModule has proper bindings without manual setup.
        var actions = Resources.Load<UnityEngine.InputSystem.InputActionAsset>("UIInputActions");
        if (actions != null)
        {
            inputModule.actionsAsset = actions;
        }
        else
        {
            Debug.LogWarning("[MenuManager] Auto-created EventSystem but no Resources/UIInputActions " +
                             "asset was found. UI navigation may not work until you assign an " +
                             "Actions Asset to the InputSystemUIInputModule on the EventSystem.", go);
        }
#else
        go.AddComponent<StandaloneInputModule>();
#endif

        if (persistAcrossScenes)
            DontDestroyOnLoad(go);

        Debug.Log("[MenuManager] Auto-created an EventSystem because the scene had none.", go);
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    /// <summary>
    /// Watches EventSystem.currentSelectedGameObject for changes and fires the
    /// navigateSound when focus moves between buttons. Suppressed briefly
    /// after select / cancel so the sounds don't stack on panel switches.
    /// </summary>
    void UpdateNavSound()
    {
        if (!IsOpen)
        {
            lastSelectedForNav = null;
            return;
        }

        var es = EventSystem.current;
        if (es == null) return;

        var sel = es.currentSelectedGameObject;
        if (sel == lastSelectedForNav) return;

        // Only fire when we previously had a valid selection AND now we have
        // one too (skip the very first focus when the menu opens, and ignore
        // transient null states).
        if (lastSelectedForNav != null && sel != null
            && Time.unscaledTime >= suppressNavUntil)
        {
            PlayNavigateSound();
        }

        lastSelectedForNav = sel;
    }

    // ============================================================
    // PANEL NAVIGATION
    // ============================================================
    public void ShowPanel(int index)
    {
        if (panels == null || index < 0 || index >= panels.Length) return;

        // Close all
        for (int i = 0; i < panels.Length; i++)
            if (panels[i] != null && i != index) panels[i].Close();

        // Open target
        if (panels[index] != null) panels[index].Open();

        CurrentPanelIndex = index;
        ApplySidebarHighlight();

        // Use the helper so the navigateSound is suppressed for a moment —
        // otherwise the focus change inside the new panel would stack on top.
        PlaySelectSound();
        OnPanelChanged?.Invoke(index);

        // Move EventSystem focus into the panel so keyboard/gamepad nav lives
        // inside it. The user comes back to the sidebar via the Cancel/Esc key.
        // Deferred one frame: Open() (and EquipmentPanel/MagicPanel's Refresh())
        // SetActive(true) the target button in this same frame, and selecting it
        // immediately silently fails — same trap the modal panels already work
        // around with FocusFirstNextFrame(). Without the delay, the first Submit
        // press opens the panel but doesn't land a selection, so it takes a
        // second press to actually navigate inside it.
        if (focusPanelRoutine != null) StopCoroutine(focusPanelRoutine);
        focusPanelRoutine = StartCoroutine(FocusFirstSelectableOfPanelNextFrame(index));
    }

    Coroutine focusPanelRoutine;

    IEnumerator FocusFirstSelectableOfPanelNextFrame(int index)
    {
        yield return null;
        focusPanelRoutine = null;
        FocusFirstSelectableOfPanel(index);
    }

    void FocusFirstSelectableOfPanel(int index)
    {
        if (EventSystem.current == null) return;
        if (panels == null || index < 0 || index >= panels.Length) return;

        var panel = panels[index];
        if (panel == null) return;

        var first = panel.GetFirstSelectable();
        if (first == null) return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    void ApplySidebarHighlight()
    {
        if (sidebarHighlights == null) return;

        for (int i = 0; i < sidebarHighlights.Length; i++)
        {
            if (sidebarHighlights[i] == null) continue;
            sidebarHighlights[i].SetActive(i == CurrentPanelIndex);
        }
    }

    void OnSaveClicked()
    {
        if (saveGamePanel != null)
        {
            saveGamePanel.gameObject.SetActive(true);
            return;
        }

        Debug.Log("[MenuManager] Save clicked but no SaveGamePanel assigned.", this);
    }
}
