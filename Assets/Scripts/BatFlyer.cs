using UnityEngine;

/// <summary>
/// One bat. Flies between random points around the player, sometimes swoops past
/// the player's head, avoids walls with a cheap sphere cast, and can be shot down.
/// Driven by BatSwarm.
/// </summary>
public class BatFlyer : MonoBehaviour
{
    [SerializeField] private float minSpeed = 4f;
    [SerializeField] private float maxSpeed = 7f;
    [SerializeField] private float swoopSpeed = 10f;
    [SerializeField] private float turnRate = 2.5f;
    [SerializeField] private float maxBankAngle = 45f;
    [SerializeField] private int pointsOnKill = 2;

    private BatSwarm swarm;
    private Vector3 target;
    private Vector3 velocity;
    private float cruiseSpeed;
    private bool swooping;
    private bool swoopPassed;
    private bool dead;
    private float nextAvoidCheck;
    private float bank;
    private Vector3 fallVelocity;
    private AudioSource audioSource;

    public bool IsSwooping => swooping;
    public bool IsDead => dead;

    public void Init(BatSwarm owner)
    {
        swarm = owner;
        cruiseSpeed = Random.Range(minSpeed, maxSpeed);
        velocity = transform.forward * cruiseSpeed;
        audioSource = GetComponent<AudioSource>();

        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.speed = Random.Range(0.9f, 1.3f);
            // Different start frame per bat so wings do not flap in sync.
            anim.Update(Random.Range(0f, 1f));
        }

        PickNewTarget();
    }

    public void StartSwoop()
    {
        if (dead) return;
        swooping = true;
        swoopPassed = false;
        if (audioSource != null && audioSource.clip != null)
            audioSource.Play();
    }

    void PickNewTarget()
    {
        target = swarm != null ? swarm.RandomFlightPoint() : transform.position + Random.insideUnitSphere * 10f;
    }

    void Update()
    {
        if (dead)
        {
            Fall();
            return;
        }

        Vector3 desired = GetSteerTarget() - transform.position;
        float speed = swooping ? swoopSpeed : cruiseSpeed;

        if (Time.time >= nextAvoidCheck)
        {
            nextAvoidCheck = Time.time + 0.25f;
            // Look ahead and turn away from buildings and trees.
            if (Physics.SphereCast(transform.position, 0.3f, velocity.normalized, out RaycastHit hit,
                    speed * 0.8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                desired = Vector3.Reflect(velocity, hit.normal) + Vector3.up * speed;
                if (!swooping) PickNewTarget();
            }
        }

        Vector3 desiredVel = desired.normalized * speed;
        Vector3 newVel = Vector3.Slerp(velocity, desiredVel, turnRate * (swooping ? 1.6f : 1f) * Time.deltaTime);

        // Bank into turns like a real bat.
        float turn = Vector3.SignedAngle(Flat(velocity), Flat(newVel), Vector3.up) / Mathf.Max(Time.deltaTime, 0.0001f);
        bank = Mathf.Lerp(bank, Mathf.Clamp(-turn * 0.25f, -maxBankAngle, maxBankAngle), 4f * Time.deltaTime);

        velocity = newVel;
        transform.position += velocity * Time.deltaTime;
        if (velocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(velocity) * Quaternion.Euler(0f, 0f, bank);

        UpdateTargetReached();
    }

    Vector3 GetSteerTarget()
    {
        if (swooping && !swoopPassed && swarm != null && swarm.Player != null)
            return swarm.Player.position + swarm.Player.right * 0.6f + Vector3.up * 0.3f;
        return target;
    }

    void UpdateTargetReached()
    {
        if (swooping && swarm != null && swarm.Player != null)
        {
            if (!swoopPassed && (transform.position - swarm.Player.position).sqrMagnitude < 2.5f)
            {
                // Passed the player's head: fly off upward to a far point.
                swoopPassed = true;
                target = swarm.RandomFlightPoint();
                // Later waves: swooping bats peck the player.
                if (EnemySpawner.Difficulty >= 0.35f && swarm.PlayerHealth != null)
                {
                    swarm.PlayerHealth.TakeDamage(5f);
                    GameFx.HapticBoth(0.5f, 0.1f);
                }
            }
            else if (swoopPassed && (transform.position - target).sqrMagnitude < 9f)
            {
                swooping = false;
                PickNewTarget();
            }
            return;
        }

        if ((transform.position - target).sqrMagnitude < 4f)
            PickNewTarget();
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    /// <summary>Called by Bullet when a shot hits this bat.</summary>
    public void Hit()
    {
        if (dead) return;
        dead = true;
        swooping = false;
        fallVelocity = velocity * 0.3f;

        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null) anim.speed = 0f;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (UIManager.Instance != null)
            UIManager.Instance.EnemyDie(pointsOnKill);

        if (swarm != null)
            swarm.OnBatKilled(this);

        Destroy(gameObject, 3f);
    }

    void Fall()
    {
        fallVelocity += Physics.gravity * Time.deltaTime;
        transform.position += fallVelocity * Time.deltaTime;
        transform.Rotate(360f * Time.deltaTime, 0f, 180f * Time.deltaTime, Space.Self);
    }
}
