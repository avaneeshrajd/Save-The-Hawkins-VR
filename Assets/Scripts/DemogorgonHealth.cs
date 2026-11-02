using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;

public class DemogorgonHealth : MonoBehaviour
{
    [SerializeField] private float currentHealth;
    [SerializeField] private Image healthFill;
    [SerializeField] private TextMeshProUGUI scoreText;

    [SerializeField] private int deathScore = 10;
    [SerializeField] private float maxHealth = 100f;
    [Tooltip("Chance (0-1) to drop a pickup on death.")]
    [SerializeField] private float dropChance = 0.2f;

    /// <summary>Optional damage modifier (boss weak-spot window).</summary>
    public Func<float, float> DamageModifier;
    public event Action<DemogorgonHealth> Died;
    public bool IsDead { get; private set; }
    public float HealthPercent => currentHealth / maxHealth;
    public bool CanStagger { get; set; } = true;
    /// <summary>Dives into a portal once when badly hurt, then ambushes from behind.</summary>
    public bool CanAmbush { get; set; }
    bool ambushUsed, ambushing;

    private Color baseTint = Color.white;
    private Coroutine flashRoutine;
    private bool started;

    void Start()
    {
        currentHealth = maxHealth;
        started = true;
    }

    /// <summary>Configures this enemy before it is activated (variants, boss).</summary>
    public void Configure(float healthMultiplier, int score, float drop)
    {
        maxHealth *= healthMultiplier;
        currentHealth = maxHealth;
        deathScore = score;
        dropChance = drop;
    }

    public void SetBaseTint(Color tint, float emission = 0f)
    {
        baseTint = tint;
        GameFx.SetTint(gameObject, tint, emission);
    }

    public void TakeDamage(float dmg)
    {
        if (IsDead) return;
        if (!started) { currentHealth = maxHealth; started = true; }
        if (DamageModifier != null) dmg = DamageModifier(dmg);

        currentHealth -= dmg;
        UpdateHealthUI();

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(HitFlash());

        if (currentHealth <= 0) { Die(); return; }

        if (CanStagger && !ambushing && TryGetComponent(out DemogorgonAI ai))
        {
            // Heavy hits (sword, explosion) play the hit reaction; bullets just stagger.
            if (dmg >= 45f && ai.PlayAnim("Hit", 0.05f)) ai.Stun(0.7f);
            else ai.Stun(0.15f);

            if (CanAmbush && !ambushUsed && currentHealth < maxHealth * 0.6f && !ai.Suspended)
            {
                ambushUsed = true;
                StartCoroutine(AmbushRoutine(ai));
            }
        }
    }

    // Sinks into the ground through a portal, then climbs out somewhere behind the player.
    IEnumerator AmbushRoutine(DemogorgonAI ai)
    {
        ambushing = true;
        ai.Suspended = true;
        var agent = ai.Agent;
        if (agent != null && agent.enabled) { agent.isStopped = true; agent.updatePosition = false; }
        var spawner = EnemySpawner.Instance;
        if (spawner != null) spawner.OpenPortal(transform.position, 1.2f);
        ai.PlayAnim("PortalEnter", 0.1f);

        Vector3 start = transform.position;
        float height = transform.lossyScale.y * 95f;
        for (float t = 0f; t < 1.6f && !IsDead; t += Time.deltaTime)
        {
            transform.position = start + Vector3.down * height * Mathf.Clamp01((t - 0.5f) / 1.1f);
            yield return null;
        }
        if (IsDead) yield break;

        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        if (healthFill != null) healthFill.canvas.enabled = false;
        yield return new WaitForSeconds(2f);
        if (IsDead) yield break;

        Vector3 target = start;
        if (spawner != null && spawner.FindAmbushPoint(out Vector3 p)) target = p;
        if (agent != null && agent.enabled) { agent.Warp(target); agent.updatePosition = true; }
        transform.position = target;
        if (healthFill != null) healthFill.canvas.enabled = true;
        Vector3 look = ai.Player != null ? ai.Player.position - target : transform.forward;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);

        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = true;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = true;
        if (spawner != null) spawner.OpenPortal(target, 1.2f);
        GameHUD.Radio("It went into the ground... it's coming up behind you!");
        ambushing = false;
        ai.Emerge();
    }

    IEnumerator HitFlash()
    {
        GameFx.SetTint(gameObject, new Color(1f, 0.25f, 0.2f), 2.5f);
        yield return new WaitForSeconds(0.08f);
        GameFx.SetTint(gameObject, baseTint);
        flashRoutine = null;
    }

    void UpdateHealthUI()
    {
        if (healthFill != null)
            healthFill.fillAmount = Mathf.Max(0f, currentHealth / maxHealth);
    }

    void Die()
    {
        IsDead = true;
        ScoreSystem.AddKill(deathScore);
        Pickup.TryDrop(transform.position + Vector3.up * 1.2f, dropChance);
        Died?.Invoke(this);
        StartCoroutine(DeathRoutine());
    }

    // Procedural death: stops, topples over, sinks into the ground, then is removed.
    IEnumerator DeathRoutine()
    {
        if (TryGetComponent(out DemogorgonAI ai)) { ai.Suspended = true; ai.enabled = false; }
        if (TryGetComponent(out NavMeshAgent agent)) agent.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        if (healthFill != null) healthFill.canvas.gameObject.SetActive(false);
        Animator anim = GetComponent<Animator>();

        if (ai != null && ai.PlayAnim("Death", 0.1f))
        {
            yield return new WaitForSeconds(2.6f); // death clip ~80 frames
        }
        else
        {
        Quaternion start = transform.rotation;
        Quaternion fallen = start * Quaternion.Euler(0f, 0f, UnityEngine.Random.value < 0.5f ? 85f : -85f);
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            transform.rotation = Quaternion.Slerp(start, fallen, t * t / 0.36f);
            yield return null;
        }
        if (anim != null) anim.speed = 0f;
        yield return new WaitForSeconds(0.8f);
        }

        Vector3 p = transform.position;
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            transform.position = p + Vector3.down * (t / 1.5f) * 1.5f * transform.lossyScale.y / 0.028f;
            yield return null;
        }
        Destroy(gameObject);
    }
}
