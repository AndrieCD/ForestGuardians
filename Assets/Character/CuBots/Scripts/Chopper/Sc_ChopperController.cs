using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Sc_ChopperController : MB_CuBotBase
{
    [Header("Targeting")]
    private float _attackRange;

    private NavMeshAgent _Agent;
    private Transform _Target;

    protected override void Awake( )
    {
        base.Awake( );

        _attackRange = _CuBotTemplate.AttackRange;

        _Agent = GetComponent<NavMeshAgent>( );
        _Agent.speed = MoveSpeed.Value( );

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _Target = player.transform;

        // Initialize attack ability
        _PrimaryAttack = new Sc_ChopperMeleeAttack(
            _CuBotTemplate.PrimaryAttack,
            this
        );
    }

    private void Update( )
    {
        if (IsDead || _Target == null) return;

        float distance = Vector3.Distance(
            transform.position,
            _Target.position
        );

        if (distance > _attackRange)
        {
            ChaseTarget( );
        } else
        {
            AttackTarget( );
        }
    }

    private void ChaseTarget( )
    {
        _Agent.isStopped = false;
        _Agent.SetDestination(_Target.position);
    }

    private void AttackTarget( )
    {
        _Agent.isStopped = true;
        // stop for 1s to attack

        transform.LookAt(_Target);
        TryUsePrimaryAttack( );
    }

    
}
