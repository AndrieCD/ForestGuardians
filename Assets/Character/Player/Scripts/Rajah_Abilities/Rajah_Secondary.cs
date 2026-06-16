// Rajah_Secondary.cs
// [RMB] Feather Shot — fires a single feather projectile toward the crosshair.
// Uses AttackSpeed for cooldown (basic attack, not an ability cooldown).
// Scales damage with ATK at current ability level.
//
// PASSIVE TRIGGER CHANGE:
//   Previously, Passive_Ability.RaiseBasicAttackHit() was called at fire time —
//   meaning the passive stacked even if the projectile missed.
//   It is now subscribed to Mb_Projectile.OnHit so it only fires on a confirmed hit.
//
// PROJECTILE SYSTEM CHANGE:
//   Previously used Instantiate() + three setup calls (SetDamageAmount, SetOwnerTag, SetOwner).
//   Now delegates all spawning to Mb_ProjectileLauncher.Fire() — one call, no setup boilerplate.
//
// OnSecondaryFired EVENT CHANGE:
//   The third parameter was previously Vector3 targetPoint (a world position).
//   It is now Vector3 direction (a normalized vector) so Rajah_R_Branch2 can pass it
//   directly to _launcher.FireToward() without recalculating a direction from a position.

using System;
using UnityEngine;

public class Rajah_Secondary : Sc_BaseAbility
{
    // SO_ProjectileData asset for the basic feather shot.
    // Assigned in the constructor from SO_Guardian — ability scripts never
    // reference a specific prefab directly; the data asset owns that reference.
    private SO_ProjectileData _projectileData;
    private GameObject _projectilePrefab;


    // Cached launcher reference — fetched once in OnEquip from the owner GameObject.
    // Mb_ProjectileLauncher handles all spawn, position, orient, and initialize logic.
    private Mb_ProjectileLauncher _launcher;

    // Fired after every shot so Rajah_R_Branch2 (Eagle Eye passive) can react.
    // Parameters: (source character, launch origin, normalized fire direction)
    // Direction is normalized so FireToward() in R_Branch2 can use it directly.
    public static event Action<Mb_CharacterBase, Vector3, Vector3> OnSecondaryFired;


    public Rajah_Secondary(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user)
    {
        // ProjectileData is read from the SO so the prefab reference lives in one place.
        // TODO: Add a ProjectileData field to SO_Ability and assign Rajah_Feather_Basic here:
        //   _projectileData = abilityData.ProjectileData;
        // For now, this is left null and guarded in FireProjectile() until the SO is updated.
        _projectileData = abilityData.ProjectileData;
    }


    public override void OnEquip(Mb_CharacterBase user)
    {
        // Cache the launcher from the owner's GameObject.
        // Mb_ProjectileLauncher must be attached to the same GameObject as the Guardian.
        _launcher = user.GetComponent<Mb_ProjectileLauncher>();

        // Fetch the generic projectile prefab from the registry
        Mb_AbilityPrefabRegistry registry = user.GetComponent<Mb_AbilityPrefabRegistry>();
        _projectilePrefab = registry?.GetPrefab(AbilityPrefabID.Rajah_FeatherProjectile);


        if (_launcher == null)
            Debug.LogError("[Rajah_Secondary] No Mb_ProjectileLauncher found on " +
                           $"{user.gameObject.name}. Add the component to the Guardian prefab.");

        Debug.Log($"Equipped {_AbilityData.AbilityName}.");
    }


    public override void OnUnequip(Mb_CharacterBase user)
    {
        Debug.Log($"Unequipped {_AbilityData.AbilityName}.");
    }


    // Called when the player presses [RMB]
    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        var controller = _User as Mb_PlayerController;
        controller?.AddDisable(
            ActionDisableFlags.AllAttacks
        );

        Mb_AudioManager.PlaySFX(CombatSFX.Rajah_Secondary_Swing,
                                user.gameObject.transform.position);

        FireProjectile(user);
        TriggerAbilityAnimation(user);

        // Secondary is a basic attack — cooldown driven by AttackSpeed, not Haste
        StartCooldown(user, GetAttackCooldown(user));
    }


    private void FireProjectile(Mb_CharacterBase user)
    {
        if (_launcher == null)
        {
            Debug.LogError("[Rajah_Secondary] Cannot fire — Mb_ProjectileLauncher is null.");
            return;
        }

        if (_projectileData == null)
        {
            Debug.LogError("[Rajah_Secondary] Cannot fire — SO_ProjectileData is null. " +
                           "Assign Rajah_Feather_Basic to SO_Ability.ProjectileData.");
            return;
        }

        // Calculate damage at fire time using current stats.
        // Crit is rolled here — the rolled value is passed into Fire() as baseDamage
        // so the projectile carries the final damage number without needing stat access.
        float damage = _AbilityData.GetStat(
            "Damage", CurrentLevel,
            user.Stats.AttackPower.GetValue()
        );
        damage = ApplyCriticalStrike(damage, user);

        // Fire() resolves aim via camera raycast (Guardian path) and returns the
        // Mb_Projectile instance so we can subscribe to OnHit immediately after.
        Mb_Projectile projectile = _launcher.Fire(_projectilePrefab, _projectileData, user, damage);

        if (projectile == null) return;

        // Store the fire direction for OnSecondaryFired — read from the projectile's
        // forward direction immediately after Fire() sets it, before any physics runs.
        Vector3 fireDirection = projectile.transform.forward;
        Vector3 launchOrigin = projectile.transform.position;

        // Subscribe to OnHit so the passive only stacks on a confirmed hit.
        // Previously, RaiseBasicAttackHit() was called unconditionally at fire time —
        // this means missed shots no longer grant stacks.
        projectile.OnHit += (target, hitPoint, hitNormal) =>
        {
            Passive_Ability.RaiseBasicAttackHit();
        };

        // Notify Eagle Eye (Rajah_R_Branch2) that a secondary was fired.
        // Passes normalized direction so the passive can call FireToward() directly
        // without needing to reconstruct a direction from a world position.
        OnSecondaryFired?.Invoke(user, launchOrigin, fireDirection);
    }


    protected override void TriggerAbilityAnimation(Mb_CharacterBase user)
    {
        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.TriggerSecondaryAttack();
    }
}