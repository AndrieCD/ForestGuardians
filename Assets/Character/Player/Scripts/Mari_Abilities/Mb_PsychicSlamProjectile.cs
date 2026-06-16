using UnityEngine;
using System.Collections;


// =============================================================================
// Mb_PsychicSlamProjectile.cs
// Attached to the shockwave prefab. Handles self-propulsion, trigger detection,
// per-enemy damage + knockback, visual scaling, and self-destruction.
//
// Kept as a separate class so Unity can serialize it as a prefab component
// and so Leo can reference it directly in the Editor.
// =============================================================================

public class Mb_PsychicSlamProjectile : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Runtime State (set by Initialize())
    // -------------------------------------------------------------------------

    private Mb_CharacterBase _owner;
    private float _damage;
    private float _knockbackForce;
    private float _knockbackDuration;
    private bool _isOvercharged;

    // World-space origin of the projectile — used to calculate knockback direction
    // per enemy so each one is pushed away from where the shockwave started
    private Vector3 _originPosition;

    // Travel config — read from SO_ProjectileData equivalent fields.
    // For Psychic Slam these are hardcoded here since it doesn't use
    // SO_ProjectileData (it's not a standard projectile from the launcher).
    // TODO: If a generic non-launcher projectile SO is added later, read from there.
    [Header("Travel")]
    [Tooltip("How fast the shockwave moves forward in world units per second.")]
    [SerializeField] private float _TravelSpeed = 14f;

    [Tooltip("How far the shockwave travels before deactivating.")]
    [SerializeField] private float _MaxRange = 12f;

    // Distance traveled so far — compared against MaxRange each Update
    private float _distanceTraveled = 0f;

    // Prevents hitting the same enemy twice in the same shockwave pass
    private System.Collections.Generic.HashSet<MB_CuBotBase> _hitEnemies
        = new System.Collections.Generic.HashSet<MB_CuBotBase>();

    // Cached collider — resized in Initialize() to match final shockwave dimensions
    private BoxCollider _collider;

    // Visual mesh root — scaled to match collider in Initialize()
    // TODO: Leo — name the shockwave mesh child GO "ShockwaveVisual" on the prefab
    private Transform _visualRoot;

    // Looping travel VFX — plays while the shockwave is in flight
    private ParticleSystem _shockwaveVFX;

    private bool _isInitialized = false;


    // -------------------------------------------------------------------------
    // Initialize
    // Called by Mari_E immediately after Instantiate()
    // -------------------------------------------------------------------------

    public void Initialize(
        Mb_CharacterBase owner,
        float damage,
        float knockbackForce,
        float knockbackDuration,
        float width,
        float height,
        float depth,
        bool isOvercharged)
    {
        _owner = owner;
        _damage = damage;
        _knockbackForce = knockbackForce;
        _knockbackDuration = knockbackDuration;
        _isOvercharged = isOvercharged;

        // Store world-space origin so knockback directions are consistent
        // even as the GO moves forward each frame
        _originPosition = transform.position;

        // --- Resize BoxCollider ---
        _collider = GetComponent<BoxCollider>();
        if (_collider != null)
        {
            _collider.size = new Vector3(width, height, depth);
            _collider.center = Vector3.zero; // Centered on the GO
        }
        else
        {
            Debug.LogError("[Mb_PsychicSlamProjectile] No BoxCollider on shockwave prefab.");
        }

        // --- Scale visual root ---
        // ShockwaveVisual is a flat mesh child — scale X and Y to match
        // collider width and height. Depth (Z) stays at 1 — the mesh is thin.
        _visualRoot = transform.Find("ShockwaveVisual");
        if (_visualRoot != null)
            _visualRoot.localScale = new Vector3(width, height, 1f);

        // --- Locate and play travel VFX ---
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject.name == "ShockwaveVFX")
            {
                _shockwaveVFX = ps;
                break;
            }
        }
        _shockwaveVFX?.Play();

        // --- Overcharge visual intensity ---
        // TODO: Tint the ShockwaveVisual material or increase VFX emission rate
        // when overcharged so the player can see the empowered version.
        // Suggested: overcharged shockwave glows brighter / emits more particles.
        if (isOvercharged && _shockwaveVFX != null)
        {
            var emission = _shockwaveVFX.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(
                emission.rateOverTime.constant * 2f
            );
        }

        _isInitialized = true;
    }


    // -------------------------------------------------------------------------
    // Movement
    // -------------------------------------------------------------------------

    private void Update()
    {
        if (!_isInitialized) return;

        // Move forward in local space — the GO's forward matches Mari's facing
        // at spawn time (inherited from spawnRot in Mari_E.Activate)
        float step = _TravelSpeed * Time.deltaTime;
        transform.position += transform.forward * step;
        _distanceTraveled += step;

        // Deactivate after traveling MaxRange units
        if (_distanceTraveled >= _MaxRange)
            Deactivate();
    }


    // -------------------------------------------------------------------------
    // Trigger Detection
    // -------------------------------------------------------------------------

    private void OnTriggerEnter(Collider other)
    {
        if (!_isInitialized) return;

        // Ignore the owner — the shockwave should never hit Mari herself
        if (_owner != null && other.gameObject == _owner.gameObject) return;

        MB_CuBotBase enemy = other.GetComponent<MB_CuBotBase>();
        if (enemy == null) return;
        if (enemy.Health == null || enemy.Health.IsDead) return;

        // Each enemy is hit exactly once per shockwave pass
        if (_hitEnemies.Contains(enemy)) return;
        _hitEnemies.Add(enemy);

        ApplyDamageAndKnockback(enemy);
    }


    // -------------------------------------------------------------------------
    // Damage and Knockback
    // -------------------------------------------------------------------------

    private void ApplyDamageAndKnockback(MB_CuBotBase enemy)
    {
        // --- Damage ---
        enemy.Health.TakeDamage(_damage);

        // --- Knockback direction ---
        // Push the enemy away from the projectile's world-space ORIGIN,
        // not from its current position — this makes every enemy in the
        // shockwave's path feel pushed back from the same source point.
        Vector3 pushDir = (enemy.transform.position - _originPosition);
        pushDir.y = 0f; // Keep knockback horizontal — no vertical launch
        pushDir.Normalize();

        // Fallback if the enemy is exactly at the origin (extremely unlikely)
        if (pushDir == Vector3.zero)
            pushDir = transform.forward;

        Vector3 knockbackVelocity = pushDir * _knockbackForce;

        // --- Apply knockback ---
        // Guardian path — uses established displacement API
        Mb_Movement movement = enemy.GetComponent<Mb_Movement>();
        if (movement != null)
        {
            movement.ApplyDisplacement(knockbackVelocity, _knockbackDuration);
            return;
        }

        // CuBot path — disable NavMeshAgent and push via CharacterController
        // Same pattern as Sc_HitEffect_Knockback for consistency
        UnityEngine.AI.NavMeshAgent agent =
            enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        CharacterController cc =
            enemy.GetComponent<CharacterController>();

        if (agent != null)
        {
            agent.enabled = false;
            StartCoroutine(ApplyCuBotKnockback(enemy, agent, cc,
                                               knockbackVelocity,
                                               _knockbackDuration));
        }

        Debug.Log($"[Mb_PsychicSlamProjectile] Hit {enemy.CharacterName} " +
                  $"for {_damage} and knocked back.");
    }


    // -------------------------------------------------------------------------
    // CuBot Knockback Coroutine
    // -------------------------------------------------------------------------

    // Moves the CuBot manually during the knockback window, then re-enables
    // the NavMeshAgent. Mirrors Sc_HitEffect_Knockback.ApplyCuBotKnockback()
    // exactly so knockback feel is consistent across the codebase.
    private System.Collections.IEnumerator ApplyCuBotKnockback(
        MB_CuBotBase target,
        UnityEngine.AI.NavMeshAgent agent,
        CharacterController cc,
        Vector3 velocity,
        float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            if (cc != null)
                cc.Move(velocity * Time.deltaTime);
            else
                target.transform.position += velocity * Time.deltaTime;

            yield return null;
        }

        // Re-enable only if the CuBot is still alive and active
        if (agent != null && target.gameObject.activeInHierarchy)
            agent.enabled = true;
    }


    // -------------------------------------------------------------------------
    // Deactivation
    // -------------------------------------------------------------------------

    private void Deactivate()
    {
        _isInitialized = false;

        // Stop travel VFX — let particles fade naturally
        if (_shockwaveVFX != null)
            _shockwaveVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        // Disable collider immediately so no more hits register during fade
        if (_collider != null)
            _collider.enabled = false;

        // Destroy after a short window for VFX to finish fading
        // TODO: Tune delay to match VFX fade duration Leo sets on the prefab
        Destroy(gameObject, 1f);
    }
}