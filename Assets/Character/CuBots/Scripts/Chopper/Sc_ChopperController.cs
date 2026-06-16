using System.Collections;
using UnityEngine;

/// <summary>
/// Chopper — fast melee CuBot. Charges toward its target and delivers
/// an axe slash when in range.
///
/// Inspector setup:
///   - Assign SO_CuBots template in the Inspector
///   - NavMeshAgent must be on the same GameObject
///   - Animator / Mb_CuBotAnimator optional but expected for visual feedback
/// </summary>
public class Mb_ChopperController : Mb_CuBotController
{
    protected override void AssignAbilities()
    {
        Abilities.SetPrimarySlot(new Sc_ChopperMeleeAttack(
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