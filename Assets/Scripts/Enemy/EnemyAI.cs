using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public EnemyMotor motor;
    public Transform player;

    [Header("Distances")]
    public float detectDistance = 6f;
    public float stopDistance = 1.5f;

    void Awake()
    {
        if (!motor)
            motor = GetComponent<EnemyMotor>();
    }

    void Update()
    {
        if (!player)
            return;

        float dist =
            Vector3.Distance(
                transform.position,
                player.position
            );

        // no detecta
        if (dist > detectDistance)
        {
            motor.Stop();
            return;
        }

        // demasiado cerca
        if (dist <= stopDistance)
        {
            motor.Stop();
            return;
        }

        // seguir
        Vector3 dir =
            player.position - transform.position;

        dir.y = 0f;

        motor.SetMoveDirection(dir);
    }
}