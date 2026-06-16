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
    #region Inspector Fields    //----------------------------------------

    [Header("Spawn Point")]
    [Tooltip("The Transform projectiles spawn from — typically a hand or weapon tip child. " +
             "Falls back to this component's own transform if left unassigned.")]
    [SerializeField] private Transform LaunchOrigin;

    [Header("Aim Raycast")]
    [Tooltip("Layer mask for the aim raycast used by Fire(). " +
             "Include terrain, environment, and enemy layers. " +
             "Exclude the Player layer so the ray doesn't hit the Guardian.")]
    [SerializeField] private LayerMask AimLayerMask = Physics.DefaultRaycastLayers; // Exclude Transparent as well

    [Tooltip("How far the aim raycast travels before using the ray endpoint as the aim point.")]
    [SerializeField] private float AimRaycastDistance = 100f;

    #endregion                  //----------------------------------------


    #region Private State       //----------------------------------------

    private Mb_CharacterBase _owner;
    private Mb_GuardianBase _guardianBase;
    private UnityEngine.AI.NavMeshAgent _agent;

    #endregion                  //----------------------------------------


    #region Unity Lifecycle     //----------------------------------------

    private void Awake()
    {
        _owner = GetComponent<Mb_CharacterBase>();

        if (_owner == null)
            Debug.LogError($"[Mb_ProjectileLauncher] No Mb_CharacterBase found on " +
                           $"{gameObject.name}.");

        _guardianBase = GetComponent<Mb_GuardianBase>();
        _agent = GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (LaunchOrigin == null)
        {
            LaunchOrigin = transform;
            Debug.LogWarning($"[Mb_ProjectileLauncher] No LaunchOrigin assigned on " +
                             $"{gameObject.name}. Falling back to root transform.");
        }
    }

    #endregion                  //----------------------------------------


    #region Public API          //----------------------------------------

    /// <summary>
    /// Fires a projectile using the given prefab, aimed via camera center raycast
    /// (Guardian) or NavMeshAgent facing direction (CuBot).
    /// Returns the Mb_Projectile instance — subscribe to OnHit after calling this.
    /// </summary>
    /// <param name="prefab">The projectile prefab to instantiate. Must have Mb_Projectile.</param>
    /// <param name="data">SO_ProjectileData defining speed, range, piercing, effects.</param>
    /// <param name="owner">The character firing.</param>
    /// <param name="baseDamage">Damage calculated by the ability at fire time.</param>
    public Mb_Projectile Fire(
        GameObject prefab,
        SO_ProjectileData data,
        Mb_CharacterBase owner,
        float baseDamage)
    {
        if (!ValidateBeforeFire(prefab, data)) return null;

        Vector3 aimDirection = ResolveAimDirection();
        return SpawnAndLaunch(prefab, data, owner, baseDamage, aimDirection);
    }


    /// <summary>
    /// Fires a projectile in an explicit direction, bypassing aim resolution.
    /// Use for spread shots, arc patterns, and AI abilities.
    /// Returns the Mb_Projectile instance — subscribe to OnHit after calling this.
    /// </summary>
    public Mb_Projectile FireToward(
        GameObject prefab,
        SO_ProjectileData data,
        Mb_CharacterBase owner,
        float baseDamage,
        Vector3 direction)
    {
        if (!ValidateBeforeFire(prefab, data)) return null;

        Vector3 aimDirection = direction.normalized;
        if (aimDirection == Vector3.zero)
        {
            Debug.LogWarning($"[Mb_ProjectileLauncher] FireToward called with zero direction. " +
                             $"Falling back to forward.");
            aimDirection = transform.forward;
        }

        return SpawnAndLaunch(prefab, data, owner, baseDamage, aimDirection);
    }

    public Mb_Projectile FireToward(
        GameObject prefab,
        SO_ProjectileData data,
        Mb_CharacterBase owner,
        float baseDamage,
        Vector3 spawnPosition,
        Vector3 direction)
    {
        if (!ValidateBeforeFire(prefab, data)) return null;

        Vector3 aimDirection = direction.normalized;
        if (aimDirection == Vector3.zero)
        {
            Debug.LogWarning($"[Mb_ProjectileLauncher] FireToward called with zero direction. " +
                             $"Falling back to forward.");
            aimDirection = transform.forward;
        }

        return SpawnAndLaunch(prefab, data, owner, baseDamage, spawnPosition, aimDirection);
    }

    #endregion                  //----------------------------------------


    #region Aim Resolution      //----------------------------------------

    private Vector3 ResolveAimDirection()
    {
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

                return SafeDirectionToTarget(LaunchOrigin.position, aimTarget);
            }

            Debug.LogWarning($"[Mb_ProjectileLauncher] Camera.main is null. " +
                             $"Falling back to transform.forward.");
        }

        if (_agent != null && _agent.velocity.sqrMagnitude > 0.01f)
            return _agent.velocity.normalized;

        return transform.forward;
    }

    #endregion                  //----------------------------------------


    #region Spawn Internals     //----------------------------------------

    private Mb_Projectile SpawnAndLaunch(
        GameObject prefab,
        SO_ProjectileData data,
        Mb_CharacterBase owner,
        float baseDamage,
        Vector3 direction)
    {
        Mb_Projectile projectile = SpawnProjectile(prefab);
        if (projectile == null) return null;

        projectile.transform.SetPositionAndRotation(
            LaunchOrigin.position,
            Quaternion.LookRotation(direction)
        );

        projectile.Initialize(data, owner, baseDamage);

        return projectile;
    }

    private Mb_Projectile SpawnAndLaunch(
        GameObject prefab,
        SO_ProjectileData data,
        Mb_CharacterBase owner,
        float baseDamage,
        Vector3 spawnPosition,
        Vector3 direction)
    {
        Mb_Projectile projectile = SpawnProjectile(prefab);
        if (projectile == null) return null;

        projectile.transform.SetPositionAndRotation(
            spawnPosition,
            Quaternion.LookRotation(direction)
        );

        projectile.Initialize(data, owner, baseDamage);

        return projectile;
    }

    private Mb_Projectile SpawnProjectile(GameObject prefab)
    {
        // TODO: Replace Instantiate() with a pool fetch when projectile pooling is added.
        GameObject go = Instantiate(prefab);

        Mb_Projectile projectile = go.GetComponent<Mb_Projectile>();

        if (projectile == null)
        {
            Debug.LogError($"[Mb_ProjectileLauncher] Prefab '{prefab.name}' is missing " +
                           $"the Mb_Projectile component. Destroying spawned object.");
            Destroy(go);
            return null;
        }

        return projectile;
    }


    private bool ValidateBeforeFire(GameObject prefab, SO_ProjectileData data)
    {
        if (prefab == null)
        {
            Debug.LogError($"[Mb_ProjectileLauncher] Cannot fire — prefab is null on " +
                           $"{gameObject.name}. Fetch it from Mb_AbilityPrefabRegistry.");
            return false;
        }

        if (data == null)
        {
            Debug.LogError($"[Mb_ProjectileLauncher] Cannot fire — SO_ProjectileData is null.");
            return false;
        }

        return true;
    }


    // Converts a world-space aim target into a safe fire direction from a given origin.
    // Guards against the case where LaunchOrigin is ahead of the aim target (e.g. the
    // origin is inside an obstacle directly in front of the player) — in that case the
    // raw direction vector would point backward, so we fall back to the Guardian's
    // body forward direction instead.
    private Vector3 SafeDirectionToTarget(Vector3 fromOrigin, Vector3 toTarget)
    {
        Vector3 direction = toTarget - fromOrigin;

        // Dot product check: if the aim target is behind or exactly at the origin,
        // the direction is invalid. Fall back to Guardian body forward.
        // Using _guardianBase.transform.forward rather than LaunchOrigin.forward
        // because the Guardian's body always faces the intended fire direction —
        // LaunchOrigin may be a child Transform with a different local rotation.
        if (Vector3.Dot(_guardianBase.transform.forward, direction) <= 0f)
            return _guardianBase.transform.forward;

        return direction.normalized;
    }
    #endregion                  //----------------------------------------
}