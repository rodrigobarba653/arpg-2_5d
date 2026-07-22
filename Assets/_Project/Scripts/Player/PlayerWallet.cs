using System;
using UnityEngine;

/// <summary>
/// Holds the party's shared Varium currency.
///
/// SHARED ACROSS THE PARTY — `varium` lives in a single static field. Each
/// character can carry a PlayerWallet component; all of them read / write the
/// same underlying amount. Events fire globally so any UI subscription updates
/// regardless of which character is active.
///
/// Use PlayerWallet.Total (static) from anywhere, or grab any instance and
/// call AddVarium / SpendVarium — they all hit the same wallet.
/// </summary>
public class PlayerWallet : MonoBehaviour
{
    static int sVarium;
    static event Action<int> sOnVariumChanged;

    /// <summary>Total varium shared across the party.</summary>
    public static int Total => sVarium;

    /// <summary>Subscribe via any instance: inv.OnVariumChanged += handler.</summary>
    public event Action<int> OnVariumChanged
    {
        add    { sOnVariumChanged += value; }
        remove { sOnVariumChanged -= value; }
    }

    /// <summary>Instance accessor — same value as Total.</summary>
    public int Varium => sVarium;

    /// <summary>
    /// Backward-compat singleton accessor. Returns the PlayerWallet on the
    /// active party member if any; otherwise the first one found.
    /// </summary>
    public static PlayerWallet Instance
    {
        get
        {
            if (Party.Active != null)
            {
                var w = Party.Active.GetComponent<PlayerWallet>();
                if (w != null) return w;
            }
            // Fallback: any player wallet in the scene.
            return UnityEngine.Object.FindObjectOfType<PlayerWallet>();
        }
    }

    public void AddVarium(int amount)
    {
        if (amount == 0) return;

        sVarium = Mathf.Max(0, sVarium + amount);
        sOnVariumChanged?.Invoke(sVarium);
    }

    public bool SpendVarium(int amount)
    {
        if (amount < 0) return false;
        if (sVarium < amount) return false;

        sVarium -= amount;
        sOnVariumChanged?.Invoke(sVarium);
        return true;
    }

    public bool CanAfford(int amount) => amount <= sVarium;

    /// <summary>Wipe varium to 0. Called by SaveManager on New Game.</summary>
    public static void ClearAll()
    {
        sVarium = 0;
        sOnVariumChanged?.Invoke(sVarium);
    }

    /// <summary>Directly set the varium amount (no event-friendly diff). Used by Load.</summary>
    public static void SetTotal(int amount)
    {
        sVarium = Mathf.Max(0, amount);
        sOnVariumChanged?.Invoke(sVarium);
    }
}
