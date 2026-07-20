using UnityEngine;

/// <summary>
/// Makes a transform gently bob up and down on the local Y axis using a Sin wave.
/// Drop on the icon / sprite of a pickup to give it a "floating" feel.
///
/// Multiple instances stay out of phase via the optional phaseOffset, so a group
/// of pickups doesn't oscillate in perfect sync.
/// </summary>
public class FloatingBob : MonoBehaviour
{
    [Tooltip("How many world units the sprite moves above and below its starting position.")]
    public float amplitude = 0.1f;

    [Tooltip("Oscillations per second.")]
    public float frequency = 1f;

    [Tooltip("Time offset (seconds) added to the wave — useful to desync multiple pickups.")]
    public float phaseOffset = 0f;

    [Tooltip("If true, randomizes the phase offset on Awake so several pickups in the same scene don't bob in lockstep.")]
    public bool randomizePhase = true;

    Vector3 startLocalPos;

    void Awake()
    {
        startLocalPos = transform.localPosition;

        if (randomizePhase)
            phaseOffset += Random.value * (1f / Mathf.Max(0.0001f, frequency));
    }

    void OnEnable()
    {
        // Keep the resting position as the baseline even if the script is toggled.
        startLocalPos = transform.localPosition;
    }

    void Update()
    {
        float t = (Time.time + phaseOffset) * frequency * Mathf.PI * 2f;
        float y = Mathf.Sin(t) * amplitude;

        Vector3 p = startLocalPos;
        p.y += y;
        transform.localPosition = p;
    }
}
