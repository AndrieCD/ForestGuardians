using UnityEngine;

public class Sc_ChopperMeleeAttack : Sc_BaseAbility
{
    private float _AttackRange = 1.8f;
    private float _attackRadius = 1.0f; // Radius for the overlap sphere
    private float _Damage;

    public Sc_ChopperMeleeAttack(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user)
    {}

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown( )) return;


        Vector3 slashCenter = user.transform.position + user.transform.forward * _AttackRange;
        
        Collider[] hits = Physics.OverlapSphere(
            slashCenter, _attackRadius
        );

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == user.gameObject) continue; // Skip self

            I_Damageable damageable = hit.GetComponent<I_Damageable>( );
            if (damageable != null && !hit.gameObject.CompareTag("CuBot"))
            {
                float damage = _AbilityData.GetStat("Damage", CurrentLevel, user.Stats.AttackPower.GetValue());

                damageable.TakeDamage(damage);
                break; // Single-target melee
            }
        }

        StartCooldown(user, GetAttackCooldown(user));
    }

}
