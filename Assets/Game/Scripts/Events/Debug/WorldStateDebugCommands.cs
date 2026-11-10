using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Keyboard helpers for Sprint 02 TestLab QA (no UI required).
/// F5 = save slot 0, F9 = load slot 0, F6 = clear WorldState (simulate New Game flags).
/// </summary>
public class WorldStateDebugCommands : MonoBehaviour
{
    public int slot = 0;

    void Update()
    {
        if (!Application.isPlaying)
            return;

        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.f5Key.wasPressedThisFrame)
        {
            if (SaveManager.Instance == null)
            {
                Debug.LogWarning("[WorldStateDebug] No SaveManager.Instance — is PlayTestBootstrap in the scene?");
                return;
            }

            SaveManager.Instance.SaveToSlot(slot);
            Debug.Log($"[WorldStateDebug] Saved slot {slot}. {WorldState.DebugSummary()}");
        }

        if (keyboard.f9Key.wasPressedThisFrame)
        {
            if (SaveManager.Instance == null)
            {
                Debug.LogWarning("[WorldStateDebug] No SaveManager.Instance.");
                return;
            }

            SaveManager.Instance.LoadFromSlot(slot);
            Debug.Log($"[WorldStateDebug] Load requested slot {slot}.");
        }

        if (keyboard.f6Key.wasPressedThisFrame)
        {
            WorldState.Clear();
            Debug.Log("[WorldStateDebug] WorldState.Clear() — flags/completed wiped (New Game style).");
        }

        // F7 — grant a Small Key for locked-door QA in TestLab.
        if (keyboard.f7Key.wasPressedThisFrame)
            GrantLabKey();
    }

    void GrantLabKey()
    {
        var db = ItemDatabase.Instance;
        if (db == null)
        {
            Debug.LogWarning("[WorldStateDebug] No ItemDatabase.");
            return;
        }

        var item = db.GetById("smallKey") as KeyItem;
        if (item == null)
        {
            Debug.LogWarning("[WorldStateDebug] Item id 'smallKey' not found.");
            return;
        }

        var active = Party.Active;
        if (active == null)
        {
            Debug.LogWarning("[WorldStateDebug] No active party member.");
            return;
        }

        var inv = active.GetComponent<PlayerInventory>();
        if (inv == null)
        {
            Debug.LogWarning("[WorldStateDebug] No PlayerInventory on active member.");
            return;
        }

        inv.Add(item, 1);
        Debug.Log("[WorldStateDebug] Granted 1x Small Key (F7).");
    }
}
