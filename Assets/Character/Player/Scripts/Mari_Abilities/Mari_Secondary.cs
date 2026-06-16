// Mari_Secondary.cs
// [RMB] Psyshock — Mari's secondary attack.
//
// BEHAVIOR:
//   Fires a single psychic energy bolt toward the crosshair.
//   Damages the first enemy hit (non-piercing — set IsPiercing = false on
//   SO_ProjectileData asset "Mari_PsyshockBolt").
//
// COOLDOWN:
//   Uses AttackSpeed via GetAttackCooldown() — basic attack, not an ability.
//   AllAttacks is disabled at fire time and re-enabled when the cooldown
//   expires (handled in Sc_BaseAbility.TickCooldown via RemoveDisable).
//
// PASSIVE INTERACTION:
//   RaiseBasicAttackHit() called on confirmed hit — grants one Mental Overflow
//   stack if the bolt connects with an enemy.
//
// BRANCH 2 INTERACTION:
//   OnSecondaryFired fires after every shot so Mari_R_Branch2 (Mind Unbound)
//   can react with its splash damage passive without holding a direct reference
//   to this ability. Passes (source, launchOrigin, normalizedDirection) —
//   identical signature to Rajah_Secondary.OnSecondaryFired so the pattern
//   is consistent and Branch2 can follow the same structure.
//
// VFX:
//   "PsyshockMuzzleVFX" — child ParticleSystem on Mari's prefab, plays on fire.
//   TODO: Leo — add a short burst particle system named "PsyshockMuzzleVFX"
//         at Mari's ProjectileOrigin child (e.g. a spark / psychic flash).

using System;
using UnityEngine;

public class Mari_Secondary : Sc_BaseAbility
{
    // -------------------------------------------------------------------------
    // Events
    // -------------------------------------------------------------------------

    // Fired after every Psyshock shot.
    // Mari_R_Branch2 subscribes to this for its splash damage passive.
    // Parameters: (source character, launch world position, normalized fire direction)
    // Normalized direction matches the pattern established by Rajah_Secondary
    // so Branch2 can call FireToward() directly without recalculating direction.
    public static event Action<Mb_CharacterBase, Vector3, Vector3> OnSecondaryFired;


    // -------------------------------------------------------------------------
    // Cached References
    // -------------------------------------------------------------------------

    private Mb_ProjectileLauncher _launcher;
    private SO_ProjectileData _projectileData;
    private GameObject _projectilePrefab;

    // Muzzle flash VFX — located by name in OnEquip
    private ParticleSystem _muzzleVFX;


    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public Mari_Secondary(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user)
    {
        _projectileData = abilityData.ProjectileData;
    }


    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public override void OnEquip(Mb_CharacterBase user)
    {
        _launcher = user.GetComponent<Mb_ProjectileLauncher>();

        if (_launcher == null)
            Debug.LogError("[Mari_Secondary] No Mb_ProjectileLauncher found on " +
                           $"{user.gameObject.name}.");

        // Fetch the generic projectile prefab from the registry
        Mb_AbilityPrefabRegistry registry = user.GetComponent<Mb_AbilityPrefabRegistry>();
        _projectilePrefab = registry?.GetPrefab(AbilityPrefabID.Mari_PsyshockBoltProjectile);

        // Locate muzzle VFX by name in Mari's prefab hierarchy
        foreach (ParticleSystem ps in user.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject.name == "PsyshockMuzzleVFX")
            {
                _muzzleVFX = ps;
                break;
            }
        }

        Debug.Log($"[Mari_Secondary] Equipped {_AbilityData.AbilityName}.");
    }


    public override void OnUnequip(Mb_CharacterBase user)
    {
        Debug.Log($"[Mari_Secondary] Unequipped {_AbilityData.AbilityName}.");
    }


    // -------------------------------------------------------------------------
    // Activation
    // -------------------------------------------------------------------------

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        // Block further attack input until the attack cooldown expires.
        // Sc_BaseAbility.TickCooldown() calls RemoveDisable(AllAttacks)
        // automatically when _CooldownRemaining hits zero.
        var controller = user as Mb_PlayerController;
        controller?.AddDisable(ActionDisableFlags.AllAttacks);

        // Play muzzle flash
        _muzzleVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _muzzleVFX?.Play();

        TriggerAbilityAnimation(user);
        FireProjectile(user);

        // Psyshock is a basic attack — cooldown driven by AttackSpeed
        StartCooldown(user, GetAttackCooldown(user));
    }


    // -------------------------------------------------------------------------
    // Fire
    // -------------------------------------------------------------------------

    private void FireProjectile(Mb_CharacterBase user)
    {
        if (_launcher == null)
        {
            Debug.LogError("[Mari_Secondary] Cannot fire — launcher is null.");
            return;
        }

        if (_projectileData == null)
        {
            Debug.LogError("[Mari_Secondary] Cannot fire — ProjectileData is null. " +
                           "Assign Mari_PsyshockBolt to SO_Ability.ProjectileData.");
            return;
        }

        // Psyshock scales with AbilityPower — psychic bolt, not a physical attack.
        // Crit is rolled here so the projectile carries the final resolved damage.
        float damage = _AbilityData.GetStat(
            "Damage",
            CurrentLevel,
            user.Stats.AttackPower.GetValue(),
            user.Stats.AbilityPower.GetValue()
        );
        damage = ApplyCriticalStrike(damage, user);

        // Fire() resolves aim via camera center raycast internally and returns
        // the Mb_Projectile instance for immediate post-fire subscriptions.
        Mb_Projectile projectile = _launcher.Fire(_projectilePrefab, _projectileData, user, damage);

        if (projectile == null) return;

        // Read fire direction and origin from the projectile immediately after
        // Fire() sets them — before any physics runs this frame.
        Vector3 fireDirection = projectile.transform.forward;
        Vector3 launchOrigin = projectile.transform.position;

        // Subscribe to OnHit for confirmed-hit passive stack grant.
        // Psyshock is non-piercing so OnHit fires at most once per shot.
        projectile.OnHit += (target, hitPoint, hitNormal) =>
        {
            // Grant one Mental Overflow stack on confirmed hit
            Passive_Ability.RaiseBasicAttackHit();
        };

        // Notify Mari_R_Branch2 that a Psyshock was fired.
        // Branch2's passive reacts to this with splash damage around the hit target.
        // We fire this regardless of whether Branch2 is equipped — the null
        // subscriber list is a no-op, so no guard is needed here.
        OnSecondaryFired?.Invoke(user, launchOrigin, fireDirection);
    }


    // -------------------------------------------------------------------------
    // Animation
    // -------------------------------------------------------------------------

    protected override void TriggerAbilityAnimation(Mb_CharacterBase user)
    {
        // TODO: Mb_GuardianAnimator needs a dedicated Mari Secondary trigger.
        // For now, reuse TriggerSecondaryAttack() so animation doesn't silently fail.
        // Replace with a Mari-specific call (e.g. TriggerMariSecondary()) once
        // Angel rigs the correct animation state in the Animator Controller.
        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.TriggerSecondaryAttack();
    }
}