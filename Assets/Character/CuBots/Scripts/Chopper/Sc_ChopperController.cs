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
public class Mb_ChopperController : Mb_CuBotController
{
    // When true, Chopper is in the middle of its attack windup — movement is frozen
    private bool _isWindingUp = false;

    protected override void AssignAbilities()
    {
        Abilities.SetPrimarySlot(new Sc_ChopperMeleeAttack(
            _CuBotTemplate.PrimaryAttack,
            this,
            onWindupStart: () => _isWindingUp = true,   // Freeze movement
            onWindupEnd: () => _isWindingUp = false   // Unfreeze movement
        ));
    }

    protected override void OnInAttackRange()
    {
        // Don't queue another attack if already winding up
        if (_isWindingUp) return;

        // Face the target before swinging
        if (_CurrentTarget != null)
            transform.LookAt(_CurrentTarget);

        TryUsePrimaryAttack();
    }

    protected override void UpdateAnimator()
    {
        if (_BasicCuBotAnimator == null) return;

        _BasicCuBotAnimator.SetSpeed(_Agent.velocity.magnitude);
    }


    protected override void OnControllerReset()
    {
        // Clear the windup flag so pool-reused Choppers don't start frozen
        _isWindingUp = false;
    }
}