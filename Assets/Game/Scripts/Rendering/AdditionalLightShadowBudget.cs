using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps point and spot shadow maps inside the URP atlas.
/// The budget goes to lights whose reach enters the camera view, nearest first.
/// A point light costs 6 maps. A spot costs 1. Directional lights are left alone.
/// </summary>
public class AdditionalLightShadowBudget : MonoBehaviour
{
    [Tooltip("How many point and spot shadow maps may be active. A point light costs 6. A spot costs 1.")]
    public int maxShadowMaps = 32;

    [Tooltip("Seconds between distance checks.")]
    public float refreshInterval = 0.25f;

    static AdditionalLightShadowBudget instance;

    readonly Dictionary<Light, LightShadows> authoredShadows = new Dictionary<Light, LightShadows>();
    readonly List<Light> lights = new List<Light>();
    readonly List<Light> dropped = new List<Light>();
    readonly List<Candidate> candidates = new List<Candidate>();
    readonly Plane[] frustumPlanes = new Plane[6];
    float nextRefresh;

    struct Candidate
    {
        public Light light;
        public LightShadows authored;
        public float distance;
        public int cost;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        var host = new GameObject(nameof(AdditionalLightShadowBudget));
        DontDestroyOnLoad(host);
        instance = host.AddComponent<AdditionalLightShadowBudget>();
        SceneManager.sceneLoaded += instance.OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }

        RestoreAuthoredShadows();
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
        ApplyBudget();
    }

    void ApplyBudget()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        Vector3 view = camera.transform.position;
        GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
        lights.Clear();
        lights.AddRange(FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        PruneDestroyedLights();

        candidates.Clear();
        for (int i = 0; i < lights.Count; i++)
        {
            Light light = lights[i];
            if (light == null || (light.type != LightType.Point && light.type != LightType.Spot))
            {
                ReleaseIfTracked(light);
                continue;
            }

            if (!authoredShadows.TryGetValue(light, out LightShadows authored))
            {
                if (light.shadows == LightShadows.None)
                    continue;

                authored = light.shadows;
                authoredShadows.Add(light, authored);
            }

            if (!OverlapsView(light))
            {
                if (light.shadows != LightShadows.None)
                    light.shadows = LightShadows.None;
                continue;
            }

            candidates.Add(new Candidate
            {
                light = light,
                authored = authored,
                distance = (light.transform.position - view).sqrMagnitude,
                cost = light.type == LightType.Point ? 6 : 1
            });
        }

        candidates.Sort((a, b) => a.distance.CompareTo(b.distance));

        int remaining = Mathf.Max(0, maxShadowMaps);
        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            if (candidate.light == null)
                continue;

            bool afford = candidate.cost <= remaining;
            LightShadows desired = afford ? candidate.authored : LightShadows.None;
            if (candidate.light.shadows != desired)
                candidate.light.shadows = desired;

            if (afford)
                remaining -= candidate.cost;
        }
    }

    bool OverlapsView(Light light)
    {
        float range = Mathf.Max(0.01f, light.range);
        Bounds bounds;
        if (light.type == LightType.Spot)
        {
            float radius = range * Mathf.Tan(light.spotAngle * 0.5f * Mathf.Deg2Rad);
            Vector3 tip = light.transform.position;
            bounds = new Bounds(tip, Vector3.zero);
            bounds.Encapsulate(tip + light.transform.forward * range);
            bounds.Expand(radius * 2f);
        }
        else
        {
            bounds = new Bounds(light.transform.position, Vector3.one * (range * 2f));
        }

        return GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
    }

    void ReleaseIfTracked(Light light)
    {
        if (light == null || !authoredShadows.TryGetValue(light, out LightShadows authored))
            return;

        if (light.shadows != authored)
            light.shadows = authored;

        authoredShadows.Remove(light);
    }

    void PruneDestroyedLights()
    {
        dropped.Clear();
        foreach (Light light in authoredShadows.Keys)
        {
            if (light == null)
                dropped.Add(light);
        }

        for (int i = 0; i < dropped.Count; i++)
            authoredShadows.Remove(dropped[i]);
    }

    void RestoreAuthoredShadows()
    {
        foreach (KeyValuePair<Light, LightShadows> entry in authoredShadows)
        {
            if (entry.Key == null)
                continue;

            if (entry.Key.shadows != entry.Value)
                entry.Key.shadows = entry.Value;
        }
    }
}
