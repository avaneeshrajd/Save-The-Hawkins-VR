using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject demoPrefab;
    public Transform[] spawnPoints;
    public float spawnRate = 10f;

    void Start()
    {
        InvokeRepeating(nameof(Spawn), spawnRate, spawnRate);
    }

    void Spawn()
    {
        for (int i = 0; i < 2; i++)
        {
            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            Instantiate(demoPrefab, point.position, point.rotation);
        }

    }
}
