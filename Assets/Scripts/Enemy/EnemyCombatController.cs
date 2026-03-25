using UnityEngine;

public class EnemyCombatController : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;
    public EnemyMotor motor;

    public GameObject meleeHitbox;
    public Transform meleeHitboxTransform;

    public Transform player;

    [Header("Attack")]
    public float attackDistance = 1.4f;
    public float attackCooldown = 1.2f;

    [Header("Hitbox")]
    public float hitboxForwardDistance = 0.6f;
    public Vector3 hitboxLocalOffset;

    float nextAttackTime;

    bool isAttacking;

    static readonly int AttackTrigger = Animator.StringToHash("Attack");
    static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");

    void Awake()
    {
        if (!motor)
            motor = GetComponent<EnemyMotor>();

        if (!animator)
            animator = GetComponentInChildren<Animator>();

        DisableHitbox();
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
        animator.ResetTrigger(AttackTrigger);
        animator.SetTrigger(AttackTrigger);

        nextAttackTime = Time.time + attackCooldown;
    }

    // ANIMATION EVENT
    public void EnableHitbox()
    {
        if (!meleeHitbox)
            return;

        PositionHitbox();

        var hb = meleeHitbox.GetComponent<MeleeHitbox>();

        if (hb)
            hb.SetOwner(transform);

        meleeHitbox.SetActive(true);
    }

    // ANIMATION EVENT
    public void DisableHitbox()
    {
        if (meleeHitbox)
            meleeHitbox.SetActive(false);
    }

    // ANIMATION EVENT
    public void EndAttack()
    {
        isAttacking = false;

        animator.SetBool(IsAttackingHash, false);
    }

    void PositionHitbox()
    {
        if (!player || !meleeHitboxTransform)
            return;

        Vector3 dir = player.position - transform.position;

        dir.y = 0f;
        dir.Normalize();

        // convertir a espacio local del enemigo
        Vector3 localDir = transform.InverseTransformDirection(dir);

        Vector3 forward =
            new Vector3(localDir.x, 0f, localDir.z).normalized;

        meleeHitboxTransform.localPosition =
            forward * hitboxForwardDistance +
            hitboxLocalOffset;
    }
}