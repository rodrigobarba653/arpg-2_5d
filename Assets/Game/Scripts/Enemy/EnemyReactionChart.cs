using UnityEngine;

/// <summary>
/// Designer chart for how this enemy answers the player's intent.
/// Each row is 10 points split across Press, Step Back, and Hold.
/// The roll happens once when that moment starts.
/// </summary>
public class EnemyReactionChart : MonoBehaviour
{
    public enum Preset
    {
        Custom,
        Aggressive,
        Cautious,
        Punisher
    }

    public enum Reaction
    {
        Press,
        StepBack,
        Hold
    }

    [System.Serializable]
    public struct Row
    {
        [Range(0, 10)] public int press;
        [Range(0, 10)] public int stepBack;
        [Range(0, 10)] public int hold;
    }

    [Tooltip("Fills the rows. Editing a number switches this to Custom.")]
    public Preset preset = Preset.Cautious;

    [Header("Rows (each sums to 10)")]
    [Tooltip("Player started a combo on this enemy.")]
    public Row swingAtMe = new Row { press = 1, stepBack = 7, hold = 2 };

    [Tooltip("Player's combo just ended.")]
    public Row comboEnded = new Row { press = 6, stepBack = 2, hold = 2 };

    [Tooltip("Player is swinging at a different enemy.")]
    public Row swingAtOther = new Row { press = 2, stepBack = 2, hold = 6 };

    [Tooltip("Player just cast a spell.")]
    public Row casting = new Row { press = 4, stepBack = 4, hold = 2 };

    [Header("Timing")]
    [Tooltip("How long a Press, Step Back, or Hold lasts after a combo ends or a cast. " +
             "A reaction to a swing lasts until that combo ends. " +
             "Press still waits on this enemy's own attack cooldown.")]
    public float responseWindow = 1f;

    [Tooltip("Extra distance past stop range that Step Back opens.")]
    public float stepBackDistance = 1.25f;

    [SerializeField, HideInInspector] Preset appliedPreset = Preset.Cautious;

    EnemyHealth health;
    EnemyAI ai;
    EnemyCombatController melee;
    EnemyRangedCombatController ranged;

    bool hasReaction;
    Reaction reaction;
    bool tiedToSwing;
    float reactionUntil;
    int rolledSwingId = -1;
    int tickedFrame = -1;

    public float StepBackDistance => stepBackDistance;
    public bool AllowsAttack => !hasReaction || reaction == Reaction.Press;
    public bool WantsStepBack => hasReaction && reaction == Reaction.StepBack;
    public bool WantsHold => hasReaction && reaction == Reaction.Hold;

    void Awake()
    {
        health = GetComponent<EnemyHealth>();
        ai = GetComponent<EnemyAI>();
        melee = GetComponent<EnemyCombatController>();
        ranged = GetComponent<EnemyRangedCombatController>();
    }

    void Update()
    {
        Tick();
    }

    /// <summary>Safe to call from combat and movement. Samples once per frame.</summary>
    public void Tick()
    {
        if (tickedFrame == Time.frameCount)
            return;
        tickedFrame = Time.frameCount;

        PlayerIntentSignals.Frame frame = PlayerIntentSignals.Current;
        if (frame.player == null || health == null || health.IsDead)
        {
            Clear();
            return;
        }

        float dist = Vector3.Distance(transform.position, frame.player.position);
        float detect = ai != null ? ai.detectDistance : 6f;
        bool inRange = dist <= detect;

        if (inRange && frame.swinging)
        {
            if (rolledSwingId != frame.swingId)
            {
                rolledSwingId = frame.swingId;
                float reach = melee != null ? melee.attackDistance
                    : ranged != null ? ranged.attackDistance
                    : 2f;
                bool atMe = frame.comboTarget == health
                    || (frame.comboTarget == null && dist <= reach);
                Commit(Roll(atMe ? swingAtMe : swingAtOther), untilSwingEnds: true);
            }
        }
        else if (!frame.swinging)
        {
            rolledSwingId = -1;
        }

        if (inRange && frame.comboEnded)
            Commit(Roll(comboEnded), untilSwingEnds: false);

        if (inRange && frame.castStarted)
            Commit(Roll(casting), untilSwingEnds: false);

        if (hasReaction && !tiedToSwing && Time.time >= reactionUntil)
            Clear();

        if (hasReaction && tiedToSwing && !frame.swinging)
            Clear();
    }

    void Commit(Reaction next, bool untilSwingEnds)
    {
        hasReaction = true;
        reaction = next;
        tiedToSwing = untilSwingEnds;
        reactionUntil = untilSwingEnds ? 0f : Time.time + Mathf.Max(0.05f, responseWindow);
    }

    void Clear()
    {
        hasReaction = false;
        tiedToSwing = false;
    }

    static Reaction Roll(Row row)
    {
        int roll = Random.Range(0, 10);
        if (roll < row.press)
            return Reaction.Press;
        if (roll < row.press + row.stepBack)
            return Reaction.StepBack;
        return Reaction.Hold;
    }

    void OnValidate()
    {
        if (preset != appliedPreset)
        {
            if (preset != Preset.Custom)
                ApplyPreset(preset);
            appliedPreset = preset;
        }
        else if (preset != Preset.Custom && !Matches(preset))
        {
            preset = Preset.Custom;
            appliedPreset = Preset.Custom;
        }

        Normalize(ref swingAtMe);
        Normalize(ref comboEnded);
        Normalize(ref swingAtOther);
        Normalize(ref casting);
    }

    void ApplyPreset(Preset next)
    {
        switch (next)
        {
            case Preset.Aggressive:
                swingAtMe = RowOf(7, 1, 2);
                comboEnded = RowOf(8, 0, 2);
                swingAtOther = RowOf(6, 1, 3);
                casting = RowOf(8, 0, 2);
                break;
            case Preset.Cautious:
                swingAtMe = RowOf(1, 7, 2);
                comboEnded = RowOf(6, 2, 2);
                swingAtOther = RowOf(2, 2, 6);
                casting = RowOf(4, 4, 2);
                break;
            case Preset.Punisher:
                swingAtMe = RowOf(1, 2, 7);
                comboEnded = RowOf(5, 2, 3);
                swingAtOther = RowOf(8, 0, 2);
                casting = RowOf(8, 1, 1);
                break;
        }
    }

    bool Matches(Preset next)
    {
        Row a, b, c, d;
        switch (next)
        {
            case Preset.Aggressive:
                a = RowOf(7, 1, 2); b = RowOf(8, 0, 2); c = RowOf(6, 1, 3); d = RowOf(8, 0, 2);
                break;
            case Preset.Cautious:
                a = RowOf(1, 7, 2); b = RowOf(6, 2, 2); c = RowOf(2, 2, 6); d = RowOf(4, 4, 2);
                break;
            case Preset.Punisher:
                a = RowOf(1, 2, 7); b = RowOf(5, 2, 3); c = RowOf(8, 0, 2); d = RowOf(8, 1, 1);
                break;
            default:
                return true;
        }

        return Same(swingAtMe, a) && Same(comboEnded, b) && Same(swingAtOther, c) && Same(casting, d);
    }

    static bool Same(Row x, Row y)
    {
        return x.press == y.press && x.stepBack == y.stepBack && x.hold == y.hold;
    }

    static Row RowOf(int press, int stepBack, int hold)
    {
        return new Row { press = press, stepBack = stepBack, hold = hold };
    }

    static void Normalize(ref Row row)
    {
        row.press = Mathf.Clamp(row.press, 0, 10);
        row.stepBack = Mathf.Clamp(row.stepBack, 0, 10 - row.press);
        row.hold = 10 - row.press - row.stepBack;
    }
}
