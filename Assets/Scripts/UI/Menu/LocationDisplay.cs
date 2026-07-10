using TMPro;
using UnityEngine;

/// <summary>
/// Footer widget: shows the current location name from GameState.
/// </summary>
public class LocationDisplay : MonoBehaviour
{
    public TMP_Text locationText;

    void OnEnable()
    {
        if (GameState.Instance != null)
            GameState.Instance.OnLocationChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (GameState.Instance != null)
            GameState.Instance.OnLocationChanged -= Refresh;
    }

    void Refresh()
    {
        if (locationText == null) return;
        locationText.text = GameState.Instance != null
            ? GameState.Instance.currentLocation
            : "";
    }
}
