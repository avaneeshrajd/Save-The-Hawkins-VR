using UnityEngine;
using UnityEngine.AI;

public class DemogorgonAI : MonoBehaviour
{
    public Transform player;

    public float chaseRange = 50f;
    public float attackRange = 20f;
    
    public float attackCooldown = 2f;
    public float damage = 5f;
    
    
    PlayerHealth playerHealth;

    NavMeshAgent agent;
    Animator anim;
    float lastAttack;

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
        float distance = Vector3.Distance(transform.position, player.position);
        Debug.Log("Distance: " + distance);


        if (distance <= chaseRange)
        {
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
        else
        {
            agent.isStopped = true;

            if (anim != null)
            {
                anim.SetBool("Walk", false);
            }
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