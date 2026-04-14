// Rajah_Primary.cs
// [LMB] Feathery Slash — Rajah slashes a small area in front of him,
// hitting every enemy within range. Scales with ATK. Supports critical strikes.
//
// Also fires Passive_Ability.OnBasicAttackHit for each enemy hit so Royal Plumage
// can grant stacks — Primary doesn't know about the passive directly.

using System.Collections.Generic;
using UnityEngine;

public class Rajah_Primary : Sc_BaseAbility
{
    // How far in front of Rajah the slash center is placed
    private const float SLASH_RANGE = 2.0f;

    // Radius of the overlap sphere around the slash center
    // TODO: Swap to OverlapBox or a cone check if a precise frontal cone is needed later
    private const float SLASH_RADIUS = 1.5f;


    public Rajah_Primary(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user) { }


    public override void OnEquip(Mb_CharacterBase user)
    {
        Debug.Log($"[{user.name}] Equipped {_AbilityData.AbilityName}.");
    }

    public override void OnUnequip(Mb_CharacterBase user)
    {
        Debug.Log($"[{user.name}] Unequipped {_AbilityData.AbilityName}.");
    }


    // Called when the player clicks [LMB]
    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        PerformSlash(user);
        TriggerAbilityAnimation(user);

        // Primary is a basic attack — cooldown is driven by AttackSpeed, not Haste
        StartCooldown(user, GetAttackCooldown(user));
    }


    private void PerformSlash(Mb_CharacterBase user)
    {
        // Place the hitbox in front of Rajah, not at his feet
        Vector3 slashCenter = user.transform.position + user.transform.forward * SLASH_RANGE;
        Collider[] hitColliders = Physics.OverlapSphere(slashCenter, SLASH_RADIUS);

        // Track already-hit enemies in case one has multiple colliders
        HashSet<MB_CuBotBase> alreadyHit = new HashSet<MB_CuBotBase>();

        bool hitAnyEnemy = false;

        foreach (Collider col in hitColliders)
        {
            MB_CuBotBase cuBot = col.GetComponent<MB_CuBotBase>();
            if (cuBot == null || alreadyHit.Contains(cuBot)) continue;

            float damage = _AbilityData.GetStat("Damage", CurrentLevel, user.Stats.AttackPower.GetValue());

            // ApplyCriticalStrike is inherited from Sc_BaseAbility
            damage = ApplyCriticalStrike(damage, user);

            cuBot.Health.TakeDamage(damage);
            alreadyHit.Add(cuBot);
            hitAnyEnemy = true;

            Debug.Log($"[Feathery Slash] Hit {cuBot.name} for {damage} damage.");
        }

        // Notify the passive that a basic attack connected — it handles stack logic
        // We fire once per activation (not per enemy hit) so one swing = one stack
        if (hitAnyEnemy)
            Passive_Ability.RaiseBasicAttackHit();
    }


    protected override void TriggerAbilityAnimation(Mb_CharacterBase user)
    {
        if (user is Mb_GuardianBase guardian)
            guardian.GuardianAnimator?.TriggerPrimaryAttack();
    }
}