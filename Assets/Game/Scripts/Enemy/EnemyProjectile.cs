using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public int damage = 5;
    public float lifeTime = 5f;
    public float speed = 6f;

    Rigidbody rb;
    Vector3 heldVelocity;
    bool held;
    float age;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Launch(Vector3 dir)
    {
        dir.y = 0f;

        transform.rotation = Quaternion.LookRotation(dir);

        rb.linearVelocity = dir.normalized * speed;
    }

    void Update()
    {
        if (HitStopperManager.Frozen)
        {
            if (rb != null && !held)
            {
                heldVelocity = rb.linearVelocity;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                held = true;
            }
            return;
        }

        if (held && rb != null)
        {
            rb.linearVelocity = heldVelocity;
            held = false;
        }

        age += Time.deltaTime;
        if (age >= lifeTime)
            Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health != null)
            health.TakeDamage(damage, transform.forward);

        Destroy(gameObject);
    }
}