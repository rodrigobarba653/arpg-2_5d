using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public int damage = 5;
    public float lifeTime = 5f;
    public float speed = 6f;

    Rigidbody rb;

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

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Destroy(gameObject);
        }
    }
}