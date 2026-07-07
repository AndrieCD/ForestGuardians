using System.Collections;
using UnityEngine;

/// <summary>
/// Trapper - ranged CuBot that fires slowing projectiles and periodically drops proximity traps.
/// </summary>
public class Mb_TrapperController : Mb_CuBotController
{
    private const float TRAP_DROP_INTERVAL = 10.0f;
    private const float TRAP_LIFETIME = 8.0f;
    private const float TRAP_FLOAT_HEIGHT = 0.35f;
    private const float TRAP_DAMAGE = 300.0f;
    private const float TRAP_SLOW_PERCENT = 75.0f;
    private const float TRAP_SLOW_DURATION = 1.0f;

    [SerializeField] private Transform _FirePoint;

    private Coroutine _trapDropRoutine;

    protected override bool RequiresLineOfSightToAttack => true;

    protected override Transform AttackLineOfSightOrigin => _FirePoint != null ? _FirePoint : transform;

    protected override void AssignAbilities()
    {
        Abilities.SetPrimarySlot(new Sc_TrapperRangeAttack(
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

    protected override void OnControllerReset()
    {
        RestartTrapRoutine();
    }

    private void OnDisable()
    {
        if (_trapDropRoutine != null)
        {
            StopCoroutine(_trapDropRoutine);
            _trapDropRoutine = null;
        }
    }

    private void RestartTrapRoutine()
    {
        if (_trapDropRoutine != null)
            StopCoroutine(_trapDropRoutine);

        _trapDropRoutine = StartCoroutine(TrapDropRoutine());
    }

    private IEnumerator TrapDropRoutine()
    {
        while (!Health.IsDead)
        {
            yield return new WaitForSeconds(TRAP_DROP_INTERVAL);

            if (!Health.IsDead && gameObject.activeInHierarchy)
                DropTrap();
        }
    }

    private void DropTrap()
    {
        Mb_AbilityPrefabRegistry registry = GetComponent<Mb_AbilityPrefabRegistry>();
        GameObject trapPrefab = registry?.GetPrefab(AbilityPrefabID.Trapper_ProximityTrap);

        if (trapPrefab == null)
        {
            Debug.LogError($"[Mb_TrapperController] Missing Trapper_ProximityTrap prefab on {gameObject.name}.");
            return;
        }

        GameObject trap = Instantiate(
            trapPrefab,
            transform.position + Vector3.up * TRAP_FLOAT_HEIGHT,
            Quaternion.identity
        );

        Mb_TrapperProximityTrap proximityTrap = trap.GetComponent<Mb_TrapperProximityTrap>();
        if (proximityTrap == null)
        {
            Debug.LogError($"[Mb_TrapperController] Trap prefab '{trapPrefab.name}' is missing Mb_TrapperProximityTrap.");
            Destroy(trap);
            return;
        }

        proximityTrap.Initialize(
            TRAP_DAMAGE,
            TRAP_SLOW_PERCENT,
            TRAP_SLOW_DURATION,
            TRAP_LIFETIME
        );
    }
}
