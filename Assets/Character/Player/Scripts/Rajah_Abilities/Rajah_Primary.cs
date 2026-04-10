using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [LMB] Feathery Slash — Rajah slashes in a small area in front of him,
/// hitting every enemy within range. Scales with ATK. Supports critical strikes.
/// </summary>
public class Rajah_Primary : Sc_BaseAbility
{
    // How far in front of Rajah the slash reaches
    private float _slashRange = 2.0f;

    // How wide the slash is — an OverlapSphere covers a circle, so this acts as the cone radius
    // TODO: Swap to OverlapBox or a cone-shaped check if a precise frontal cone is needed later
    private float _slashRadius = 1.5f;

    public Rajah_Primary(SO_Ability abilityObject, Mb_CharacterBase user) : base(abilityObject, user)
    {
        // Cooldown is driven by Attack Speed: faster AS = shorter time between swings
        // e.g. AS of 1.0 = 1 swing/sec, AS of 2.0 = 0.5 sec cooldown
        _Cooldown = 1f / user.Stats.AttackSpeed.Value( );
    }

    // Called when Rajah spawns
    public override void OnEquip(Mb_CharacterBase user)
    {
        Debug.Log($"{user.name} has equipped {_AbilityData.AbilityName}.");
    }

    // Called when the player clicks LMB
    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        PerformSlash(user);
        StartCooldown(user);

        PlayAbilityAnimation(user);
    }

    private static void PlayAbilityAnimation(Mb_CharacterBase user)
    {
        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.TriggerPrimaryAttack();
    }

    // Called when Rajah dies or swaps abilities
    public override void OnUnequip(Mb_CharacterBase user)
    {
        Debug.Log($"{user.name} has unequipped {_AbilityData.AbilityName}.");
    }

    // -------------------------------------------------------

    // Finds all enemies in a small area in front of Rajah and deals damage to each one.
    private void PerformSlash(Mb_CharacterBase user)
    {
        // The center of the slash hitbox is placed in front of Rajah, not at his feet
        Vector3 slashCenter = user.transform.position + user.transform.forward * _slashRange;

        // Find all colliders within the slash area
        Collider[] hitColliders = Physics.OverlapSphere(slashCenter, _slashRadius);

        // Track who we've already damaged this swing, in case an enemy has multiple colliders
        HashSet<MB_CuBotBase> alreadyHit = new HashSet<MB_CuBotBase>( );

        foreach (Collider col in hitColliders)
        {
            MB_CuBotBase cuBot = col.GetComponent<MB_CuBotBase>( );

            // Skip if not an enemy, or if we already hit this same enemy
            if (cuBot == null || alreadyHit.Contains(cuBot)) continue;

            // Calculate damage — Feathery Slash scales 100% ATK
            float damage = _AbilityData.GetStat("Damage", _currentAbilityLevel, user.Stats.AttackPower.Value( ));

            // Roll for critical strike
            damage = ApplyCriticalStrike(damage, user);

            cuBot.Health.TakeDamage(damage);
            alreadyHit.Add(cuBot);

            Debug.Log($"Feathery Slash hit {cuBot.name} for {damage} damage.");
        }
    }

    // Rolls a random number against the user's crit chance.
    // On a crit, multiplies damage by CriticalDamage stat.
    // Returns the final damage value (either normal or crit).
    private float ApplyCriticalStrike(float baseDamage, Mb_CharacterBase user)
    {
        // CriticalChance is stored as a percentage (e.g. 10 = 10%), so divide by 100
        float critChance = user.Stats.CriticalChance.Value( ) / 100f;
        float roll = Random.value; // Random float between 0.0 and 1.0

        if (roll <= critChance)
        {
            float critMultiplier = user.Stats.CriticalDamage.Value( ) / 100f;
            Debug.Log($"Critical Strike! Multiplier: {critMultiplier}x");
            return baseDamage * critMultiplier;
        }

        return baseDamage;
    }
}