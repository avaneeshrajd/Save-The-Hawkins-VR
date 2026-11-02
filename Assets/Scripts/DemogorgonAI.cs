using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DemogorgonAI : MonoBehaviour
{
    public static DemogorgonAI Instance;
    [SerializeField] private Transform player;

    [Header("Movement")]
    [Tooltip("Used when the walk clip has no root motion to measure stride speed from.")]
    [SerializeField] private float fallbackMoveSpeed = 3f;
    [Tooltip("Playback speed of the walk/run clip. Agent speed is scaled with it so feet do not slide.")]
    [SerializeField] private float walkAnimPlaybackSpeed = 1f;
    [SerializeField] private float minMoveSpeed = 1.8f;
    [SerializeField] private float maxMoveSpeed = 4.5f;
    [SerializeField] private float turnSpeed = 8f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 2.5f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float damage = 20f;

    public float distance;

    PlayerHealth playerHealth;
    NavMeshAgent agent;
    Animator anim;
    float lastAttack;
    float moveSpeed;
    float speedMultiplier = 1f;

    /// <summary>Number of Demogorgons alive in the scene (used by EnemySpawner to cap count).</summary>
    public static int AliveCount { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    /// <summary>All active Demogorgons (for proximity heartbeat, flicker, explosions).</summary>
    public static readonly List<DemogorgonAI> All = new List<DemogorgonAI>();
    /// <summary>Attack sound, set by DemoHuntDirector.</summary>
    public static AudioClip AttackClip;

    public float DamageMultiplier { get; set; } = 1f;
    /// <summary>True while another script (pounce, death, boss roar) controls this Demogorgon.</summary>
    public bool Suspended { get; set; }
    public Transform Player => player;
    public NavMeshAgent Agent => agent;
    float stunUntil;
    /// <summary>Runner variant: use the hunting run cycle instead of the normal run.</summary>
    public bool UseHuntRun { get; set; }

    /// <summary>Cross-fades to an animator state if it exists (PortalExit, Hit, Stun, Death...).</summary>
    public bool PlayAnim(string state, float fade = 0.15f)
    {
        if (anim == null) anim = GetComponent<Animator>();
        if (anim == null || !anim.HasState(0, Animator.StringToHash(state))) return false;
        anim.speed = 1f;
        anim.CrossFadeInFixedTime(state, fade);
        return true;
    }

    /// <summary>Climbs out of an Upside Down portal before hunting.</summary>
    public void Emerge() => StartCoroutine(EmergeRoutine());

    IEnumerator EmergeRoutine()
    {
        Suspended = true;
        yield return null; // wait for Start
        if (PlayAnim("PortalExit", 0f)) yield return new WaitForSeconds(2f);
        Suspended = false;
    }

    void ApplyHuntRun()
    {
        if (anim == null || anim.runtimeAnimatorController == null) return;
        AnimationClip run = null, hunt = null;
        foreach (var c in anim.runtimeAnimatorController.animationClips)
        {
            if (c.name.EndsWith("|Run")) run = c;
            else if (c.name.EndsWith("|RunHunt")) hunt = c;
        }
        if (run == null || hunt == null) return;
        var ov = new AnimatorOverrideController(anim.runtimeAnimatorController);
        ov[run] = hunt;
        anim.runtimeAnimatorController = ov;
    }

    void OnEnable() { AliveCount++; All.Add(this); }
    void OnDisable() { AliveCount--; All.Remove(this); }

    /// <summary>Stops movement briefly (hit stagger).</summary>
    public void Stun(float seconds) { stunUntil = Mathf.Max(stunUntil, Time.time + seconds); }

    /// <summary>Distance to the nearest active Demogorgon from a point (horizontal).</summary>
    public static float NearestDistance(Vector3 from)
    {
        float best = float.MaxValue;
        foreach (DemogorgonAI d in All)
        {
            if (d == null) continue;
            Vector3 v = d.transform.position - from; v.y = 0f;
            best = Mathf.Min(best, v.magnitude);
        }
        return best;
    }

    /// <summary>EnemySpawner raises this over time so later Demogorgons hunt faster.</summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0.1f, multiplier);
        if (agent != null)
            agent.speed = moveSpeed * speedMultiplier;
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();

        if (player == null)
            player = Camera.main.transform;

        playerHealth = player.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null)
            Debug.LogError("PlayerHealth script not found on player!");

        if (anim != null)
            anim.applyRootMotion = false; // NavMeshAgent owns movement
        if (UseHuntRun) ApplyHuntRun();

        moveSpeed = ComputeMoveSpeedFromAnimation();
        agent.speed = moveSpeed * speedMultiplier;
        agent.stoppingDistance = attackRange * 0.8f;
    }

    // Match agent speed to the walk clip's stride so the animation and movement agree.
    float ComputeMoveSpeedFromAnimation()
    {
        float speed = fallbackMoveSpeed;

        if (anim != null && anim.runtimeAnimatorController != null)
        {
            AnimationClip walkClip = null;
            foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
            {
                if (clip.name.EndsWith("|Run")) { walkClip = clip; break; }
            }

            if (walkClip != null)
            {
                float clipSpeed = new Vector3(walkClip.averageSpeed.x, 0f, walkClip.averageSpeed.z).magnitude;
                float worldSpeed = clipSpeed * transform.lossyScale.y * walkAnimPlaybackSpeed;
                if (worldSpeed > 0.05f)
                    speed = worldSpeed;
            }
        }

        return Mathf.Clamp(speed, minMoveSpeed, maxMoveSpeed);
    }

    void Update()
    {
        if (Suspended || agent == null || !agent.enabled) return;
        if (Time.time < stunUntil)
        {
            agent.isStopped = true;
            return;
        }

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f; // camera height should not count toward attack distance
        distance = toPlayer.magnitude;

        bool inRange = distance <= attackRange;

        agent.isStopped = inRange;
        if (!inRange)
            agent.SetDestination(player.position);

        if (anim != null)
        {
            anim.SetBool("Walk", !inRange);
            anim.speed = inRange ? 1f : walkAnimPlaybackSpeed * speedMultiplier;
        }

        if (inRange)
        {
            FacePlayer(toPlayer);
            TryAttack();
        }
    }

    void FacePlayer(Vector3 toPlayer)
    {
        if (toPlayer.sqrMagnitude < 0.001f) return;
        Quaternion look = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    void TryAttack()
    {
        if (Time.time - lastAttack < attackCooldown) return;

        lastAttack = Time.time;

        // Mix of attacks: claw swipe or the big swing.
        if (Random.value < 0.35f && PlayAnim("Swing", 0.1f)) { }
        else if (anim != null)
            anim.SetTrigger("Attack");

        if (AttackClip != null)
            GameFx.Play3D(AttackClip, transform.position + Vector3.up, 0.9f, Random.Range(0.9f, 1.1f));
        GameFx.HapticBoth(0.9f, 0.25f);

        if (playerHealth != null)
            playerHealth.TakeDamage(damage * DamageMultiplier);
    }
}
