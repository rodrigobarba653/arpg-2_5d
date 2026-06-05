using System.Collections.Generic;
using UnityEngine;

public class EnemyAttackScheduler : MonoBehaviour
{
    static EnemyAttackScheduler _instance;

    public static EnemyAttackScheduler Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<EnemyAttackScheduler>();

                if (_instance == null)
                {
                    var go = new GameObject("EnemyAttackScheduler");
                    _instance = go.AddComponent<EnemyAttackScheduler>();
                }
            }

            return _instance;
        }
    }

    [Header("Limits")]
    [Tooltip("How many enemies can be performing an attack at the same time.")]
    public int maxSimultaneousAttackers = 1;

    [Tooltip("Minimum time (seconds) between the start of any two enemy attacks.")]
    public float minStaggerBetweenAttacks = 0.35f;

    [Header("Safety")]
    [Tooltip("If a reservation is held longer than this (seconds) it's auto-released. " +
             "Prevents the scheduler from getting stuck if an attack animation fails to fire EndAttack.")]
    public float maxReservationTime = 3f;

    struct Token
    {
        public Object attacker;
        public float reservedAt;
    }

    readonly List<Token> active = new List<Token>();
    float nextAttackAllowedTime;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }

        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    void PruneStale()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var t = active[i];

            // Owner destroyed/disabled? Drop.
            if (t.attacker == null)
            {
                active.RemoveAt(i);
                continue;
            }

            // Held too long? Drop (anim event probably never fired).
            if (Time.time - t.reservedAt > maxReservationTime)
            {
                active.RemoveAt(i);
            }
        }
    }

    public bool TryReserve(Object attacker)
    {
        if (attacker == null)
            return false;

        PruneStale();

        for (int i = 0; i < active.Count; i++)
        {
            if (active[i].attacker == attacker)
                return true;
        }

        if (active.Count >= maxSimultaneousAttackers)
            return false;

        if (Time.time < nextAttackAllowedTime)
            return false;

        active.Add(new Token { attacker = attacker, reservedAt = Time.time });
        nextAttackAllowedTime = Time.time + minStaggerBetweenAttacks;
        return true;
    }

    public void Release(Object attacker)
    {
        if (attacker == null)
            return;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (active[i].attacker == attacker)
            {
                active.RemoveAt(i);
                return;
            }
        }
    }

    public bool IsReserved(Object attacker)
    {
        if (attacker == null) return false;

        for (int i = 0; i < active.Count; i++)
        {
            if (active[i].attacker == attacker)
                return true;
        }

        return false;
    }

    public int ActiveCount => active.Count;

    public static void ReleaseIfExists(Object attacker)
    {
        if (_instance != null)
            _instance.Release(attacker);
    }
}
