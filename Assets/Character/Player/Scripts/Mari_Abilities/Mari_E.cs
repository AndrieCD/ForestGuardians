// Mari_E.cs
// [E] Psychic Slam — Mari fires a wide psychic shockwave forward.
//
// BEHAVIOR:
//   A wide, flat trigger-only projectile is launched forward from Mari's position.
//   It passes through all enemies it contacts (piercing), dealing damage and
//   pushing each one away from the projectile's world-space origin point.
//   The projectile deactivates after traveling MaxRange units or hitting a solid
//   surface (if IsPiercing = true on SO_ProjectileData, it ignores enemy colliders
//   and only stops on environment geometry).
//
// KNOCKBACK:
//   Each enemy hit receives a push in the direction from the projectile's spawn
//   origin away to the enemy's position — this makes the shockwave feel like a
//   real physical force emanating from one point.
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
//   - A BoxCollider (isTrigger = true) — wide and flat: (ShockwaveWidth, ShockwaveHeight, ShockwaveDepth)
//     Width is the dominant axis — this is the "wall" of the shockwave.
//   - A Rigidbody (isKinematic = true, useGravity = false) — required for OnTrigger events
//   - Attach Mb_PsychicSlamProjectile to the root
//   - A child ParticleSystem named "ShockwaveVFX" (looping ripple/energy wall effect)
//   - A child mesh GO for the visible shockwave (flat energy plane, additive shader)
//     named "ShockwaveVisual" — scales with collider dimensions
//   Assign this prefab to Mari_E.ShockwavePrefab in the Inspector.
//
// VFX ON MARI:
//   "PsychicSlamCastVFX" — burst particle system child on Mari's prefab.
//   Plays at cast time (e.g. energy burst from Mari's hands/body).
//   TODO: Leo — add and name this particle system on Mari's prefab.
//
// INSPECTOR SETUP (on Mari_E fields):
//   ShockwavePrefab           — the wide trigger projectile prefab
//   ShockwaveWidth            — lateral extent of the shockwave   (default 5f)
//   ShockwaveHeight           — vertical extent                   (default 2.5f)
//   ShockwaveDepth            — forward thickness of the trigger  (default 1f)
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

    // The wide shockwave trigger prefab.
    // Must have: BoxCollider (isTrigger), Rigidbody (kinematic),
    // Mb_PsychicSlamProjectile, child "ShockwaveVFX" ParticleSystem,
    // child "ShockwaveVisual" mesh GO.
    private GameObject _shockwavePrefab;

    [Header("Shockwave Dimensions")]
    // TODO: Tune once visual asset exists — width is the most impactful value.
    [SerializeField] private float _ShockwaveWidth = 5f;
    [SerializeField] private float _ShockwaveHeight = 2.5f;
    [SerializeField] private float _ShockwaveDepth = 1f;   // Forward thickness

    [Header("Knockback")]
    // TODO: Tune knockback feel in playtesting once enemy move speeds are final.
    [SerializeField] private float _KnockbackForce = 12f;
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

        // --- Spawn position ---
        // Spawn the shockwave at Mari's position, facing her forward direction.
        // The projectile moves forward from here via Mb_PsychicSlamProjectile.
        Vector3 spawnPos = user.transform.position
                         + Vector3.up * (_ShockwaveHeight / 2f);  // Vertically center on Mari

        Quaternion spawnRot = user.transform.rotation;

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
            width: _ShockwaveWidth,
            height: _ShockwaveHeight,
            depth: _ShockwaveDepth,
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
    // Animation
    // -------------------------------------------------------------------------

    protected override void TriggerAbilityAnimation(Mb_CharacterBase user)
    {
        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.TriggerEAbility();
    }
}
