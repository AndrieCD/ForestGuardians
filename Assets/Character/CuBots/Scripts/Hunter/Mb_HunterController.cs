using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.UI.Image;

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
public class Mb_HunterController : Mb_CuBotController
{
    // When true, Chopper is in the middle of its attack windup — movement is frozen
    private bool _isWindingUp = false;

    [SerializeField] private LayerMask _LineOfSightMask;
    [SerializeField] private Transform _FirePoint;

    protected override void AssignAbilities()
    {
        Abilities.SetPrimarySlot(new Sc_HunterRangeAttack(
            _CuBotTemplate.PrimaryAttack,
            this,
            onWindupStart: () => _isWindingUp = true,   // Freeze movement
            onWindupEnd: () => _isWindingUp = false   // Unfreeze movement
        ));
    }

    protected override void OnInAttackRange()
    {
        if (_isWindingUp)
            return;

        if (!HasLineOfSight())
        {
            _Agent.isStopped = false;
            return;
        }

        transform.LookAt(_CurrentTarget);

        TryUsePrimaryAttack();
    }

    protected override void UpdateAnimator()
    {
        if (_Animator == null) return;

        // TODO: Drive locomotion blend tree parameter (e.g. "Speed") once rig is ready
        // TODO: Trigger windup/attack animation from within Sc_ChopperMeleeAttack
        // Example: _Animator.SetFloat("Speed", _Agent.velocity.magnitude);
    }

    protected override void OnControllerReset()
    {
        // Clear the windup flag so pool-reused Choppers don't start frozen
        _isWindingUp = false;
    }

    private bool HasLineOfSight()
    {
        if (_CurrentTarget == null)
            return false;

        Vector3 origin = _FirePoint.position;
        Vector3 targetPos = _CurrentTarget.position + Vector3.up * 1.0f;

        Vector3 direction = targetPos - origin;
        float distance = direction.magnitude;

        Debug.DrawRay(
            origin,
            direction.normalized * distance,
            Color.red,
            0.1f
        );

        if (Physics.Raycast(
                origin,
                direction.normalized,
                out RaycastHit hit,
                distance))
        {
            Debug.Log($"Line of sight check hit: {hit.transform.name}");
            return hit.transform == _CurrentTarget;
        }

        return false;
    }

}