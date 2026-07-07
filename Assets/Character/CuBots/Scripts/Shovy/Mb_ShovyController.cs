using UnityEngine;

/// <summary>
/// Shovy - short-ranged CuBot that lobs a gravity-affected dirt projectile.
/// </summary>
public class Mb_ShovyController : Mb_CuBotController
{
    protected override void AssignAbilities()
    {
        Abilities.SetPrimarySlot(new Sc_ShovyDirtShot(
            _CuBotTemplate.PrimaryAttack,
            this,
            getCurrentTarget: () => _CurrentTarget
        ));
    }

    protected override void OnInAttackRange()
    {
        if (_CurrentTarget != null)
            transform.LookAt(_CurrentTarget);

        TryUsePrimaryAttack();
    }

    protected override void UpdateAnimator()
    {
        if (_BasicCuBotAnimator == null) return;

        _BasicCuBotAnimator.SetSpeed(_Agent.velocity.magnitude);
    }
}
