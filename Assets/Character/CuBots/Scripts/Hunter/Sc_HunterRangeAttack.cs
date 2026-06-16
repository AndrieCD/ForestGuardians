// Sc_HunterRangeAttack.cs
// Hunter CuBot's ranged attack — fires a single bolt toward the current target
// immediately on activation. No windup.
//
// Uses Mb_ProjectileLauncher.FireToward() so all spawn, orient, and initialize
// logic stays in the launcher — this script only resolves the aim direction
// and calculates damage.
//
// The target getter delegate is passed in from Mb_HunterController so aim
// always reflects the live _CurrentTarget, not a stale cached reference.
// This means Hunter correctly shoots at both the Player and the Panoharra
// depending on its current aggro state.

using UnityEngine;

public class Sc_HunterRangeAttack : Sc_BaseAbility
{
    // SO_ProjectileData asset for Hunter's bolt — assign in the SO_Ability Inspector.
    // Drives launch speed, max range, piercing, and any on-hit effects (e.g. knockback).
    private readonly SO_ProjectileData _projectileData;
    private GameObject _projectilePrefab;


    // Launcher component on the Hunter's GameObject — handles all spawn logic.
    private readonly Mb_ProjectileLauncher _launcher;

    // Delegate returning the controller's live _CurrentTarget each shot —
    // avoids a stale player reference when Hunter switches back to Panoharra.
    private readonly System.Func<Transform> _getCurrentTarget;


    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public Sc_HunterRangeAttack(
        SO_Ability abilityData,
        Mb_CharacterBase user,
        System.Func<Transform> getCurrentTarget)
        : base(abilityData, user)
    {
        _getCurrentTarget = getCurrentTarget;

        // Cache the launcher from the Hunter's own GameObject.
        // Constructor runs inside AssignAbilities() which is called from
        // InitializeFromTemplate() — the MonoBehaviour is fully initialized here.
        _launcher = user.GetComponent<Mb_ProjectileLauncher>();

        if (_launcher == null)
            Debug.LogError("[Sc_HunterRangeAttack] No Mb_ProjectileLauncher found on " +
                           $"{user.gameObject.name}. Add the component to the Hunter prefab.");

        // Cache the projectile data from the SO — one source of truth.
        // SO_Ability.ProjectileData must be assigned in the Inspector.
        _projectileData = abilityData.ProjectileData;

        // Fetch the generic projectile prefab from the registry
        Mb_AbilityPrefabRegistry registry = user.GetComponent<Mb_AbilityPrefabRegistry>();
        _projectilePrefab = registry?.GetPrefab(AbilityPrefabID.Hunter_BulletProjectile);

        if (_projectileData == null)
            Debug.LogError("[Sc_HunterRangeAttack] SO_Ability.ProjectileData is null on " +
                           $"{abilityData.AbilityName}. Assign a SO_ProjectileData asset.");
    }


    // -------------------------------------------------------------------------
    // Activation
    // -------------------------------------------------------------------------

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        Shoot(user);

        StartCooldown(user, GetAttackCooldown(user));
    }


    // -------------------------------------------------------------------------
    // Shoot
    // -------------------------------------------------------------------------

    private void Shoot(Mb_CharacterBase user)
    {
        if (_launcher == null || _projectileData == null) return;

        Transform currentTarget = _getCurrentTarget?.Invoke();

        if (currentTarget == null)
        {
            Debug.LogWarning("[Sc_HunterRangeAttack] No current target — aborting shot.");
            return;
        }

        // Aim at chest height to avoid floor clips on level terrain.
        // The +1.0f Y offset puts the aim point at roughly torso level for
        // both Guardians and the Panoharra's base object.
        Vector3 targetPos = currentTarget.position + Vector3.up * 1.0f;
        Vector3 fireDirection = (targetPos - user.transform.position).normalized;

        // Damage is calculated at fire time — crit is rolled here and locked in.
        float damage = _AbilityData.GetStat(
            "Damage",
            CurrentLevel,
            user.Stats.AttackPower.GetValue()
        );
        damage = ApplyCriticalStrike(damage, user);

        // FireToward() handles spawn, orientation, and Initialize() —
        // the returned projectile is ready and already in flight.
        _launcher.FireToward(_projectilePrefab, _projectileData, user, damage, fireDirection);
    }
}