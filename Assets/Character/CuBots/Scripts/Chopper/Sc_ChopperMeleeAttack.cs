using UnityEngine;

public class Sc_ChopperMeleeAttack : Sc_BaseAbility
{
    private float _AttackRange = 1.8f;
    private float _Damage;

    public Sc_ChopperMeleeAttack(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user)
    {
        _Cooldown = 1f / user.AttackSpeed.Value( );
        _Damage = _AbilityData.GetStat("Damage", _currentAbilityLevel, user.AttackPower.Value( ));
    }

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown( )) return;

        Collider[] hits = Physics.OverlapSphere(
            user.transform.position,
            _AttackRange
        );

        foreach (Collider hit in hits)
        {
            I_Damageable damageable = hit.GetComponent<I_Damageable>( );
            if (damageable != null && !hit.gameObject.CompareTag("CuBot"))
            {
                damageable.TakeDamage(_Damage);
                break; // Single-target melee
            }
        }

        _CooldownRemaining = _Cooldown;
        user.StartCoroutine(RefreshCooldown( ));
    }

}
