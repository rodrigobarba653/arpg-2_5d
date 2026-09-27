using UnityEngine;

/// <summary>
/// Designer chart for how this enemy answers the player's intent.
/// Each row is 10 points split across Press, Step Back, and Hold.
/// Defend is a fourth slice, and it only shows when this enemy can defend.
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
        Hold,
        Defend
    }

    [System.Serializable]
    public struct Row
    {
        [Range(0, 10)] public int press;
        [Range(0, 10)] public int stepBack;
        [Range(0, 10)] public int hold;
        [Range(0, 10)] public int defend;
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
             "A reaction to a swing lasts until that combo ends. Hold strafes instead of standing still. " +
             "Press still waits on this enemy's own attack cooldown.")]
    public float responseWindow = 1f;

    [Tooltip("Extra distance past stop range that Step Back opens.")]
    public float stepBackDistance = 1.25f;

    [Tooltip("Hold strafe speed as a fraction of chase speed. Lower feels like a sidestep.")]
    [Range(0.15f, 1f)]
    public float holdStrafeSpeed = 0.55f;

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
    public float HoldStrafeSpeed => holdStrafeSpeed;
    public bool AllowsAttack => !hasReaction || reaction == Reaction.Press;
    public bool WantsStepBack => hasReaction && reaction == Reaction.StepBack;
    public bool WantsHold => hasReaction && reaction == Reaction.Hold;
    public bool WantsDefend => hasReaction && reaction == Reaction.Defend;

    public bool ShowsDefend
    {
        get
        {
            if (ai == null)
                ai = GetComponent<EnemyAI>();
            return ai != null && ai.canDefend;
        }
    }

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
        float notice = ai != null ? ai.detectDistance : 6f;
        float keep = ai != null ? Mathf.Max(notice, ai.disengageDistance) : notice;
        bool inRange = ai != null && ai.IsChasing ? dist <= keep : dist <= notice;

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

    public void CancelReaction()
    {
        Clear();
    }

    void Clear()
    {
        hasReaction = false;
        tiedToSwing = false;
    }

    Reaction Roll(Row row)
    {
        bool showDefend = ShowsDefend;
        int defend = showDefend ? row.defend : 0;
        int hold = row.hold + (showDefend ? 0 : row.defend);
        int roll = Random.Range(0, 10);
        if (roll < row.press)
            return Reaction.Press;
        if (roll < row.press + row.stepBack)
            return Reaction.StepBack;
        if (roll < row.press + row.stepBack + hold)
            return Reaction.Hold;
        return Reaction.Defend;
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

        RefreshShares();
    }

    public void RefreshShares()
    {
        bool showDefend = ShowsDefend;
        Normalize(ref swingAtMe, showDefend);
        Normalize(ref comboEnded, showDefend);
        Normalize(ref swingAtOther, showDefend);
        Normalize(ref casting, showDefend);
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
        return x.press == y.press && x.stepBack == y.stepBack && x.hold == y.hold && x.defend == y.defend;
    }

    static Row RowOf(int press, int stepBack, int hold)
    {
        return new Row { press = press, stepBack = stepBack, hold = hold, defend = 0 };
    }

    /// <summary>
    /// Writes one slice and keeps the row at 10.
    /// Extra points come out of Hold first. Points given up go back to Hold,
    /// unless Hold itself was lowered, in which case they go to Defend.
    /// </summary>
    public static void SetShare(ref Row row, int edited, int value, bool showDefend)
    {
        value = Mathf.Clamp(value, 0, 10);
        int[] points = { row.press, row.stepBack, row.hold, showDefend ? row.defend : 0 };
        if (edited < 0 || edited > 3)
            return;
        if (edited == 3 && !showDefend)
            return;

        points[edited] = value;
        if (!showDefend)
            points[3] = 0;

        int sum = points[0] + points[1] + points[2] + points[3];
        int[] trimOrder = { 2, 3, 1, 0 };
        for (int n = 0; n < trimOrder.Length && sum > 10; n++)
        {
            int i = trimOrder[n];
            if (i == edited || (!showDefend && i == 3) || points[i] <= 0)
                continue;
            int take = Mathf.Min(points[i], sum - 10);
            points[i] -= take;
            sum -= take;
        }

        if (sum > 10)
            points[edited] -= sum - 10;

        if (sum < 10)
        {
            int fill = showDefend && edited == 2 ? 3 : 2;
            points[fill] += 10 - sum;
        }

        row.press = points[0];
        row.stepBack = points[1];
        row.hold = points[2];
        row.defend = points[3];
    }

    static void Normalize(ref Row row, bool showDefend)
    {
        row.press = Mathf.Clamp(row.press, 0, 10);
        row.stepBack = Mathf.Clamp(row.stepBack, 0, 10);
        row.hold = Mathf.Clamp(row.hold, 0, 10);
        row.defend = showDefend ? Mathf.Clamp(row.defend, 0, 10) : 0;

        int sum = row.press + row.stepBack + row.hold + row.defend;
        if (sum == 10)
            return;

        if (sum > 10)
        {
            int excess = sum - 10;
            int hold = row.hold;
            int defend = row.defend;
            int step = row.stepBack;
            int press = row.press;
            Trim(ref hold, ref excess);
            Trim(ref defend, ref excess);
            Trim(ref step, ref excess);
            Trim(ref press, ref excess);
            row.hold = hold;
            row.defend = defend;
            row.stepBack = step;
            row.press = press;
            return;
        }

        if (showDefend)
            row.defend += 10 - sum;
        else
            row.hold += 10 - sum;
    }

    static void Trim(ref int points, ref int excess)
    {
        if (excess <= 0 || points <= 0)
            return;
        int take = Mathf.Min(points, excess);
        points -= take;
        excess -= take;
    }
}
