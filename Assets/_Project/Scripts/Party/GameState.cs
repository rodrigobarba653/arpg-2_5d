using System;
using UnityEngine;

/// <summary>
/// Lightweight world state: only data that's NOT character-bound. Character
/// stats live on PlayerHealth, currency on PlayerWallet.
/// </summary>
public class GameState : MonoBehaviour
{
    public static GameState Instance { get; private set; }

    [Header("World")]
    public string currentLocation = "Feral Island West";

    [Header("Persistence")]
    public bool persistAcrossScenes = true;

    public event Action OnLocationChanged;

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
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetLocation(string locationName)
    {
        if (currentLocation == locationName) return;
        currentLocation = locationName;
        OnLocationChanged?.Invoke();
    }
}
