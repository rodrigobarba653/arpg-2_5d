using UnityEngine;

public class EnemyRangedCombatController : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;
    public EnemyMotor motor;

    public Transform player;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    public Transform shootPoint;

    [Header("Attack")]
    public float attackDistance = 6f;
    public float attackCooldown = 2f;

    float nextAttackTime;
    bool isAttacking;

    static readonly int AttackTrigger = Animator.StringToHash("RangeAttack");
    static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");

    void Awake()
    {
        if (!motor)
            motor = GetComponent<EnemyMotor>();

        if (!animator)
            animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (!player)
            return;

        if (isAttacking)
            return;

        float dist =
            Vector3.Distance(
                transform.position,
                player.position
            );

        if (dist > attackDistance)
            return;

        if (Time.time < nextAttackTime)
            return;

        StartAttack();
    }

    void StartAttack()
    {
        isAttacking = true;

        motor.Stop();

        animator.SetBool(IsAttackingHash, true);
        animator.SetTrigger(AttackTrigger);

        nextAttackTime = Time.time + attackCooldown;
    }

    // 🔥 animation event
    public void Shoot()
    {
        if (!projectilePrefab || !shootPoint || !player)
            return;

        Vector3 dir =
            player.position - shootPoint.position;

        dir.y = 0f;

        GameObject p =
            Instantiate(
                projectilePrefab,
                shootPoint.position,
                Quaternion.identity
            );

        EnemyProjectile proj =
            p.GetComponent<EnemyProjectile>();

        if (proj)
            proj.Launch(dir);
    }

    // animation event
    public void EndAttack()
    {
        isAttacking = false;
        animator.SetBool(IsAttackingHash, false);
    }
}