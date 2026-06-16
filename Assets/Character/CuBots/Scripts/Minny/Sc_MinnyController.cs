using UnityEngine;

/// <summary>
/// Chopper — fast melee CuBot. Charges toward its target and delivers
/// a single axe slash with a short windup before the hit lands.
///
/// Inherits targeting, movement, and pool reset logic from Mb_CuBotController.
///
/// Inspector setup:
///   - Assign SO_CuBots template in the Inspector (inherited field)
///   - NavMeshAgent must be on the same GameObject
///   - Animator must be on the same GameObject (optional — no hard error if missing)
/// </summary>
public class Mb_MinnyController : Mb_CuBotController
{

    protected override void AssignAbilities()
    {
        Abilities.SetPrimarySlot(new Sc_MinnyMeleeAttack(
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