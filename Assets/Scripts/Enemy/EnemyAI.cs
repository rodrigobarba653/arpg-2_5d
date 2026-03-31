using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public EnemyMotor motor;
    public Transform player;
    Animator animator;

    [Header("Distances")]
    public float detectDistance = 6f;
    public float stopDistance = 1.5f;

    [Header("Defense")]
    public bool canDefend = true;
    [Range(0, 1)] public float defendChance = 0.3f;
    public float defendCooldown = 3f;
    [HideInInspector] public float lastDefendTime;

    [Header("Defense Range")]
    public float defendDistance = 2f;

    [Header("Defense State")]
    public bool isDefending;
    public float defendDuration = 1.5f;

    float defendTimer;

    void Awake()
    {
        if (!motor)
            motor = GetComponent<EnemyMotor>();

        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (!player)
            return;

        float dist = Vector3.Distance(transform.position, player.position);

        // ===== DEFENSE TIMER =====
        if (isDefending)
        {
            defendTimer -= Time.deltaTime;

            motor.Stop();

            if (defendTimer <= 0f)
            {
                EndDefense();
            }

            return;
        }

        // ===== DECIDIR DEFENDER =====
        if (canDefend &&
            Time.time >= lastDefendTime + defendCooldown &&
            dist <= defendDistance &&
            dist > stopDistance * 0.8f) // evita defender pegado pegado
        {
            if (Random.value < defendChance)
            {
                StartDefense();
                return;
            }
        }

        // ===== NORMAL AI =====

        if (dist > detectDistance)
        {
            motor.Stop();
            return;
        }

        if (dist <= stopDistance)
        {
            motor.Stop();
            return;
        }

        Vector3 moveDir = player.position - transform.position;
        moveDir.y = 0f;

        motor.SetMoveDirection(moveDir);
    }

    void StartDefense()
    {
        isDefending = true;
        defendTimer = defendDuration;
        lastDefendTime = Time.time;

        if (animator)
            animator.SetBool("isDefending", true);
    }

    void EndDefense()
    {
        isDefending = false;

        if (animator)
            animator.SetBool("isDefending", false);
    }
}