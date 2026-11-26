using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton that spawns ItemPickupPopups above the player.
/// Place this on a root GameObject in your title scene; it persists across
/// scene loads via DontDestroyOnLoad.
///
/// Anywhere in code that gives an item to the player can call:
///     ItemPickupNotifier.Show(item); // category icon in teal box
///
/// When UI Kit v2 is available, world pickups use a screen-space kit banner
/// (icon left / text right) instead of the legacy floating prefab chrome.
/// </summary>
public class ItemPickupNotifier : MonoBehaviour
{
    public static ItemPickupNotifier Instance { get; private set; }

    [Header("Prefab")]
    [Tooltip("Prefab with the ItemPickupPopup script on its root. " +
             "Used when UI Kit is not available.")]
    public ItemPickupPopup popupPrefab;

    [Header("Position")]
    [Tooltip("Offset from the player's position where popups spawn.")]
    public Vector3 spawnOffset = new Vector3(0f, 2.2f, 0f);

    [Tooltip("If true, popups parent to the player so they follow if the " +
             "player moves while the popup is alive.")]
    public bool parentToPlayer = false;

    readonly List<ItemPickupPopup> screenNotices = new List<ItemPickupPopup>();
    readonly List<KitScreenToast> kitToasts = new List<KitScreenToast>();
    Canvas kitCanvas;
    const float ScreenRowStep = NotificationKitBuilder.DefaultHeight + 8f;

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
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Show(string itemDisplayName, Sprite icon)
    {
        var inst = Instance;
        if (inst == null) return;

        string label = string.IsNullOrEmpty(itemDisplayName)
            ? "Item"
            : $"{itemDisplayName} obtenido.";

        Sprite resolved = icon != null ? icon : NotificationIconResolver.Resolve(null, null);
        if (UiKitRuntime.HasKit && inst.TryShowKitToast(label, resolved))
            return;

        inst.SpawnPopup(itemDisplayName, resolved);
    }

    public static void Show(ItemDefinition item, int quantity = 1)
    {
        if (item == null)
            return;

        string name = string.IsNullOrEmpty(item.displayName) ? item.name : item.displayName;
        if (quantity > 1)
            name = name + " x" + quantity;
        Show(name, NotificationIconResolver.ForItem(item));
    }

    public static void ShowVarium(int amount)
    {
        if (amount <= 0)
            return;
        Show("Varium x" + amount, NotificationIconResolver.ForVarium());
    }

    /// <summary>
    /// Event / chest rewards. Routes to <see cref="RewardNoticePanel"/> (modal).
    /// Kept for callers that still use this entry point.
    /// </summary>
    public static void ShowEventReward(string label, Sprite icon)
    {
        RewardNoticePanel.Enqueue(
            string.IsNullOrEmpty(label) ? "" : $"Obtained \"{label}\"",
            icon);
    }

    bool TryShowScreen(string label, Sprite icon)
    {
        if (popupPrefab == null)
            return false;

        var popup = Instantiate(popupPrefab);
        popup.Setup(label, icon);
        if (!popup.BeginScreenPresentation(screenNotices.Count, ScreenRowStep))
        {
            Destroy(popup.gameObject);
            return false;
        }

        popup.BindScreenHost(this);
        screenNotices.Add(popup);
        return true;
    }

    public void ReleaseScreenNotice(ItemPickupPopup popup)
    {
        screenNotices.Remove(popup);
        for (int i = 0; i < screenNotices.Count; i++)
        {
            if (screenNotices[i] != null)
                screenNotices[i].SetScreenStack(i, ScreenRowStep);
        }
    }

    public void ReleaseKitToast(KitScreenToast toast)
    {
        kitToasts.Remove(toast);
        for (int i = 0; i < kitToasts.Count; i++)
        {
            if (kitToasts[i] != null)
                kitToasts[i].SetStack(i, ScreenRowStep);
        }
    }

    bool TryShowKitToast(string label, Sprite icon)
    {
        var kit = UiKitRuntime.Get();
        if (kit == null || kit.notificationFrame == null)
            return false;

        EnsureKitCanvas();
        var banner = NotificationKitBuilder.Build(
            kitCanvas.transform as RectTransform,
            "KitToast",
            kit,
            new Vector2(NotificationKitBuilder.DefaultWidth, NotificationKitBuilder.DefaultHeight),
            label,
            icon,
            ResolveFont());

        var toast = banner.gameObject.AddComponent<KitScreenToast>();
        toast.Begin(kitToasts.Count, ScreenRowStep, this);
        kitToasts.Add(toast);
        return true;
    }

    void EnsureKitCanvas()
    {
        if (kitCanvas != null)
            return;

        var go = new GameObject("KitNoticeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(go);
        kitCanvas = go.GetComponent<Canvas>();
        kitCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        kitCanvas.sortingOrder = (int)DialoguePresentationPriority.Notification;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    static TMP_FontAsset ResolveFont()
    {
        var loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < loaded.Length; i++)
        {
            if (loaded[i] != null &&
                loaded[i].name.IndexOf("dogica", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return loaded[i];
        }

        return TMP_Settings.defaultFontAsset;
    }

    void SpawnPopup(string itemDisplayName, Sprite icon)
    {
        if (popupPrefab == null)
        {
            Debug.LogWarning("[ItemPickupNotifier] No popupPrefab assigned.", this);
            return;
        }

        Transform playerT = ResolvePlayerTransform();
        if (playerT == null) return;

        Vector3 pos = playerT.position + spawnOffset;
        var popup = Instantiate(popupPrefab, pos, Quaternion.identity);

        if (parentToPlayer)
            popup.transform.SetParent(playerT, worldPositionStays: true);

        popup.Setup(itemDisplayName, icon);
    }

    static Transform ResolvePlayerTransform()
    {
        if (PersistentPlayer.Instance != null)
            return PersistentPlayer.Instance.transform;

        var players = PlayerHealth.All;
        if (players.Count > 0 && players[0] != null)
            return players[0].transform;

        var go = GameObject.FindWithTag("Player");
        return go != null ? go.transform : null;
    }
}
