using System.Collections;
using TMPro;
using UnityEngine;

public enum DemogorgonType { Normal, Runner, Brute, Pouncer, Boss }

/// <summary>Turns a plain Demogorgon clone into a variant. Call before SetActive(true).</summary>
public static class EnemyVariants
{
    public static void Apply(GameObject demo, DemogorgonType type, float speedMul)
    {
        var health = demo.GetComponent<DemogorgonHealth>();
        var ai = demo.GetComponent<DemogorgonAI>();
        float scale = 1f, hp = 1f, speed = 1f, dmg = 1f, drop = 0.2f;
        int score = 10;
        Color tint = Color.white;

        switch (type)
        {
            case DemogorgonType.Runner:  scale = 0.75f; hp = 0.5f; speed = 1.6f; dmg = 0.6f; score = 10; tint = new Color(0.75f, 0.9f, 0.75f); break;
            case DemogorgonType.Brute:   scale = 1.35f; hp = 3f;   speed = 0.7f; dmg = 1.8f; score = 25; drop = 0.6f; tint = new Color(0.9f, 0.55f, 0.5f); break;
            case DemogorgonType.Pouncer: scale = 0.9f;  hp = 1f;   speed = 1.1f; dmg = 1.2f; score = 15; drop = 0.3f; tint = new Color(0.7f, 0.6f, 1f); break;
            case DemogorgonType.Boss:    scale = 2.2f;  hp = 25f;  speed = 0.85f; dmg = 2.5f; score = 200; drop = 1f; break;
        }

        demo.transform.localScale *= scale;
        if (health != null)
        {
            health.Configure(hp, score, drop);
            health.SetBaseTint(tint);
        }
        if (ai != null)
        {
            ai.SetSpeedMultiplier(speedMul * speed);
            ai.DamageMultiplier = dmg;
        }
        if (ai != null) ai.UseHuntRun = type == DemogorgonType.Runner;
        if (health != null) health.CanAmbush = (type == DemogorgonType.Normal || type == DemogorgonType.Runner) && Random.value < 0.4f;
        if (type == DemogorgonType.Pouncer) demo.AddComponent<PounceBehaviour>();
        if (type == DemogorgonType.Boss)
        {
            demo.AddComponent<BossDemogorgon>();
            // Boss: big jump slam from range.
            demo.AddComponent<PounceBehaviour>().Configure(8f, 18f, 12f, 3f, 1.1f, "Jump", true);
        }
    }
}

/// <summary>Leaps at the player from mid range. Can be shot mid-air.</summary>
public class PounceBehaviour : MonoBehaviour
{
    float MinRange = 4f, MaxRange = 7.5f, Cooldown = 5f, Height = 1.3f, Duration = 0.6f;
    string leapAnim = "Pounce";
    bool slam;
    DemogorgonAI ai;

    public void Configure(float minRange, float maxRange, float cooldown, float height, float duration, string anim, bool groundSlam)
    {
        MinRange = minRange; MaxRange = maxRange; Cooldown = cooldown; Height = height; Duration = duration;
        leapAnim = anim; slam = groundSlam;
    }
    float nextPounce;

    bool forced;
    void Start() { ai = GetComponent<DemogorgonAI>(); if (!forced) nextPounce = Time.time + 2f; }

    /// <summary>Jump-scare: leap at the player almost immediately, ignoring the minimum range.</summary>
    public void LeapSoon(float delay)
    {
        forced = true;
        nextPounce = Time.time + delay;
        MinRange = 2.5f;
    }

    void Update()
    {
        if (ai == null || ai.Suspended || ai.Player == null || Time.time < nextPounce) return;
        Vector3 to = ai.Player.position - transform.position; to.y = 0f;
        float d = to.magnitude;
        if (d > MinRange && d < MaxRange) StartCoroutine(Pounce(to.normalized, d));
    }

    IEnumerator Pounce(Vector3 dir, float dist)
    {
        nextPounce = Time.time + Cooldown;
        ai.Suspended = true;
        var agent = ai.Agent;
        agent.isStopped = true;
        agent.updatePosition = false;
        if (!ai.PlayAnim(leapAnim, 0.05f) && TryGetComponent(out Animator anim)) anim.SetTrigger("Attack");
        GameFx.Play3D(DemogorgonAI.AttackClip, transform.position, 1f, slam ? 0.7f : 1.3f);

        Vector3 start = transform.position;
        Vector3 end = start + dir * (dist - 1.8f);
        transform.rotation = Quaternion.LookRotation(dir);
        for (float t = 0f; t < Duration; t += Time.deltaTime)
        {
            if (this == null || GetComponent<DemogorgonHealth>().IsDead) yield break;
            float k = t / Duration;
            transform.position = Vector3.Lerp(start, end, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * Height;
            yield return null;
        }
        if (agent.enabled)
        {
            agent.Warp(end);
            agent.updatePosition = true;
        }
        ai.PlayAnim("Land", 0.05f);
        if (slam)
        {
            // Ground slam: shockwave hurts the player if close.
            GameFx.Burst(end + Vector3.up * 0.3f, 20, 5f, 0.8f, new Color(0.5f, 0.1f, 0.1f), Color.black);
            GameFx.Play3D(GameFx.Boom, end, 1f, 0.6f, 60f);
            GameFx.HapticBoth(1f, 0.5f);
            var ph = ai.Player != null ? ai.Player.GetComponentInParent<PlayerHealth>() : null;
            Vector3 d = ai.Player != null ? ai.Player.position - end : Vector3.one * 99f; d.y = 0f;
            if (ph != null && d.magnitude < 4.5f) ph.TakeDamage(15f);
            yield return new WaitForSeconds(0.8f);
        }
        ai.Suspended = false;
    }
}

/// <summary>
/// Boss: roars every few seconds, standing still and glowing red. While glowing it
/// takes triple damage (weak-spot window). Shows a floating boss health bar.
/// </summary>
public class BossDemogorgon : MonoBehaviour
{
    const float RoarInterval = 9f, RoarDuration = 2.6f, WeakMultiplier = 3f;
    DemogorgonAI ai;
    DemogorgonHealth health;
    TextMeshPro bar;
    Light glow;
    bool roaring;
    Transform cam;

    void Start()
    {
        ai = GetComponent<DemogorgonAI>();
        health = GetComponent<DemogorgonHealth>();
        health.CanStagger = false;
        health.DamageModifier = d => roaring ? d * WeakMultiplier : d;
        cam = Camera.main != null ? Camera.main.transform : null;

        bar = GameHUD.CreateText("Boss Bar", 2.2f, new Color(1f, 0.2f, 0.15f), new Vector2(5f, 1.2f));
        glow = GameFx.CreateLight(transform, new Vector3(0f, 60f, 0f), Color.red, 10f, 0f);
        StartCoroutine(RoarLoop());
    }

    void Update()
    {
        if (bar == null) return;
        if (health.IsDead) { Destroy(bar.gameObject); return; }
        bar.transform.position = transform.position + Vector3.up * (transform.lossyScale.y * 100f); // model is ~88 units tall
        GameFx.Billboard(bar.transform, cam);
        int filled = Mathf.CeilToInt(health.HealthPercent * 20f);
        string weak = roaring ? "\n<color=#FFFF55><size=60%>WEAK SPOT EXPOSED - SHOOT!</size></color>" : "";
        bar.text = "THE ALPHA DEMOGORGON\n<size=60%>" + new string('|', filled) + "<color=#444>" + new string('|', 20 - filled) + "</color></size>" + weak;
    }

    float nextRoar;
    /// <summary>Forces the next roar to happen now (trailer).</summary>
    public void RoarNow() => nextRoar = 0f;

    IEnumerator RoarLoop()
    {
        while (!health.IsDead)
        {
            nextRoar = Time.time + RoarInterval;
            while (Time.time < nextRoar) yield return null;
            if (health.IsDead) yield break;
            roaring = true;
            ai.Suspended = true;
            ai.Agent.isStopped = true;
            if (TryGetComponent(out Animator anim)) anim.SetTrigger("Attack");
            GameFx.Play3D(DemogorgonAI.AttackClip, transform.position, 1f, 0.6f, 60f);
            GameFx.HapticBoth(0.5f, 0.8f);
            health.SetBaseTint(new Color(1f, 0.35f, 0.3f), 3f);
            for (float t = 0f; t < RoarDuration && !health.IsDead; t += Time.deltaTime)
            {
                glow.intensity = 4f + Mathf.Sin(t * 20f) * 2f;
                yield return null;
            }
            glow.intensity = 0f;
            health.SetBaseTint(Color.white);
            roaring = false;
            if (!health.IsDead) ai.Suspended = false;
        }
    }

    void OnDestroy() { if (bar != null) Destroy(bar.gameObject); }
}
