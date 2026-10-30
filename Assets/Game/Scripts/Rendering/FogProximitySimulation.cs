using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Pauses particle systems that are far from the controlled player.
/// Systems that already finished are left alone, so a one-shot does not replay when the player walks past.
/// Runs in every scene during Play. Edit mode is left untouched.
/// </summary>
public class FogProximitySimulation : MonoBehaviour
{
    [Tooltip("Particle systems within this distance of the player keep simulating, in meters.")]
    public float activeDistance = 40f;

    [Tooltip("Extra meters before a running system pauses, so it does not flicker on the edge.")]
    public float hysteresis = 10f;

    [Tooltip("Seconds between distance checks.")]
    public float refreshInterval = 0.25f;

    static FogProximitySimulation instance;

    readonly Dictionary<ParticleSystem, Entry> entries = new Dictionary<ParticleSystem, Entry>();
    readonly List<ParticleSystem> dropped = new List<ParticleSystem>();
    readonly List<ParticleSystem> systems = new List<ParticleSystem>();
    float nextRefresh;

    sealed class Entry
    {
        public ParticleSystemRenderer renderer;
        public bool suspended;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        var host = new GameObject(nameof(FogProximitySimulation));
        DontDestroyOnLoad(host);
        instance = host.AddComponent<FogProximitySimulation>();
        SceneManager.sceneLoaded += instance.OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }

        ResumeAll();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        nextRefresh = 0f;
    }

    void Update()
    {
        if (Time.unscaledTime < nextRefresh)
            return;

        nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
        Scan();
        Apply();
    }

    void Scan()
    {
        systems.Clear();
        systems.AddRange(FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));

        for (int i = 0; i < systems.Count; i++)
        {
            ParticleSystem system = systems[i];
            if (system == null || entries.ContainsKey(system))
                continue;

            var main = system.main;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            entries.Add(system, new Entry
            {
                renderer = system.GetComponent<ParticleSystemRenderer>(),
                suspended = false
            });
        }

        dropped.Clear();
        foreach (ParticleSystem system in entries.Keys)
        {
            if (system == null)
                dropped.Add(system);
        }

        for (int i = 0; i < dropped.Count; i++)
            entries.Remove(dropped[i]);
    }

    void Apply()
    {
        if (!TryGetPlayerPosition(out Vector3 player))
            return;

        float near = Mathf.Max(0.5f, activeDistance);
        float far = near + Mathf.Max(0f, hysteresis);
        float nearSqr = near * near;
        float farSqr = far * far;

        foreach (KeyValuePair<ParticleSystem, Entry> pair in entries)
        {
            ParticleSystem system = pair.Key;
            Entry entry = pair.Value;
            if (system == null)
                continue;

            float distance = (system.transform.position - player).sqrMagnitude;
            if (entry.suspended)
            {
                if (distance > nearSqr)
                    continue;

                if (entry.renderer != null)
                    entry.renderer.enabled = true;
                system.Play(true);
                entry.suspended = false;
                continue;
            }

            if (!system.isPlaying || system.isPaused || distance <= farSqr)
                continue;

            system.Pause(true);
            if (entry.renderer != null)
                entry.renderer.enabled = false;
            entry.suspended = true;
        }
    }

    void ResumeAll()
    {
        foreach (KeyValuePair<ParticleSystem, Entry> pair in entries)
        {
            if (pair.Key == null)
                continue;

            if (!pair.Value.suspended)
                continue;

            if (pair.Value.renderer != null)
                pair.Value.renderer.enabled = true;

            pair.Key.Play(true);
        }
    }

    static bool TryGetPlayerPosition(out Vector3 position)
    {
        PartyMemberControl[] members = FindObjectsByType<PartyMemberControl>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < members.Length; i++)
        {
            PartyMemberControl member = members[i];
            if (member == null || !member.isControlled)
                continue;

            position = member.transform.position;
            return true;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            position = player.transform.position;
            return true;
        }

        Camera camera = Camera.main;
        if (camera != null)
        {
            position = camera.transform.position;
            return true;
        }

        position = default;
        return false;
    }
}
