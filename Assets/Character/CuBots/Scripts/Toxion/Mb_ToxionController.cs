using UnityEngine;

/// <summary>
/// Toxion - tanky poison miniboss CuBot that lobs toxic sludge at medium range.
/// </summary>
public class Mb_ToxionController : Mb_CuBotController
{
    protected override void AssignAbilities()
    {
        if (_CuBotTemplate.PrimaryAttack == null)
        {
            Debug.LogError("[Mb_ToxionController] PrimaryAttack SO_Ability is not assigned on the SO_CuBots template.");
            return;
        }

        Abilities.SetPrimarySlot(new Sc_ToxionSludgeShot(
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

        float normalizedSpeed = _CuBotTemplate.MoveSpeed > 0f
            ? _Agent.velocity.magnitude / _CuBotTemplate.MoveSpeed
            : _Agent.velocity.magnitude;

        _BasicCuBotAnimator.SetSpeed(normalizedSpeed);
    }
}
