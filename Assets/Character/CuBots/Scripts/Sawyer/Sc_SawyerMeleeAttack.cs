using UnityEngine;

/// <summary>
/// Sawyer's melee attack - an immediate single-target close-range hit.
/// </summary>
public class Sc_SawyerMeleeAttack : Sc_BaseAbility
{
    private const float ATTACK_RANGE = 1.8f;
    private const float ATTACK_RADIUS = 1.0f;

    public Sc_SawyerMeleeAttack(SO_Ability abilityData, Mb_CharacterBase user)
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
        Vector3 slashCenter = user.transform.position
            + Vector3.up * 1.0f
            + user.transform.forward * ATTACK_RANGE;

        Collider[] hits = Physics.OverlapSphere(slashCenter, ATTACK_RADIUS);

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == user.gameObject) continue;
            if (hit.gameObject.CompareTag("CuBot")) continue;

            I_Damageable damageable = hit.GetComponent<I_Damageable>();
            if (damageable == null) continue;

            float damage = _AbilityData.GetStat(
                "Damage",
                CurrentLevel,
                user.Stats.AttackPower.GetValue()
            );

            damage = ApplyCriticalStrike(damage, user);
            damageable.TakeDamage(damage);
            break;
        }
    }
}
