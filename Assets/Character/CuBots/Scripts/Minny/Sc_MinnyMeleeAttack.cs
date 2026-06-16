using System.Collections;
using UnityEngine;

/// <summary>
/// Chopper's melee attack — a short-range axe slash.
///
/// WINDUP FLOW:
///   1. CheckCooldown() — bail if still on CD
///   2. Signal controller to freeze movement (via callback)
///   3. Wait WindupDuration seconds (animation plays)
///   4. Apply OverlapSphere damage hit
///   5. Signal controller to resume movement
///   6. Start cooldown
/// </summary>
public class Sc_MinnyMeleeAttack : Sc_BaseAbility
{
    private const float _AttackRange = 1.8f;

    // Radius of the overlap sphere used to find hit targets
    private const float ATTACK_RADIUS = 1.0f;


    public Sc_MinnyMeleeAttack(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user) { }

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        user.GetComponent<Mb_CuBotAnimator>()?.TriggerAttack();

        ApplyHit(user);

        StartCooldown(user, GetAttackCooldown(user));
    }

    private void ApplyHit(Mb_CharacterBase user)
    {
        // The slash origin is slightly in front of Chopper
        Vector3 slashCenter = user.transform.position + (Vector3.up * 1.0f) + (user.transform.forward * _AttackRange);

        Collider[] hits = Physics.OverlapSphere(slashCenter, ATTACK_RADIUS);

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == user.gameObject) continue; // Never hit yourself
            if (hit.gameObject.CompareTag("CuBot")) continue; // CuBots don't hurt each other

            I_Damageable damageable = hit.GetComponent<I_Damageable>();
            if (damageable != null)
            {
                float damage = _AbilityData.GetStat(
                    "Damage",
                    CurrentLevel,
                    user.Stats.AttackPower.GetValue()
                );

                damage = ApplyCriticalStrike(damage, user);
                damageable.TakeDamage(damage);
                break; // Single target — stop after first valid hit
            }
        }
    }
}