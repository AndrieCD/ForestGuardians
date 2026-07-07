using UnityEngine;

/// <summary>
/// Sawyer - melee CuBot that uses a simple close-range attack.
/// </summary>
public class Mb_SawyerController : Mb_CuBotController
{
    protected override void AssignAbilities()
    {
        Abilities.SetPrimarySlot(new Sc_SawyerMeleeAttack(
            _CuBotTemplate.PrimaryAttack,
            this
        ));
    }

    protected override void OnInAttackRange()
    {
        if (_CurrentTarget != null)
            transform.LookAt(_CurrentTarget);

        StartCoroutine(MeleeAttackCoroutine());
    }

    protected override void UpdateAnimator()
    {
        if (_BasicCuBotAnimator == null) return;

        _BasicCuBotAnimator.SetSpeed(_Agent.velocity.magnitude);
    }
}
