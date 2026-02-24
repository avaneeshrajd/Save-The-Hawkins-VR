using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject demoPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnRate = 10f;

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
