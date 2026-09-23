using UnityEngine;

public class TrashSpawner : MonoBehaviour
{
    public GameObject trashPrefab;
    public float spawnRate = 3f;

    [Tooltip("Seconds before each spawned BasuraCayendo is destroyed.")]
    public float lifetime = 3f;

    void Start()
    {
        InvokeRepeating(nameof(SpawnTrash), 0f, spawnRate);
    }

    void SpawnTrash()
    {
        if (trashPrefab == null) return;

        var trash = Instantiate(trashPrefab, transform.position, Quaternion.identity);
        if (lifetime > 0f)
            Destroy(trash, lifetime);
    }
}
