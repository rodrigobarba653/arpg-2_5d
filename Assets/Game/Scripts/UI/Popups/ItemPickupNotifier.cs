using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that spawns ItemPickupPopups above the player.
/// Place this on a root GameObject in your title scene; it persists across
/// scene loads via DontDestroyOnLoad.
///
/// Anywhere in code that gives an item to the player can call:
///     ItemPickupNotifier.Show(item.displayName, item.icon);
/// </summary>
public class ItemPickupNotifier : MonoBehaviour
{
    public static ItemPickupNotifier Instance { get; private set; }

    [Header("Prefab")]
    [Tooltip("Prefab with the ItemPickupPopup script on its root. " +
             "Designed by the level designer / artist.")]
    public ItemPickupPopup popupPrefab;

    [Header("Position")]
    [Tooltip("Offset from the player's position where popups spawn.")]
    public Vector3 spawnOffset = new Vector3(0f, 2.2f, 0f);

    [Tooltip("If true, popups parent to the player so they follow if the " +
             "player moves while the popup is alive.")]
    public bool parentToPlayer = false;

    readonly List<ItemPickupPopup> screenNotices = new List<ItemPickupPopup>();
    const float ScreenRowStep = 52f;

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
        inst.SpawnPopup(itemDisplayName, icon);
    }

    /// <summary>
    /// Screen-space notice for event rewards. Stays on screen during a directed
    /// shot. Falls back to the world-space popup when the screen path cannot start.
    /// </summary>
    public static void ShowEventReward(string label, Sprite icon)
    {
        var inst = Instance;
        if (inst == null)
        {
            Debug.LogWarning("[ItemPickupNotifier] No instance; event reward notice skipped.");
            return;
        }

        if (!inst.TryShowScreen(label, icon))
            inst.SpawnPopup(label, icon);
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
