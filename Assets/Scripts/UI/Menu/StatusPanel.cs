using UnityEngine;

/// <summary>
/// "Status" panel: shows every PlayerHealth currently in the scene.
/// One widget per slot — widgets beyond the number of players are hidden.
/// </summary>
public class StatusPanel : MenuPanel
{
    [Tooltip("Pre-placed widget GameObjects in the panel (one per party slot). " +
             "Widgets beyond the number of registered PlayerHealths get hidden.")]
    public CharacterStatusWidget[] widgets;

    void OnEnable()
    {
        // Re-bind whenever a member joins/leaves the party (so the second
        // character appearing mid-menu shows up immediately).
        Party.OnPartyChanged += Refresh;
        Party.OnActiveChanged += HandleActiveChanged;
    }

    void OnDisable()
    {
        Party.OnPartyChanged -= Refresh;
        Party.OnActiveChanged -= HandleActiveChanged;
    }

    void HandleActiveChanged(PlayerHealth _) => Refresh();

    public override void Refresh()
    {
        if (widgets == null || widgets.Length == 0)
        {
            Debug.LogWarning("[StatusPanel] 'Widgets' array is empty. Drag your " +
                             "CharacterStatusWidget GameObjects into it in the Inspector.", this);
            return;
        }

        var players = PlayerHealth.All;

        if (players.Count == 0)
        {
            Debug.LogWarning("[StatusPanel] No PlayerHealth components found in scene. " +
                             "Make sure your Player GameObject has a PlayerHealth.", this);
        }

        for (int i = 0; i < widgets.Length; i++)
        {
            if (widgets[i] == null) continue;

            if (i < players.Count)
            {
                widgets[i].gameObject.SetActive(true);
                widgets[i].Bind(players[i]);
            }
            else
            {
                widgets[i].Bind(null);
                widgets[i].gameObject.SetActive(false);
            }
        }
    }
}
