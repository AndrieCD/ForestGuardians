// Mb_ProjectileLauncher.cs
// Reusable component that handles all projectile spawning for any character.
// Attach this to any GameObject that fires projectiles — Guardians or CuBots.
//
// WHY THIS EXISTS:
//   Without a launcher component, every ability script that fires a projectile
//   would duplicate the same spawn, position, orient, and initialize logic.
//   This class is the single place that logic lives. Ability scripts call
//   Fire() or FireToward() and get back a ready Mb_Projectile to subscribe to.
//
// PASSIVE TRIGGER PATTERN — read this before writing any ability that fires projectiles:
//   Subscribe to OnHit AFTER calling Fire() so the passive only triggers on
//   confirmed hits, not at fire time. Example:
//
//     Mb_Projectile projectile = _launcher.Fire(_projectileData, user, damage);
//     projectile.OnHit += (target, hitPoint, hitNormal) =>
//     {
//         Passive_Ability.RaiseBasicAttackHit(); // only fires on actual hit
//     };
//
//   TODO: Rajah_Secondary.cs currently calls RaiseBasicAttackHit() at fire time.
//         Migrate it to subscribe to OnHit here instead, then remove the old call.
//   TODO: Rajah_E_Ability.cs fires spread shots — migrate each Fire() call to
//         FireToward() with an explicit spread direction, then subscribe to OnHit
//         on each returned projectile for passive triggering.
//
// AIM RESOLUTION:
//   Fire()        — for Guardians: raycasts from screen center to find a world-space
//                   aim point, then orients the projectile toward it. This matches
//                   the crosshair aim model used by most third-person shooters.
//                 — for CuBots: uses the NavMeshAgent's current facing direction.
//   FireToward()  — explicit direction override. Use this for spread shots (Rajah E),
//                   arc patterns, and any shot that shouldn't follow the camera aim.
//
// Inspector setup:
//   LaunchOrigin  — assign the child Transform that marks where projectiles spawn.
//                   Position this at the character's hand, weapon tip, or barrel.
//                   If left null, falls back to this component's own transform.
//   ProjectilePrefab — the prefab containing Mb_Projectile, Rigidbody, and a
//                   trigger Collider. One prefab can serve all projectile types —
//                   behavior is driven by SO_ProjectileData, not the prefab.
//
// TODO: When projectile pooling is implemented, replace Instantiate() in
//       SpawnProjectile() with a pool fetch. The rest of the class stays the same.

using UnityEngine;

public class Mb_ProjectileLauncher : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    #region Inspector Fields    //----------------------------------------

    [Header("Spawn Point")]
    [Tooltip("The Transform projectiles spawn from — typically a hand or weapon tip child. " +
             "Falls back to this component's own transform if left unassigned.")]
    [SerializeField] private Transform LaunchOrigin;

    [Header("Projectile Prefab")]
    [Tooltip("Prefab with Mb_Projectile, Rigidbody (isKinematic = false, gravity = false), " +
             "and a trigger Collider. All behavior is driven by SO_ProjectileData at runtime — " +
             "one prefab can serve every projectile type.")]
    [SerializeField] private GameObject ProjectilePrefab;

    [Header("Aim Raycast")]
    [Tooltip("Layer mask for the aim raycast used by Fire(). " +
             "Include terrain, environment, and enemy layers. " +
             "Exclude the Player layer so the ray doesn't hit the Guardian herself.")]
    [SerializeField] private LayerMask AimLayerMask = Physics.DefaultRaycastLayers;

    [Tooltip("How far the aim raycast travels before using the ray endpoint as the aim point. " +
             "Set this to match or exceed the projectile's MaxRange.")]
    [SerializeField] private float AimRaycastDistance = 100f;

    #endregion                  //----------------------------------------


    // -------------------------------------------------------------------------
    // Private State
    // -------------------------------------------------------------------------

    #region Private State       //----------------------------------------

    // Cached owner reference — used to determine aim mode (Guardian vs CuBot)
    // and passed to projectile Initialize() calls.
    private Mb_CharacterBase _owner;

    // Cached component references — fetched once in Awake
    private Mb_GuardianBase _guardianBase;      // non-null if owner is a Guardian
    private UnityEngine.AI.NavMeshAgent _agent; // non-null if owner is a CuBot

    #endregion                  //----------------------------------------


    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    #region Unity Lifecycle     //----------------------------------------

    private void Awake()
    {
        _owner = GetComponent<Mb_CharacterBase>();

        if (_owner == null)
            Debug.LogError($"[Mb_ProjectileLauncher] No Mb_CharacterBase found on " +
                           $"{gameObject.name}. Launcher must be on the same GameObject " +
                           $"as the character component.");

        // Determine firing mode by checking which character type owns this launcher.
        // Guardians aim via camera raycast; CuBots aim via NavMeshAgent facing direction.
        _guardianBase = GetComponent<Mb_GuardianBase>();
        _agent = GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (ProjectilePrefab == null)
            Debug.LogError($"[Mb_ProjectileLauncher] No ProjectilePrefab assigned on " +
                           $"{gameObject.name}. Assign a prefab with Mb_Projectile attached.");

        // Fall back to this transform if no LaunchOrigin was assigned in the Inspector.
        // This keeps the launcher functional during early prototyping before a proper
        // hand bone or weapon tip transform has been set up.
        if (LaunchOrigin == null)
        {
            LaunchOrigin = transform;
            Debug.LogWarning($"[Mb_ProjectileLauncher] No LaunchOrigin assigned on " +
                             $"{gameObject.name}. Falling back to root transform. " +
                             $"Assign a child transform for accurate spawn positioning.");
        }
    }

    #endregion                  //----------------------------------------


    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    #region Public API          //----------------------------------------

    /// <summary>
    /// Fires a projectile from LaunchOrigin toward the resolved aim point.
    /// For Guardians: raycasts from screen center to find the world-space target.
    /// For CuBots: uses the NavMeshAgent's current facing direction.
    ///
    /// Returns the Mb_Projectile instance so the calling ability can subscribe
    /// to OnHit for passive triggers. Always subscribe AFTER calling Fire():
    ///
    ///   Mb_Projectile p = _launcher.Fire(data, user, damage);
    ///   p.OnHit += (target, hitPoint, hitNormal) => { YourPassiveTrigger(); };
    /// </summary>
    /// <param name="data">ProjectileData asset defining speed, range, piercing, effects.</param>
    /// <param name="owner">The character firing — used for damage credit and faction check.</param>
    /// <param name="baseDamage">Damage calculated by the ability at fire time.</param>
    /// <returns>The fired Mb_Projectile, or null if spawning failed.</returns>
    public Mb_Projectile Fire(SO_ProjectileData data, Mb_CharacterBase owner, float baseDamage)
    {
        if (!ValidateBeforeFire(data)) return null;

        Vector3 aimDirection = ResolveAimDirection();
        return SpawnAndLaunch(data, owner, baseDamage, aimDirection);
    }


    /// <summary>
    /// Fires a projectile in an explicit direction, bypassing aim resolution.
    /// Use this for:
    ///   - Spread shots (Rajah E) where each projectile has a pre-calculated direction
    ///   - Arc patterns (multiple projectiles fanned around a center direction)
    ///   - CuBot abilities that aim at a specific target rather than straight ahead
    ///
    /// Returns the Mb_Projectile instance — subscribe to OnHit for passive triggers.
    ///
    ///   Mb_Projectile p = _launcher.FireToward(data, user, damage, spreadDir);
    ///   p.OnHit += (target, hitPoint, hitNormal) => { YourPassiveTrigger(); };
    /// </summary>
    /// <param name="direction">World-space direction the projectile will travel.
    /// Does not need to be normalized — this method normalizes it internally.</param>
    public Mb_Projectile FireToward(SO_ProjectileData data, Mb_CharacterBase owner,
                                    float baseDamage, Vector3 direction)
    {
        if (!ValidateBeforeFire(data)) return null;

        // Normalize so LaunchSpeed from SO_ProjectileData is always the true speed
        // regardless of what magnitude the caller passed in.
        Vector3 aimDirection = direction.normalized;
        if (aimDirection == Vector3.zero)
        {
            Debug.LogWarning($"[Mb_ProjectileLauncher] FireToward called with zero direction " +
                             $"on {gameObject.name}. Falling back to forward.");
            aimDirection = transform.forward;
        }

        return SpawnAndLaunch(data, owner, baseDamage, aimDirection);
    }

    #endregion                  //----------------------------------------


    // -------------------------------------------------------------------------
    // Aim Resolution
    // -------------------------------------------------------------------------

    #region Aim Resolution      //----------------------------------------

    // Determines the fire direction based on owner type.
    // Guardian: camera-center raycast for crosshair accuracy.
    // CuBot: NavMeshAgent facing direction for straight-ahead AI shots.
    // Fallback: this transform's forward direction.
    private Vector3 ResolveAimDirection()
    {
        // --- Guardian aim: camera raycast ---
        // Cast a ray from the center of the screen into the scene.
        // If it hits something, aim the projectile at that exact world point.
        // If it misses (open sky, etc.), use the far end of the ray as the target.
        // This is the standard crosshair aim model for third-person shooters.
        if (_guardianBase != null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Ray aimRay = mainCamera.ScreenPointToRay(
                    new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f)
                );

                Vector3 aimTarget = Physics.Raycast(aimRay, out RaycastHit hit,
                    AimRaycastDistance, AimLayerMask)
                    ? hit.point
                    : aimRay.GetPoint(AimRaycastDistance);

                // Direction from the launch origin toward the resolved aim point.
                // Using LaunchOrigin (not camera) as the start of this direction vector
                // ensures the projectile travels toward where the player is aiming,
                // not where the camera is positioned (which can differ significantly
                // in third-person view).
                return (aimTarget - LaunchOrigin.position).normalized;
            }

            Debug.LogWarning($"[Mb_ProjectileLauncher] Camera.main is null on " +
                             $"{gameObject.name}. Falling back to transform.forward.");
        }

        // --- CuBot aim: NavMeshAgent facing direction ---
        // The agent's velocity direction is the direction the CuBot is currently
        // moving and facing. When the CuBot stops to attack (agent.isStopped = true),
        // velocity is zero — fall through to transform.forward in that case.
        if (_agent != null && _agent.velocity.sqrMagnitude > 0.01f)
            return _agent.velocity.normalized;

        // Fallback: fire straight ahead along the character's facing direction.
        // This covers CuBots that have stopped moving and any unexpected edge cases.
        return transform.forward;
    }

    #endregion                  //----------------------------------------


    // -------------------------------------------------------------------------
    // Spawn Internals
    // -------------------------------------------------------------------------

    #region Spawn Internals     //----------------------------------------

    // Core spawn method — instantiates the projectile, positions and orients it,
    // then calls Initialize() so it launches itself.
    private Mb_Projectile SpawnAndLaunch(SO_ProjectileData data, Mb_CharacterBase owner,
                                         float baseDamage, Vector3 direction)
    {
        Mb_Projectile projectile = SpawnProjectile();
        if (projectile == null) return null;

        // Position at the launch origin and rotate to face the aim direction.
        // We set rotation before Initialize() so that when Launch() reads
        // transform.forward to set Rigidbody velocity, it gets the correct direction.
        projectile.transform.SetPositionAndRotation(
            LaunchOrigin.position,
            Quaternion.LookRotation(direction)
        );

        // Initialize configures all runtime behavior from the data asset and
        // launches the Rigidbody. Order: position/rotate → then Initialize().
        projectile.Initialize(data, owner, baseDamage);

        return projectile;
    }


    // Instantiates or fetches from pool.
    // TODO: Replace Instantiate() with a pool fetch when projectile pooling is added.
    //       The rest of SpawnAndLaunch() does not need to change — only this method.
    private Mb_Projectile SpawnProjectile()
    {
        GameObject go = Instantiate(ProjectilePrefab);

        Mb_Projectile projectile = go.GetComponent<Mb_Projectile>();

        if (projectile == null)
        {
            Debug.LogError($"[Mb_ProjectileLauncher] ProjectilePrefab on {gameObject.name} " +
                           $"is missing the Mb_Projectile component. Destroying spawned object.");
            Destroy(go);
            return null;
        }


        return projectile;
    }


    // Guards against misconfigured calls — returns false if something critical is missing.
    private bool ValidateBeforeFire(SO_ProjectileData data)
    {
        if (ProjectilePrefab == null)
        {
            Debug.LogError($"[Mb_ProjectileLauncher] Cannot fire — ProjectilePrefab is null " +
                           $"on {gameObject.name}.");
            return false;
        }

        if (data == null)
        {
            Debug.LogError($"[Mb_ProjectileLauncher] Cannot fire — SO_ProjectileData is null. " +
                           $"Assign a data asset before calling Fire() or FireToward().");
            return false;
        }

        return true;
    }

    #endregion                  //----------------------------------------
}