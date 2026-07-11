// Mb_Projectile.cs
// A generic, data-driven projectile used by all ranged characters and abilities.
//
// HOW IT WORKS:
//   1. Ability script calls Mb_ProjectileLauncher.Fire() or FireToward()
//   2. The launcher calls Initialize(data, owner) on this component
//   3. OnEnable() launches the Rigidbody forward using SO_ProjectileData.LaunchSpeed
//   4. Update() checks travel distance and deactivates after MaxRange is exceeded
//   5. OnTriggerEnter() handles hits — applies base damage, runs all HitEffects,
//      and fires OnHit so ability passives can react to actual hits (not just fires)
//   6. Deactivate() replaces all Destroy() calls — sets the GameObject inactive
//      so it can be reused without re-instantiation
//
// PIERCING:
//   If SO_ProjectileData.IsPiercing is true, the projectile passes through targets
//   until it has hit MaxPierceTargets enemies. A HashSet<Collider> prevents the same
//   collider from being hit twice in one flight even if the projectile passes slowly.
//
// PAUSE SAFETY:
//   Subscribes to Mb_PauseManager.OnPaused / OnResumed. On pause, the Rigidbody
//   is set kinematic and velocity is stored. On resume, velocity is restored.
//   This is necessary because Time.timeScale = 0 freezes coroutines but does NOT
//   stop a Rigidbody that already has velocity — it must be handled explicitly.
//
// PASSIVE TRIGGER PATTERN:
//   Subscribe to OnHit AFTER calling Fire() to react only to actual hits:
//
//     Mb_Projectile projectile = _launcher.Fire(_projectileData, user);
//     projectile.OnHit += (target, hitPoint, hitNormal) =>
//     {
//         Passive_Ability.RaiseBasicAttackHit(); // fires only on confirmed hit
//     };
//
//   This replaces the old pattern where RaiseBasicAttackHit() was called from the
//   ability script at fire time regardless of whether the projectile hit anything.
//   TODO: Update Rajah_Secondary.cs and Rajah_E_Ability.cs to use this pattern.
//         Remove the RaiseBasicAttackHit() call from those ability scripts once migrated.
//
// Inspector setup:
//   - Rigidbody must be attached. Set isKinematic = false, Use Gravity = false,
//     Collision Detection = Continuous (prevents tunneling at high speeds).
//   - Collider must be set to isTrigger = true.
//   - No other fields need to be set in the Inspector — all config comes from
//     SO_ProjectileData at runtime via Initialize().

using System;
using System.Collections.Generic;
using UnityEngine;

public class Mb_Projectile : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Events
    // -------------------------------------------------------------------------

    #region Events              //----------------------------------------

    /// <summary>
    /// Fired on every valid hit.
    /// Subscribe after calling Fire() to react only to confirmed hits.
    /// Parameters: (target hit, world-space hit point, surface normal at hit point)
    /// </summary>
    public event Action<Mb_CharacterBase, Vector3, Vector3> OnHit;

    /// <summary>
    /// Static version of OnHit — fired for every projectile hit in the scene.
    /// Use this for systems that need to react to all hits without per-instance
    /// subscriptions, such as a VFX manager or audio manager.
    /// Parameters: (the projectile that hit, the character that hit, the character that was hit)
    /// </summary>
    public static event Action<Mb_Projectile, Mb_CharacterBase, Mb_CharacterBase> OnAnyProjectileHit;


    /// <summary>
    /// Fired when this projectile deactivates — either from hitting MaxPierceTargets,
    /// exceeding MaxRange, or hitting a non-piercing target.
    /// TODO: Connect this to a projectile pool when pooling is implemented.
    ///       The pool should subscribe to this event and return the instance on fire.
    /// </summary>
    public event Action<Mb_Projectile> OnDeactivated;

    #endregion                  //----------------------------------------


    // -------------------------------------------------------------------------
    // Private State
    // -------------------------------------------------------------------------

    #region Private State       //----------------------------------------

    // Runtime data — populated by Initialize(), reset by OnEnable()
    private SO_ProjectileData _data;
    private Mb_CharacterBase _owner;
    private float _baseDamage;

    // Travel distance tracking — compared against SO_ProjectileData.MaxRange each frame
    private Vector3 _spawnPosition;

    // Piercing hit tracking — prevents the same collider from being hit twice.
    // Cleared in OnEnable() so pool-reused projectiles always start clean.
    private readonly HashSet<Collider> _hitColliders = new HashSet<Collider>();

    // Counts confirmed hits — compared against MaxPierceTargets for piercing projectiles
    private int _hitCount = 0;

    // Pause handling — store velocity so we can restore it exactly on resume
    private Vector3 _storedVelocity = Vector3.zero;
    private bool _isPaused = false;

    // Tracks whether Initialize() has been called — guards against OnEnable()
    // firing before the projectile has been configured by an ability script
    private bool _isInitialized = false;

    private Rigidbody _rb;

    #endregion                  //----------------------------------------


    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    #region Unity Lifecycle     //----------------------------------------

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (_rb == null)
            Debug.LogError($"[Mb_Projectile] No Rigidbody found on {gameObject.name}. " +
                           "Attach a Rigidbody with isKinematic = false and Use Gravity = false.");
    }


    private void OnEnable()
    {
        // Reset all runtime state so a reused projectile always starts clean.
        // This matters even before pooling is implemented — if a projectile is
        // re-enabled manually, stale state from the previous flight must not carry over.
        _hitColliders.Clear();
        _hitCount = 0;
        _storedVelocity = Vector3.zero;
        _isPaused = false;

        // Don't launch until Initialize() has been called.
        // If OnEnable fires before the ability script calls Initialize() (which can
        // happen if SetActive(true) is called before Initialize()), we skip the launch
        // here and let Initialize() trigger it directly instead.
        if (!_isInitialized) return;

        Launch();
    }


    private void OnDisable()
    {
        // Unsubscribe from pause events when deactivated.
        // This prevents ghost listeners accumulating if the projectile is reused.
        Mb_PauseManager.OnPaused -= HandlePause;
        Mb_PauseManager.OnResumed -= HandleResume;
    }


    private void Update()
    {
        if (_isPaused) return;
        if (!_isInitialized) return;

        // Deactivate after exceeding max range — prevents projectiles flying forever
        // if they miss every target.
        if (Vector3.Distance(_spawnPosition, transform.position) >= _data.MaxRange)
            Deactivate();
    }

    #endregion                  //----------------------------------------


    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    #region Public API          //----------------------------------------

    /// <summary>
    /// Configures this projectile and launches it.
    /// Call this immediately after positioning and orienting the projectile —
    /// before or right after SetActive(true). All behavior is driven by the data asset.
    /// </summary>
    /// <param name="data">ScriptableObject defining speed, range, piercing, and effects.</param>
    /// <param name="owner">The character firing this projectile. Used for damage credit
    /// and to skip friendly-fire collisions.</param>
    /// <param name="baseDamage">Base damage dealt on hit — calculated by the ability script
    /// using the owner's current stats at fire time.</param>
    public void Initialize(SO_ProjectileData data, Mb_CharacterBase owner, float baseDamage)
    {
        _data = data;
        _owner = owner;
        _baseDamage = baseDamage;
        _isInitialized = true;

        // Subscribe to pause events fresh each initialization.
        // Unsubscribe first as a safety net in case Initialize() is called twice
        // (e.g. if the ability script re-uses a projectile without disabling it first).
        Mb_PauseManager.OnPaused -= HandlePause;
        Mb_PauseManager.OnPaused += HandlePause;
        Mb_PauseManager.OnResumed -= HandleResume;
        Mb_PauseManager.OnResumed += HandleResume;

        // If the GameObject is already active (i.e. SetActive(true) was called before
        // Initialize()), OnEnable already fired but skipped the launch. Launch now.
        if (gameObject.activeInHierarchy)
            Launch();
    }

    #endregion                  //----------------------------------------


    // -------------------------------------------------------------------------
    // Hit Detection
    // -------------------------------------------------------------------------

    #region Hit Detection       //----------------------------------------

    private void OnTriggerEnter(Collider other)
    {
        if (!_isInitialized) return;
        if (_isPaused) return;

        if (_hitColliders.Contains(other)) return;

        // Skip friendly-fire — owner tag: "Player" for Guardians, "CuBot" for CuBot projectiles.
        if (_owner != null && other.gameObject.CompareTag(_owner.tag)) return;

        // Skip Player-Panoharra friendly-fire
        if (_owner.CompareTag("Player") && other.gameObject.CompareTag("Panoharra")) return;

        // Must be damageable — environment colliders (walls, floor) have no I_Damageable.
        // Gravity-enabled projectiles are thrown objects, so they should expire when they land.
        I_Damageable damageable = other.GetComponent<I_Damageable>();
        if (damageable == null)
        {
            if (_rb != null && _rb.useGravity)
                Deactivate();

            return;
        }

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        Vector3 hitNormal = (transform.position - hitPoint).normalized;

        // Try to get a full character — used for hit effects, kill credit, and OnHit event.
        // Panoharra has no Mb_CharacterBase so target will be null for it — that is fine,
        // the damage path below runs regardless.
        Mb_CharacterBase target = other.GetComponent<Mb_CharacterBase>();

        if (target != null && target.Health != null && target.Health.IsUntargetable)
            return;

        // Register the hit so this collider cannot be hit again this flight.
        _hitColliders.Add(other);
        _hitCount++;

        // Credit the kill to the correct attacker BEFORE calling TakeDamage().
        // Only relevant for CuBots — Panoharra does not use the kill-credit system.
        if (target != null)
        {
            MB_CuBotBase cuBot = target as MB_CuBotBase;
            if (cuBot != null && _owner != null)
                cuBot.SetLastAttacker(_owner);
        }

        // Apply base damage — works for any I_Damageable including Panoharra.
        damageable.TakeDamage(_baseDamage);

        // Apply hit effects only when a full character was hit.
        // Panoharra intentionally receives no knockback, slow, or stun.
        if (target != null && _data.HitEffects != null)
        {
            foreach (Sc_HitEffect effect in _data.HitEffects)
            {
                if (effect != null)
                    effect.ApplyOnHit(target, _owner, hitPoint, hitNormal);
            }
        }

        // Fire instance and static events only for full character hits.
        // Panoharra hits do not trigger Guardian passives or VFX subscriptions.
        if (target != null)
        {
            OnHit?.Invoke(target, hitPoint, hitNormal);
            OnAnyProjectileHit?.Invoke(this, _owner,target);
        }

        bool shouldDeactivate = !_data.IsPiercing ||
                                _hitCount >= _data.MaxPierceTargets;

        if (shouldDeactivate)
            Deactivate();
    }

    #endregion                  //----------------------------------------


    // -------------------------------------------------------------------------
    // Launch and Deactivate
    // -------------------------------------------------------------------------

    #region Launch and Deactivate   //----------------------------------------

    // Fires the Rigidbody forward at the configured launch speed.
    // Called from OnEnable() (if already initialized) or from Initialize()
    // (if the GameObject was already active when Initialize() was called).
    private void Launch()
    {
        _spawnPosition = transform.position;

        if (_rb == null) return;

        _rb.isKinematic = false;
        _rb.linearVelocity = transform.forward * _data.LaunchSpeed;
    }


    /// <summary>
    /// Deactivates this projectile and fires the OnDeactivated event.
    /// All projectile lifetime endings go through here — never call
    /// Destroy() or SetActive(false) directly from outside this class.
    /// TODO: When projectile pooling is added, the pool should subscribe to
    ///       OnDeactivated and return this instance to the available stack.
    /// </summary>
    private void Deactivate()
    {
        _isInitialized = false;

        // Clear instance-level event subscriptions so listeners from the previous
        // flight don't carry over if this projectile is reused.
        // Static events (OnAnyProjectileHit) are intentionally NOT cleared here —
        // global listeners manage their own subscriptions.
        OnHit = null;

        OnDeactivated?.Invoke(this);
        OnDeactivated = null;

        gameObject.SetActive(false);
        // TODO: Return to pool here once projectile pooling is implemented.
        //       Replace gameObject.SetActive(false) with a pool.Return(this) call.

        Destroy(gameObject); // Temporary until pooling is implemented — destroy to prevent clutter during testing)
    }

    #endregion                  //----------------------------------------


    // -------------------------------------------------------------------------
    // Pause Handling
    // -------------------------------------------------------------------------

    #region Pause Handling      //----------------------------------------

    // Stores the Rigidbody velocity and sets it kinematic so physics stops.
    // Time.timeScale = 0 alone does NOT stop a moving Rigidbody — this is required.
    private void HandlePause()
    {
        if (_rb == null) return;

        _isPaused = true;
        _storedVelocity = _rb.linearVelocity;
        _rb.isKinematic = true;
    }


    // Restores the stored velocity and re-enables physics simulation.
    private void HandleResume()
    {
        if (_rb == null) return;

        _isPaused = false;
        _rb.isKinematic = false;
        _rb.linearVelocity = _storedVelocity;
    }

    #endregion                  //----------------------------------------


    // -------------------------------------------------------------------------
    // Accessors
    // -------------------------------------------------------------------------

    #region Accessors           //----------------------------------------

    /// <summary>Returns the base damage this projectile was initialized with.</summary>
    public float GetDamageAmount() => _baseDamage;

    /// <summary>Returns the character who fired this projectile. May be null.</summary>
    public Mb_CharacterBase GetOwner() => _owner;

    #endregion                  //----------------------------------------
}
