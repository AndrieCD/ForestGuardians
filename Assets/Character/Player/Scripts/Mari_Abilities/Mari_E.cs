// Mari_E.cs
// [E] Psychic Slam — Mari fires a forward piercing psychic projectile.
//
// BEHAVIOR:
//   A trigger-only projectile is launched from Mari's ProjectileOrigin toward
//   the center-screen cursor/crosshair. It passes through all enemies it contacts,
//   dealing damage once per enemy and applying knockback in the projectile's
//   travel direction. It does not try to sit on terrain, so cliff edges and steep
//   mountain elevation do not change its behavior.
//
// KNOCKBACK:
//   Each enemy hit receives a horizontal push along the projectile's forward
//   direction.
//   - Guardians: Mb_Movement.ApplyDisplacement(velocity, duration)
//   - CuBots:    NavMeshAgent disabled, CharacterController.Move() for duration,
//                then agent re-enabled (same pattern as Sc_HitEffect_Knockback)
//   This is handled inside Mb_PsychicSlamProjectile (the projectile component)
//   which owns all per-hit logic so Mari_E stays clean.
//
// OVERCHARGE (from Mari_Passive.IsOvercharged):
//   - Damage          × OverchargeDamageMultiplier    (default 2f)
//   - KnockbackForce  × OverchargeKnockbackMultiplier (default 2f)
//   Overcharge is consumed before the projectile fires.
//   The resolved final values are passed into the projectile via Initialize().
//
// PROJECTILE PREFAB SETUP (for Leo/Angel):
//   Create a prefab with:
//   - A BoxCollider (isTrigger = true) — compact projectile-sized volume
//   - A Rigidbody (isKinematic = true, useGravity = false) — required for OnTrigger events
//   - Attach Mb_PsychicSlamProjectile to the root
//   - A child ParticleSystem named "ShockwaveVFX" (looping psychic projectile effect)
//   - A child mesh GO for the visible projectile
//     named "ShockwaveVisual" — scales with collider dimensions
//   Assign this prefab to the Mari_PsychicSlamShockwave slot in Mb_AbilityPrefabRegistry.
//
// VFX ON MARI:
//   "PsychicSlamCastVFX" — burst particle system child on Mari's prefab.
//   Plays at cast time (e.g. energy burst from Mari's hands/body).
//   TODO: Leo — add and name this particle system on Mari's prefab.
//
// INSPECTOR SETUP (on Mari_E fields):
//   ShockwavePrefab           — the trigger projectile prefab
//   ProjectileWidth           — lateral extent of the projectile
//   ProjectileHeight          — vertical extent of the projectile
//   ProjectileDepth           — forward trigger thickness
//   KnockbackForce            — push speed in units/second        (default 12f)
//   KnockbackDuration         — push duration in seconds          (default 0.3f)
//   OverchargeDamageMultiplier    — (default 2f)
//   OverchargeKnockbackMultiplier — (default 2f)

using UnityEngine;

public class Mari_E : Sc_BaseAbility
{
    // -------------------------------------------------------------------------
    // Inspector-Assigned Fields
    // -------------------------------------------------------------------------

    // The piercing projectile trigger prefab.
    // Must have: BoxCollider (isTrigger), Rigidbody (kinematic),
    // Mb_PsychicSlamProjectile, child "ShockwaveVFX" ParticleSystem,
    // child "ShockwaveVisual" mesh GO.
    private GameObject _shockwavePrefab;

    [Header("Projectile Dimensions")]
    [SerializeField] private float _ProjectileWidth = 1.25f;
    [SerializeField] private float _ProjectileHeight = 1.25f;
    [SerializeField] private float _ProjectileDepth = 1.5f;

    [Header("Aim")]
    [SerializeField] private float _AimRaycastDistance = 100f;

    [Header("Knockback")]
    // TODO: Tune knockback feel in playtesting once enemy move speeds are final.
    [SerializeField] private float _KnockbackForce = 15f;
    [SerializeField] private float _KnockbackDuration = 0.3f;

    [Header("Overcharge Multipliers")]
    [SerializeField] private float _OverchargeDamageMultiplier = 2f;
    [SerializeField] private float _OverchargeKnockbackMultiplier = 2f;


    // -------------------------------------------------------------------------
    // Cached References
    // -------------------------------------------------------------------------

    // Cast burst VFX on Mari herself — located by name in OnEquip
    private ParticleSystem _castVFX;


    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public Mari_E(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user) { }


    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public override void OnEquip(Mb_CharacterBase user)
    {
        Mb_AbilityPrefabRegistry registry = user.GetComponent<Mb_AbilityPrefabRegistry>();
        _shockwavePrefab = registry?.GetPrefab(AbilityPrefabID.Mari_PsychicSlamShockwave);

        if (_shockwavePrefab == null)
            Debug.LogError("[Mari_E] Mari_PsychicSlamShockwave prefab not found in registry.");


        foreach (ParticleSystem ps in user.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject.name == "PsychicSlamCastVFX")
            {
                _castVFX = ps;
                break;
            }
        }

        Debug.Log($"[Mari_E] Equipped {_AbilityData.AbilityName}.");
    }


    public override void OnUnequip(Mb_CharacterBase user)
    {
        Debug.Log($"[Mari_E] Unequipped {_AbilityData.AbilityName}.");
    }


    // -------------------------------------------------------------------------
    // Activation
    // -------------------------------------------------------------------------

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        if (_shockwavePrefab == null)
        {
            Debug.LogError("[Mari_E] ShockwavePrefab is not assigned.");
            return;
        }

        // --- Resolve overcharge before consuming it ---
        bool isOvercharged = false;
        Mari_Passive passive = GetPassive();
        if (passive != null && passive.IsOvercharged)
        {
            isOvercharged = true;
            passive.ConsumeOvercharge();
            Debug.Log("[Mari_E] Overcharged Psychic Slam activated!");
        }

        // --- Resolve final damage ---
        float baseDamage = _AbilityData.GetStat(
            "Damage",
            CurrentLevel,
            user.Stats.AttackPower.GetValue(),
            user.Stats.AbilityPower.GetValue()
        );
        float finalDamage = isOvercharged
            ? baseDamage * _OverchargeDamageMultiplier
            : baseDamage;

        // --- Resolve final knockback ---
        float finalKnockbackForce = isOvercharged
            ? _KnockbackForce * _OverchargeKnockbackMultiplier
            : _KnockbackForce;

        Vector3 spawnPos = _Guardian != null && _Guardian.ProjectileOrigin != null
            ? _Guardian.ProjectileOrigin.position
            : user.transform.position + Vector3.up * (_ProjectileHeight * 0.5f);

        Vector3 fireDirection = ResolveCursorAimDirection(user, spawnPos);
        Quaternion spawnRot = Quaternion.LookRotation(fireDirection, Vector3.up);

        // --- Instantiate shockwave ---
        GameObject shockwaveGO = GameObject.Instantiate(
            _shockwavePrefab,
            spawnPos,
            spawnRot
        );

        // --- Configure projectile component ---
        Mb_PsychicSlamProjectile projectile =
            shockwaveGO.GetComponent<Mb_PsychicSlamProjectile>();

        if (projectile == null)
        {
            Debug.LogError("[Mari_E] ShockwavePrefab is missing " +
                           "Mb_PsychicSlamProjectile component.");
            GameObject.Destroy(shockwaveGO);
            return;
        }

        projectile.Initialize(
            owner: user,
            damage: finalDamage,
            knockbackForce: finalKnockbackForce,
            knockbackDuration: _KnockbackDuration,
            width: _ProjectileWidth,
            height: _ProjectileHeight,
            depth: _ProjectileDepth,
            isOvercharged: isOvercharged
        );

        // --- Cast VFX on Mari ---
        _castVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _castVFX?.Play();

        TriggerAbilityAnimation(user);

        StartCooldown(user, GetAbilityCooldown(user));
    }


    // -------------------------------------------------------------------------
    // Passive Retrieval
    // -------------------------------------------------------------------------

    private Mari_Passive GetPassive()
    {
        Sc_BaseAbility passive = _User.Abilities.GetAbilityBySlot(AbilitySlot.Passive);
        return passive as Mari_Passive;
    }


    // -------------------------------------------------------------------------
    // Aim
    // -------------------------------------------------------------------------

    private Vector3 ResolveCursorAimDirection(Mb_CharacterBase user, Vector3 spawnPosition)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[Mari_E] Camera.main is null. Falling back to Mari forward.");
            return user.transform.forward;
        }

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        Vector3 aimTarget = Physics.Raycast(
            ray,
            out RaycastHit hit,
            _AimRaycastDistance,
            Mb_ProjectileLauncher.DefaultAimLayerMask,
            QueryTriggerInteraction.Ignore)
            ? hit.point
            : ray.GetPoint(_AimRaycastDistance);

        Vector3 direction = aimTarget - spawnPosition;

        if (Vector3.Dot(user.transform.forward, direction.normalized) < Mb_ProjectileLauncher.MIN_FORWARD_AIM_DOT)
            return user.transform.forward;

        if (direction.sqrMagnitude <= 0.001f)
            return user.transform.forward;

        return direction.normalized;
    }


    // -------------------------------------------------------------------------
    // Animation
    // -------------------------------------------------------------------------

    protected override void TriggerAbilityAnimation(Mb_CharacterBase user)
    {
        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.TriggerEAbility();
    }
}
