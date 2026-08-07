using TMPro;
using UnityEngine;

/// <summary>
/// Footer widget: shows the current Varium amount from PlayerWallet and
/// auto-refreshes when it changes.
/// </summary>
public class VariumDisplay : MonoBehaviour
{
    public TMP_Text amountText;
    public string format = "{0}";

    PlayerWallet wallet;

    void OnEnable()
    {
        TryHookWallet();
        Refresh(wallet != null ? wallet.Varium : 0);
    }

    void OnDisable()
    {
        if (wallet != null)
            wallet.OnVariumChanged -= Refresh;
    }

    void Update()
    {
        // Wallet may not exist yet on first enable (player spawns later, scene
        // load order, etc). Keep trying until we find it.
        if (wallet == null)
        {
            TryHookWallet();
            if (wallet != null)
                Refresh(wallet.Varium);
        }
    }

    void TryHookWallet()
    {
        if (wallet != null) return;

        wallet = PlayerWallet.Instance;
        if (wallet != null)
            wallet.OnVariumChanged += Refresh;
    }

    void Refresh(int amount)
    {
        if (amountText == null) return;
        amountText.text = string.Format(format, amount);
    }
}
