using UnityEngine;

/// <summary>
/// One shared read of what the active player is doing this frame.
/// Edges (swing started, combo ended, cast) stay true for the whole frame
/// so every enemy sees the same moment and rolls once.
/// </summary>
public static class PlayerIntentSignals
{
    public struct Frame
    {
        public Transform player;
        public bool swinging;
        public bool swingStarted;
        public bool comboEnded;
        public bool castStarted;
        public int swingId;
        public EnemyHealth comboTarget;
    }

    static Frame current;
    static int sampledFrame = -1;
    static bool prevSwinging;
    static int swingId;
    static int pendingCasts;

    public static Frame Current
    {
        get
        {
            Sample();
            return current;
        }
    }

    /// <summary>Call from a successful player cast. Casts are instant, so this is the notice.</summary>
    public static void NotifyCast()
    {
        pendingCasts++;
    }

    static void Sample()
    {
        if (sampledFrame == Time.frameCount)
            return;

        sampledFrame = Time.frameCount;

        PlayerHealth health = Party.Active;
        Transform player = health != null ? health.transform : null;
        var combat = health != null ? health.GetComponent<PlayerCombatController>() : null;
        var soft = health != null ? health.GetComponent<MeleeSoftTargeting>() : null;

        bool swinging = combat != null && combat.IsAttacking();
        bool started = swinging && !prevSwinging;
        if (started)
            swingId++;

        current = new Frame
        {
            player = player,
            swinging = swinging,
            swingStarted = started,
            comboEnded = !swinging && prevSwinging,
            castStarted = pendingCasts > 0,
            swingId = swingId,
            comboTarget = (soft != null && swinging) ? soft.ComboTarget : null
        };

        pendingCasts = 0;
        prevSwinging = swinging;
    }
}
