using UnityEngine;

/// <summary>
/// Plays footstep SFX at a configurable interval while the player is grounded
/// and moving horizontally. Picks a random clip from footstepSounds and varies
/// pitch + interval slightly so consecutive steps don't sound identical.
///
/// Drop on the Player GameObject. Optional refs auto-resolve.
/// </summary>
public class PlayerFootsteps : MonoBehaviour
{
    [Header("Refs")]
    public PlayerMotor motor;
    public PlayerJump jump;
    PlayerClimbing climbing;
    PlayerSwimming swim;

    [Header("Sounds")]
    [Tooltip("Random clip is picked per step. If empty, nothing plays.")]
    public AudioClip[] footstepSounds;

    [Range(0f, 1f)] public float volume = 0.6f;

    [Header("Timing")]
    [Tooltip("Seconds between footsteps while walking normally.")]
    public float stepInterval = 0.4f;

    [Tooltip("Minimum horizontal speed for footsteps to play.")]
    public float speedDeadzone = 0.1f;

    [Tooltip("If true, while rolling no footsteps play.")]
    public bool silentDuringRoll = true;

    [Header("Variation")]
    [Range(0f, 0.5f)]
    [Tooltip("Random pitch variation (1 ± pitchVariation).")]
    public float pitchVariation = 0.1f;

    [Range(0f, 0.3f)]
    [Tooltip("Random variation added to the interval each step.")]
    public float intervalVariation = 0.05f;

    [Header("3D")]
    [Tooltip("Plays footsteps at the player's position (3D). Disable for pure 2D playback.")]
    public bool positional = true;

    float timer;

    void Awake()
    {
        if (!motor)    motor    = GetComponent<PlayerMotor>();
        if (!jump)     jump     = GetComponent<PlayerJump>();
        climbing = GetComponent<PlayerClimbing>();
        swim     = GetComponent<PlayerSwimming>();
    }

    void Update()
    {
        if (motor == null) return;

        // No footsteps while climbing or swimming.
        if (climbing != null && climbing.IsClimbing()) { ResetTimer(); return; }
        if (swim != null && swim.IsSwimming())         { ResetTimer(); return; }

        // No footsteps while rolling (the roll SFX covers it).
        if (silentDuringRoll && motor.rollActive)      { ResetTimer(); return; }

        bool grounded = jump != null ? jump.IsGrounded : motor.IsGrounded();
        if (!grounded) { ResetTimer(); return; }

        float speed = motor.GetRealSpeed();
        if (speed < speedDeadzone) { ResetTimer(); return; }

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            PlayFootstep();
            timer = stepInterval + Random.Range(-intervalVariation, intervalVariation);
        }
    }

    void ResetTimer()
    {
        // Half-interval so the first step plays shortly after the player starts walking.
        timer = stepInterval * 0.5f;
    }

    void PlayFootstep()
    {
        if (footstepSounds == null || footstepSounds.Length == 0) return;

        AudioClip clip = footstepSounds[Random.Range(0, footstepSounds.Length)];
        if (clip == null) return;

        AudioSource src;

        if (positional && AudioManager.Instance != null)
        {
            src = AudioManager.Instance.PlaySFXAt(clip, transform.position, volume);
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(clip, volume);
            return;
        }
        else
        {
            return;
        }

        if (src != null && pitchVariation > 0f)
            src.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
    }
}
