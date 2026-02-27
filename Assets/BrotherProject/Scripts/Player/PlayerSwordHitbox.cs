using UnityEngine;
using System.Collections.Generic;

public class SwordHitbox : MonoBehaviour
{
    public int damage = 10;

    HashSet<Collider> hitEnemies = new HashSet<Collider>();

    void OnEnable()
    {
        hitEnemies.Clear();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy") && !hitEnemies.Contains(other))
        {
            hitEnemies.Add(other);

            EnemyHealth enemy = other.GetComponent<EnemyHealth>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);

                FindObjectOfType<HitStopManager>().DoHitStop(0.07f);
            }
        }
    }

}
