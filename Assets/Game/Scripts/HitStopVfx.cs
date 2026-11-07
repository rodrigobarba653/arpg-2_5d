using UnityEngine;

/// <summary>
/// Holds a spawned hit or finisher effect while hit stop is active,
/// then lets it continue. Also keeps its lifetime from counting down during the freeze.
/// </summary>
public class HitStopVfx : MonoBehaviour
{
    ParticleSystem[] systems;
    float[] savedSpeeds;
    Animator[] animators;
    float[] savedAnimatorSpeeds;
    float remaining = -1f;
    bool paused;

    public static void Arm(GameObject vfx, float lifetime)
    {
        if (vfx == null)
            return;

        var hold = vfx.GetComponent<HitStopVfx>();
        if (hold == null)
            hold = vfx.AddComponent<HitStopVfx>();

        hold.systems = vfx.GetComponentsInChildren<ParticleSystem>(true);
        hold.savedSpeeds = new float[hold.systems.Length];
        for (int i = 0; i < hold.systems.Length; i++)
        {
            if (hold.systems[i] != null)
                hold.savedSpeeds[i] = hold.systems[i].main.simulationSpeed;
        }

        hold.animators = vfx.GetComponentsInChildren<Animator>(true);
        hold.savedAnimatorSpeeds = new float[hold.animators.Length];
        for (int i = 0; i < hold.animators.Length; i++)
        {
            if (hold.animators[i] != null)
                hold.savedAnimatorSpeeds[i] = hold.animators[i].speed;
        }

        hold.remaining = lifetime > 0f ? lifetime : -1f;

        if (HitStopperManager.Frozen)
            hold.SetPaused(true);
    }

    void Update()
    {
        if (HitStopperManager.Frozen)
        {
            if (!paused)
                SetPaused(true);
            return;
        }

        if (paused)
            SetPaused(false);

        if (remaining < 0f)
            return;

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
            Destroy(gameObject);
    }

    void SetPaused(bool value)
    {
        paused = value;
        if (systems == null)
            return;

        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem system = systems[i];
            if (system == null)
                continue;

            var main = system.main;
            if (value)
            {
                if (savedSpeeds != null && i < savedSpeeds.Length)
                    savedSpeeds[i] = main.simulationSpeed;
                main.simulationSpeed = 0f;
                system.Pause(true);
            }
            else
            {
                float speed = savedSpeeds != null && i < savedSpeeds.Length ? savedSpeeds[i] : 1f;
                if (speed <= 0f)
                    speed = 1f;
                main.simulationSpeed = speed;
                system.Play(true);
            }
        }

        if (animators == null)
            return;

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
                continue;

            if (value)
            {
                if (savedAnimatorSpeeds != null && i < savedAnimatorSpeeds.Length)
                    savedAnimatorSpeeds[i] = animator.speed;
                animator.speed = 0f;
            }
            else
            {
                float speed = savedAnimatorSpeeds != null && i < savedAnimatorSpeeds.Length
                    ? savedAnimatorSpeeds[i]
                    : 1f;
                animator.speed = speed;
            }
        }
    }
}
