using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject demoPrefab;
    [SerializeField] private Transform[] spawnPoints;
    
    private float spawnRate = 10f;
    private float spawnTime = 2f;
    

    void Start()
    {
        InvokeRepeating(nameof(Spawn), spawnTime, spawnRate);
    }

    void Spawn()
    {
        for (int i = 0; i < 6; i++)
        {
            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            Instantiate(demoPrefab, point.position, point.rotation);
        }
    }
}
