using UnityEngine;
using UnityEngine.AI;

public class DemogorgonAI : MonoBehaviour
{
    public static DemogorgonAI Instance;
    [SerializeField] private Transform player;
    
    private float attackRange = 10f;
    private float attackCooldown = 2f;
    private float damage = 25f;
    public float distance;
    
    
    PlayerHealth playerHealth;
    NavMeshAgent agent;
    Animator anim;
    float lastAttack;


    void Awake()
    {
        Instance = this;
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
    }

    void Update()
    {
        distance = Vector3.Distance(transform.position, player.position);
        Debug.Log("Distance: " + distance);
        agent.isStopped = false;
        agent.SetDestination(player.position);
        if (anim != null) 
        { 
            anim.SetBool("Walk", true);
            Debug.Log("Distance: " + distance);
        }
        if (distance <= attackRange)
        { 
            agent.isStopped = true;
            TryAttack();
        }
    }

    void TryAttack()
    {
        Debug.Log("TryAttack called");

        if (Time.time - lastAttack < attackCooldown) return;

        lastAttack = Time.time;

        if (anim != null)
            anim.SetTrigger("Attack");

        if (playerHealth != null)
            playerHealth.TakeDamage(damage);

        Debug.Log("Demogorgon attacked player");
    }
}