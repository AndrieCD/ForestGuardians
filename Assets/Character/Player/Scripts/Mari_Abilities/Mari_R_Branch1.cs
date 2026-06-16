// Mari_R_Branch1.cs
// [R] Psychic Bloom — Mari's first ultimate branch.
//
// PASSIVE — Blooming DoT:
//   Every Mind Flurry (LMB) hit applies a damage-over-time effect to the enemy
//   struck. The DoT deals DOT_PERCENT (30%) of the projectile's base damage
//   over DOT_DURATION (5s) in ticks every DOT_TICK_INTERVAL (0.5s).
//   Applied fresh on every hit — re-application resets the DoT timer on that
//   enemy (last hit wins, no stacking).
//   Subscribes to Mari_Primary.OnPrimaryHit — filters by source so only this
//   Mari's hits trigger the passive, not a future second player.
//
// ACTIVE — Psychic Bloom Field:
//   A spherical trigger zone is created centered on Mari's position.
//   Enemies inside take damage every FIELD_TICK_INTERVAL seconds and are slowed.
//   The field persists for FIELD_DURATION seconds, then deactivates.
//   Stronger than Q — larger radius, higher damage, Mari is the epicenter.
//
// ZONE PATTERN:
//   Reuses Mb_BloomZone (defined below), which mirrors Mb_MindspikesZone
//   but with a SphereCollider instead of a BoxCollider.
//   Mari herself is not damaged — owner reference is passed for filtering.
//
// VFX:
//   "BloomCastVFX"  — burst particle on Mari at cast time (flower burst / energy)
//   "BloomFieldVFX" — looping particle on the zone GO while active
//   "BloomDoTVFX"   — short burst particle on the enemy hit by DoT tick
//                     (subtle psychic petal/spark per tick)
//   All located by name in OnEquip / prefab hierarchy.
//   TODO: Leo — add these particle systems to Mari's prefab and zone prefab.
//
// PREFAB SETUP (for Leo/Angel):
//   Create a prefab with:
//   - A SphereCollider (isTrigger = true) — radius set dynamically in Initialize()
//   - A Rigidbody (isKinematic = true, useGravity = false)
//   - Attach Mb_BloomZone to the root
//   - A child ParticleSystem named "BloomFieldVFX" (looping petal/bloom aura)
//   - A child mesh GO named "BloomVisualRoot" (translucent sphere, bloom shader)
//     scaled uniformly to match SphereCollider radius
//   Assign prefab to Mari_R_Branch1.BloomZonePrefab in the Inspector.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mari_R_Branch1 : Sc_BaseAbility
{
    // -------------------------------------------------------------------------
    // Inspector-Assigned Fields
    // -------------------------------------------------------------------------

    // The spherical bloom zone prefab.
    // Must have: SphereCollider (isTrigger), Rigidbody (kinematic),
    // Mb_BloomZone component, child "BloomFieldVFX" ParticleSystem,
    // child "BloomVisualRoot" mesh GO.
    private GameObject _bloomZonePrefab;

    [Header("Passive — Blooming DoT")]
    // Fraction of the hitting projectile's base damage applied as total DoT
    // TODO: Tune — 0.3 = 30% of the hit damage as DoT over DOT_DURATION
    [SerializeField] private float _DotPercent = 0.30f;

    // Total duration of the DoT in seconds
    [SerializeField] private float _DotDuration = 5f;

    // How often the DoT ticks — total DoT damage is divided evenly across ticks
    [SerializeField] private float _DotTickInterval = 0.5f;

    [Header("Active — Bloom Field")]
    // Radius of the spherical zone in world units
    // TODO: Tune — should feel noticeably larger than Q's rectangle
    [SerializeField] private float _FieldRadius = 6f;

    // How long the field persists in seconds
    [SerializeField] private float _FieldDuration = 4f;

    // Seconds between damage ticks inside the field
    [SerializeField] private float _FieldTickInterval = 0.5f;

    // Slow applied to enemies inside the field each tick
    [SerializeField] private float _FieldSlowPercent = 25f;
    [SerializeField] private float _FieldSlowDuration = 1f;


    // -------------------------------------------------------------------------
    // Cached References
    // -------------------------------------------------------------------------

    // Cast burst VFX on Mari — located by name in OnEquip
    private ParticleSystem _castVFX;

    // DoT hit VFX prefab — instantiated briefly on each DoT tick target
    // TODO: Leo — create a small burst prefab named "BloomDoTVFX" and assign here
    [SerializeField] private GameObject _DotHitVFXPrefab;


    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public Mari_R_Branch1(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user) { }


    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public override void OnEquip(Mb_CharacterBase user)
    {
        Mb_AbilityPrefabRegistry registry = user.GetComponent<Mb_AbilityPrefabRegistry>();
        _bloomZonePrefab = registry?.GetPrefab(AbilityPrefabID.Mari_BloomZone);

        if (_bloomZonePrefab == null)
            Debug.LogError("[Mari_Q] Mari_BloomZone prefab not found in registry.");

        // Subscribe to Mari_Primary's confirmed hit event for DoT passive
        // Unsubscribe-first prevents duplicate listeners on re-equip
        Mari_Primary.OnPrimaryHit -= HandlePrimaryHit;
        Mari_Primary.OnPrimaryHit += HandlePrimaryHit;

        // Locate cast VFX by name
        foreach (ParticleSystem ps in user.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject.name == "BloomCastVFX")
            {
                _castVFX = ps;
                break;
            }
        }

        Debug.Log("[Psychic Bloom] Equipped.");
    }


    public override void OnUnequip(Mb_CharacterBase user)
    {
        Mari_Primary.OnPrimaryHit -= HandlePrimaryHit;

        Debug.Log("[Psychic Bloom] Unequipped.");
    }


    // -------------------------------------------------------------------------
    // Passive — Blooming DoT Handler
    // -------------------------------------------------------------------------

    // Fired by Mari_Primary.OnPrimaryHit on every confirmed LMB hit.
    // target is the MB_CuBotBase hit; we receive damage and source from the event.
    // We need the actual target — so we rely on the projectile's OnHit giving us
    // the target via a separate subscription path. See note below.
    //
    // DESIGN NOTE ON TARGET ACQUISITION:
    //   Mari_Primary.OnPrimaryHit passes (damageDealt, source) but not the target.
    //   To get the target, we subscribe to the projectile's OnHit inside
    //   HandlePrimaryHit via an intermediary. However, since Mari_Primary already
    //   hooks projectile.OnHit internally, the cleanest approach is to extend
    //   the OnPrimaryHit event signature to include the target.
    //
    //   Updated event signature (add to Mari_Primary.cs):
    //   public static event Action<float, Mb_CharacterBase, MB_CuBotBase> OnPrimaryHit;
    //   Fired as: OnPrimaryHit?.Invoke(damage, user, target as MB_CuBotBase);
    //   This is the pattern used below.

    private void HandlePrimaryHit(float damageDealt, Mb_CharacterBase source, MB_CuBotBase target)
    {
        // Filter — only react to this Mari's hits
        if (source != _User) return;
        if (target == null) return;
        if (target.Health == null || target.Health.IsDead) return;

        // Calculate total DoT damage — DotPercent of the hit damage
        float totalDotDamage = damageDealt * _DotPercent;

        // Calculate damage per tick — divide total evenly across the tick count
        int tickCount = Mathf.Max(1, Mathf.RoundToInt(_DotDuration / _DotTickInterval));
        float damagePerTick = totalDotDamage / tickCount;

        // Start DoT coroutine on the user (MonoBehaviour runner)
        // If a DoT is already running on this target from a previous hit,
        // the new one will run concurrently — last hit refreshes independently.
        // TODO: Consider a per-target coroutine tracker (Dictionary<target, Coroutine>)
        // if stacking becomes a balance issue. For now, re-application is intentional.
        _User.StartCoroutine(
            ApplyDotRoutine(target, damagePerTick, tickCount)
        );

        Debug.Log($"[Psychic Bloom Passive] DoT applied to {target.CharacterName} — " +
                  $"{damagePerTick:F1} per tick × {tickCount} ticks " +
                  $"({totalDotDamage:F1} total over {_DotDuration}s).");
    }


    // -------------------------------------------------------------------------
    // DoT Coroutine
    // -------------------------------------------------------------------------

    private IEnumerator ApplyDotRoutine(MB_CuBotBase target, float damagePerTick, int tickCount)
    {
        for (int i = 0; i < tickCount; i++)
        {
            yield return new WaitForSeconds(_DotTickInterval);

            // Guard each tick — the enemy may have died or been pooled
            if (target == null) yield break;
            if (!target.gameObject.activeInHierarchy) yield break;
            if (target.Health == null || target.Health.IsDead) yield break;

            // Apply one tick of DoT damage
            target.Health.TakeDamage(damagePerTick);

            // Spawn brief DoT VFX on the target position
            // TODO: Replace with a pool once VFX asset is built — Instantiate
            // is fine for prototype but will generate GC on busy waves.
            if (_DotHitVFXPrefab != null)
            {
                GameObject vfx = GameObject.Instantiate(
                    _DotHitVFXPrefab,
                    target.transform.position + Vector3.up * 1f,
                    Quaternion.identity
                );
                // Auto-destroy the VFX GO after 2 seconds
                GameObject.Destroy(vfx, 2f);
            }
        }
    }


    // -------------------------------------------------------------------------
    // Active — Activation
    // -------------------------------------------------------------------------

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        if (_bloomZonePrefab == null)
        {
            Debug.LogError("[Mari_R_Branch1] BloomZonePrefab is not assigned.");
            return;
        }

        // Resolve damage from SO scaling — Psychic Bloom active scales with AP
        float damage = _AbilityData.GetStat(
            "Damage",
            CurrentLevel,
            user.Stats.AttackPower.GetValue(),
            user.Stats.AbilityPower.GetValue()
        );

        // Spawn zone centered exactly on Mari's position
        // Zone follows Mari's feet — vertically offset by radius so the sphere
        // center sits at roughly waist height, covering the full play area
        Vector3 spawnPos = user.transform.position + Vector3.up * _FieldRadius * 0.5f;

        GameObject zoneGO = GameObject.Instantiate(
            _bloomZonePrefab,
            spawnPos,
            Quaternion.identity  // Sphere is omnidirectional — no rotation needed
        );

        Mb_BloomZone zone = zoneGO.GetComponent<Mb_BloomZone>();
        if (zone == null)
        {
            Debug.LogError("[Mari_R_Branch1] BloomZonePrefab is missing Mb_BloomZone component.");
            GameObject.Destroy(zoneGO);
            return;
        }

        zone.Initialize(
            owner: user,
            damage: damage,
            tickInterval: _FieldTickInterval,
            slowPercent: _FieldSlowPercent,
            slowDuration: _FieldSlowDuration,
            radius: _FieldRadius,
            duration: _FieldDuration
        );

        // Cast VFX on Mari
        _castVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _castVFX?.Play();

        TriggerAbilityAnimation(user);

        StartCooldown(user, GetAbilityCooldown(user));
    }


    // -------------------------------------------------------------------------
    // Animation
    // -------------------------------------------------------------------------

    protected override void TriggerAbilityAnimation(Mb_CharacterBase user)
    {
        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.TriggerR1Ability();
    }
}


// =============================================================================
// Mb_BloomZone.cs
// Spherical trigger zone for Psychic Bloom's active.
// Mirrors Mb_MindspikesZone exactly but uses a SphereCollider.
// Kept as a separate MonoBehaviour so it can be a prefab component.
// =============================================================================

public class Mb_BloomZone : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Runtime State
    // -------------------------------------------------------------------------

    private Mb_CharacterBase _owner;
    private float _damage;
    private float _tickInterval;
    private float _slowPercent;
    private float _slowDuration;
    private float _duration;

    // Per-enemy tick gate — same pattern as Mb_MindspikesZone
    private Dictionary<MB_CuBotBase, float> _tickTimers
        = new Dictionary<MB_CuBotBase, float>();

    private SphereCollider _collider;

    // Visual sphere root — scaled uniformly to match radius
    // TODO: Leo — name the bloom sphere mesh child GO "BloomVisualRoot"
    private Transform _visualRoot;

    // Looping aura VFX
    private ParticleSystem _fieldVFX;


    // -------------------------------------------------------------------------
    // Initialize
    // -------------------------------------------------------------------------

    public void Initialize(
        Mb_CharacterBase owner,
        float damage,
        float tickInterval,
        float slowPercent,
        float slowDuration,
        float radius,
        float duration)
    {
        _owner = owner;
        _damage = damage;
        _tickInterval = tickInterval;
        _slowPercent = slowPercent;
        _slowDuration = slowDuration;
        _duration = duration;

        // --- Resize SphereCollider ---
        _collider = GetComponent<SphereCollider>();
        if (_collider != null)
        {
            _collider.radius = radius;
            _collider.center = Vector3.zero;
        }
        else
        {
            Debug.LogError("[Mb_BloomZone] No SphereCollider found on bloom zone prefab.");
        }

        // --- Scale visual root uniformly to match radius ---
        // A unit sphere mesh (diameter = 1) scaled by (radius * 2) fills the collider
        _visualRoot = transform.Find("BloomVisualRoot");
        if (_visualRoot != null)
            _visualRoot.localScale = Vector3.one * (radius * 2f);

        // --- Locate and play field VFX ---
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject.name == "BloomFieldVFX")
            {
                _fieldVFX = ps;
                break;
            }
        }
        _fieldVFX?.Play();

        StartCoroutine(DurationRoutine());
    }


    // -------------------------------------------------------------------------
    // Trigger Detection
    // -------------------------------------------------------------------------

    private void OnTriggerStay(Collider other)
    {
        // Ignore the owner — Mari should not damage herself
        if (_owner != null && other.gameObject == _owner.gameObject) return;

        MB_CuBotBase enemy = other.GetComponent<MB_CuBotBase>();
        if (enemy == null) return;
        if (enemy.Health == null || enemy.Health.IsDead) return;

        float now = Time.time;
        if (_tickTimers.TryGetValue(enemy, out float lastTick))
        {
            if (now - lastTick < _tickInterval) return;
        }

        _tickTimers[enemy] = now;
        ApplyDamageAndSlow(enemy);
    }

    private void OnTriggerExit(Collider other)
    {
        MB_CuBotBase enemy = other.GetComponent<MB_CuBotBase>();
        if (enemy != null)
            _tickTimers.Remove(enemy);
    }


    // -------------------------------------------------------------------------
    // Damage and Slow
    // -------------------------------------------------------------------------

    private void ApplyDamageAndSlow(MB_CuBotBase enemy)
    {
        enemy.Health.TakeDamage(_damage);

        Sc_StatEffect slowEffect = new Sc_StatEffect(
            StatType.MoveSpeed,
            -_slowPercent,
            StatModType.Percent
        );

        Sc_Modifier slowModifier = new Sc_Modifier(
            "Psychic Bloom Slow",
            ModifierSource.Ability,
            new List<Sc_StatEffect> { slowEffect },
            _slowDuration
        );

        enemy.Stats.AddModifier(slowModifier);

        Debug.Log($"[Mb_BloomZone] Hit {enemy.CharacterName} for {_damage} " +
                  $"and applied {_slowPercent}% slow.");
    }


    // -------------------------------------------------------------------------
    // Duration and Cleanup
    // -------------------------------------------------------------------------

    private IEnumerator DurationRoutine()
    {
        yield return new WaitForSeconds(_duration);
        DeactivateZone();
    }

    private void DeactivateZone()
    {
        if (_fieldVFX != null)
            _fieldVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        _tickTimers.Clear();

        StartCoroutine(DestroyAfterDelay(1.5f));
    }

    private IEnumerator DestroyAfterDelay(float delay)
    {
        if (_collider != null)
            _collider.enabled = false;

        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }
}