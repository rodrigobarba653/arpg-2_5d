using UnityEngine;

public class TrashSpawner : MonoBehaviour
{
    public GameObject trashPrefab;
    public float spawnRate = 3f; // This is set to 3 seconds!

    void Start()
    {
        // This starts the timer as soon as the game opens
        InvokeRepeating("SpawnTrash", 0f, spawnRate);
    }

    void SpawnTrash()
    {
        // This creates the trash bag at the spawner's exact location
        Instantiate(trashPrefab, transform.position, Quaternion.identity);
    }
}