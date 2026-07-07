using UnityEngine;

/// <summary>
/// Hunter — ranged glass cannon CuBot. Stops at attack range and fires
/// a bolt toward its current target. Requires line of sight to fire.
///
/// Inspector setup:
///   - Assign SO_CuBots template
///   - Assign _FirePoint (a child Transform at Hunter's weapon/eye level)
///   - _LineOfSightMask is removed — raycast now hits all layers and checks
///     whether the FIRST thing hit belongs to the current target
///   - NavMeshAgent must be on the same GameObject
/// </summary>
public class Mb_HunterController : Mb_CuBotController
{
    [SerializeField] private Transform _FirePoint;

    protected override bool RequiresLineOfSightToAttack => true;

    protected override Transform AttackLineOfSightOrigin => _FirePoint != null ? _FirePoint : transform;

    protected override void AssignAbilities()
    {
        Abilities.SetPrimarySlot(new Sc_HunterRangeAttack(
            _CuBotTemplate.PrimaryAttack,
            this,
            getCurrentTarget: () => _CurrentTarget
        ));
    }

    protected override void OnInAttackRange()
    {
        if (_CurrentTarget != null)
            transform.LookAt(_CurrentTarget);

        if (!HasLineOfSightToCurrentTarget())
        {
            // LOS blocked — resume movement so Hunter repositions
            // rather than freezing in place
            _Agent.isStopped = false;
            return;
        }

        TryUsePrimaryAttack();
    }

    protected override void UpdateAnimator()
    {
        if (_BasicCuBotAnimator == null) return;

        _BasicCuBotAnimator.SetSpeed(_Agent.velocity.magnitude);
    }

}
