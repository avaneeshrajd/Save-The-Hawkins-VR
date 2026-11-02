using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps a swarm of bats flying over Hawkins around the player.
/// Bats sometimes swoop past the player's head; swoops get more frequent as
/// EnemySpawner.Difficulty rises. Shot bats are replaced after a short delay.
/// </summary>
public class BatSwarm : MonoBehaviour
{
    [Tooltip("Bat prefab. If empty, loads Resources/Bat (built by the editor menu Save The Hawkins > Build Bat Prefab).")]
    [SerializeField] private GameObject batPrefab;
    [SerializeField] private int batCount = 16;

    [Header("Flight area (around the player)")]
    [SerializeField] private float minRadius = 6f;
    [SerializeField] private float maxRadius = 28f;
    [SerializeField] private float minHeight = 4f;
    [SerializeField] private float maxHeight = 14f;

    [Header("Swoops")]
    [SerializeField] private float startSwoopInterval = 12f;
    [SerializeField] private float minSwoopInterval = 4f;

    [Header("Sound")]
    [Tooltip("Optional screech played when a bat swoops at the player.")]
    [SerializeField] private AudioClip swoopSound;
    [SerializeField] private float respawnDelay = 6f;

    private readonly List<BatFlyer> bats = new List<BatFlyer>();
    private Transform player;
    private float nextSwoop;

    public Transform Player => player;
    public PlayerHealth PlayerHealth { get; private set; }

    void Start()
    {
        if (Camera.main != null)
            player = Camera.main.transform;

        PlayerHealth = FindFirstObjectByType<PlayerHealth>();
        if (batPrefab == null)
            batPrefab = Resources.Load<GameObject>("Bat");

        if (batPrefab == null)
        {
            Debug.LogWarning("BatSwarm: no bat prefab. Run 'Save The Hawkins > Build Bat Prefab' in the editor.");
            enabled = false;
            return;
        }

        for (int i = 0; i < batCount; i++)
            SpawnBat(RandomFlightPoint());

        nextSwoop = Time.time + startSwoopInterval;
    }

    void Update()
    {
        if (Time.time < nextSwoop) return;

        float interval = Mathf.Lerp(startSwoopInterval, minSwoopInterval, EnemySpawner.Difficulty);
        nextSwoop = Time.time + interval * Random.Range(0.7f, 1.3f);

        BatFlyer bat = PickSwoopBat();
        if (bat != null)
            bat.StartSwoop();
    }

    BatFlyer PickSwoopBat()
    {
        if (player == null) return null;
        BatFlyer best = null;
        float bestDist = float.MaxValue;
        foreach (BatFlyer bat in bats)
        {
            if (bat == null || bat.IsDead || bat.IsSwooping) continue;
            float d = (bat.transform.position - player.position).sqrMagnitude;
            // Prefer a bat that is fairly close, so the swoop starts quickly.
            if (d < bestDist)
            {
                best = bat;
                bestDist = d;
            }
        }
        return best;
    }

    public Vector3 RandomFlightPoint()
    {
        Vector3 center = player != null ? player.position : transform.position;
        Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(minRadius, maxRadius);
        float groundY = player != null ? player.position.y - 1.6f : transform.position.y;
        return new Vector3(center.x + ring.x, groundY + Random.Range(minHeight, maxHeight), center.z + ring.y);
    }

    void SpawnBat(Vector3 position)
    {
        GameObject go = Instantiate(batPrefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
        BatFlyer bat = go.GetComponent<BatFlyer>();
        if (bat == null) bat = go.AddComponent<BatFlyer>();

        if (swoopSound != null)
        {
            AudioSource src = go.GetComponent<AudioSource>();
            if (src == null) src = go.AddComponent<AudioSource>();
            src.clip = swoopSound;
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.minDistance = 1f;
            src.maxDistance = 25f;
        }

        bat.Init(this);
        bats.Add(bat);
    }

    public void OnBatKilled(BatFlyer bat)
    {
        bats.Remove(bat);
        StartCoroutine(RespawnLater());
    }

    IEnumerator RespawnLater()
    {
        yield return new WaitForSeconds(respawnDelay);
        // Respawn far away and high up so it flies in, instead of popping in nearby.
        Vector3 p = RandomFlightPoint();
        SpawnBat(new Vector3(p.x, p.y + 6f, p.z));
    }
}
