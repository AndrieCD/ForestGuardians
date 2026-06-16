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

        if (!HasLineOfSight())
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

    /// <summary>
    /// Checks whether Hunter has an unobstructed line of sight to the current target.
    /// Casts against ALL layers and checks if the first thing hit belongs to the target.
    /// This correctly handles targets on any layer — no mask configuration needed.
    /// </summary>
    private bool HasLineOfSight()
    {
        if (_CurrentTarget == null || _FirePoint == null) return false;

        Vector3 origin = _FirePoint.position;

        // Aim at chest height of target to avoid floor clips
        Vector3 targetPos = _CurrentTarget.position + Vector3.up * 1.0f;
        Vector3 direction = (targetPos - origin).normalized;
        float distance = Vector3.Distance(origin, targetPos);

        Debug.DrawRay(origin, direction * distance, Color.red, 0.1f);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, distance))
        {
            // Accept the hit if it belongs to the target's GameObject or any of its children.
            // IsChildOf returns true even when called on the root itself, so this covers
            // both the root collider and any child colliders on large objects like Panoharra.
            return hit.transform.IsChildOf(_CurrentTarget) || hit.transform == _CurrentTarget;
        }

        // Nothing blocked the ray — clear line of sight
        return true;
    }
}