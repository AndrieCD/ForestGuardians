using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Abstract base controller for all CuBot enemies.
/// Handles:
/// - Aggro switching
/// - NavMesh movement
/// - Collider-aware targeting
///
/// TARGETING RULES:
///   - Default target is Panoharra
///   - Player enters AggroRange -> chase player
///   - Player exits DeAggroRange -> return to Panoharra
///
/// MOVEMENT RULES:
///   - Uses NavMeshAgent.stoppingDistance
///   - Targets closest point on collider instead of transform center
///   - Uses remainingDistance for robust arrival detection
/// </summary>
public abstract class Mb_CuBotController : MB_CuBotBase
{
    [Header("Aggro Settings")]
    [SerializeField] private float AggroRange = 20f;
    [SerializeField] private float DeAggroRange = 40f;

    // Components
    protected NavMeshAgent _Agent;
    protected Animator _Animator;

    // Targets
    protected Transform _CurrentTarget;

    private Transform _PanoharraTarget;
    private Transform _PlayerTarget;

    // Add this field at the top with the other private fields
    private Vector3 _lastDestination;
    private const float DESTINATION_UPDATE_THRESHOLD = 0.5f; // Only re-path if target moved this far

    // Cached target collider
    private Collider _CurrentTargetCollider;

    private CuBotAIState _aiState = CuBotAIState.ChasingPanoharra;


    protected Mb_CuBotAnimator _BasicCuBotAnimator = null;


    #region Events

    public static event Action<MB_CuBotBase, Transform> OnAggroChanged;

    #endregion

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    protected override void Awake()
    {
        base.Awake();

        _Agent = GetComponent<NavMeshAgent>();
        _Animator = GetComponent<Animator>();

        if (_Agent == null)
        {
            Debug.LogError($"[Mb_CuBotController] No NavMeshAgent found on {gameObject.name}.");
        }

        _BasicCuBotAnimator = GetComponent<Mb_CuBotAnimator>();
    }

    private void Start()
    {

        if (_Agent != null)
        {
            _Agent.speed = Stats.MoveSpeed.GetValue();
        }
    }

    private void Update()
    {
        if (Health.IsDead)
            return;

        UpdateAggroState();
        UpdateMovement();
        UpdateAnimator();
    }

    // -------------------------------------------------------------------------
    // Target Setup
    // -------------------------------------------------------------------------

    // -------------------------------------------------------------------------
    // Target Setup
    // -------------------------------------------------------------------------

    private void FindTargets()
    {
        GameObject panoharra = GameObject.FindGameObjectWithTag("Panoharra");
        if (panoharra != null)
            _PanoharraTarget = panoharra.transform;
        else
            Debug.LogWarning("[Mb_CuBotController] No GameObject with tag 'Panoharra' found.");

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _PlayerTarget = player.transform;
        else
            Debug.LogWarning("[Mb_CuBotController] No GameObject with tag 'Player' found.");

        SetTarget(_PanoharraTarget);
    }

    // -------------------------------------------------------------------------
    // Aggro Logic
    // -------------------------------------------------------------------------

    private void UpdateAggroState()
    {
        if (_PlayerTarget == null)
            return;

        float distToPlayer = Vector3.Distance(transform.position, _PlayerTarget.position);

        if (_aiState != CuBotAIState.ChasingPlayer &&
            distToPlayer <= AggroRange)
        {
            _aiState = CuBotAIState.ChasingPlayer;
            SetTarget(_PlayerTarget);
        }
        else if (_aiState == CuBotAIState.ChasingPlayer &&
                 distToPlayer > DeAggroRange)
        {
            _aiState = CuBotAIState.ChasingPanoharra;
            SetTarget(_PanoharraTarget);
        }
    }

    private void SetTarget(Transform newTarget)
    {
        if (newTarget == null)
            return;

        if (_CurrentTarget == newTarget)
            return;

        _CurrentTarget = newTarget;

        // Cache collider for closest-point targeting
        _CurrentTargetCollider = _CurrentTarget.GetComponent<Collider>();

        // Fallback: search children if root collider missing
        if (_CurrentTargetCollider == null)
        {
            _CurrentTargetCollider = _CurrentTarget.GetComponentInChildren<Collider>();
        }

        OnTargetChanged(_CurrentTarget);
        OnAggroChanged?.Invoke(this, _CurrentTarget);
    }

    // -------------------------------------------------------------------------
    // Movement
    // -------------------------------------------------------------------------

    private void UpdateMovement()
    {
        if (_CurrentTarget == null || _Agent == null)
            return;

        float attackRange = _CuBotTemplate != null
            ? _CuBotTemplate.AttackRange
            : 2f;

        // Measure to the closest surface point — handles large colliders correctly
        Vector3 targetPoint = GetTargetPoint();
        float distToTarget = Vector3.Distance(transform.position, targetPoint);

        if (distToTarget <= attackRange)
        {
            // In range — stop and let derived class handle attacking
            _Agent.isStopped = true;
            OnInAttackRange();
        }
        else
        {
            // Only re-path if the destination has shifted significantly —
            // calling SetDestination every frame on a large object causes stutter
            if (Vector3.Distance(targetPoint, _lastDestination) > DESTINATION_UPDATE_THRESHOLD)
            {
                _lastDestination = targetPoint;
                _Agent.isStopped = false;
                _Agent.stoppingDistance = 0f;

                // Align target point Y with agent to prevent looking up/down when target has significant height difference
                targetPoint.y = transform.position.y;

                _Agent.SetDestination(targetPoint);
            }
        }
    }

    /// <summary>
    /// Gets the closest valid point on the target collider.
    /// Falls back to transform position if no collider exists.
    /// </summary>
    private Vector3 GetTargetPoint()
    {
        if (_CurrentTargetCollider != null)
        {
            return _CurrentTargetCollider.ClosestPoint(transform.position);
        }

        return _CurrentTarget.position;
    }

    // -------------------------------------------------------------------------
    // Abstract / Virtual Hooks
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called every frame while inside attack range.
    /// Derived classes should handle attacks here.
    /// </summary>
    protected abstract void OnInAttackRange();

    /// <summary>
    /// Called every Update frame.
    /// Derived classes should update Animator parameters here.
    /// </summary>
    protected abstract void UpdateAnimator();

    /// <summary>
    /// Called whenever the current target changes.
    /// </summary>
    protected virtual void OnTargetChanged(Transform newTarget) {
        if (newTarget == _PlayerTarget) _BasicCuBotAnimator.TriggerAggro(); 
    }

    /// <summary>
    /// Called after Reset() for derived cleanup logic.
    /// </summary>
    protected virtual void OnControllerReset() { }

    // -------------------------------------------------------------------------
    // Pool Reset
    // -------------------------------------------------------------------------

    protected override void Reset()
    {
        base.Reset(); // calls MB_CuBotBase.Reset() → InitializeFromTemplate()

        _aiState = CuBotAIState.ChasingPanoharra;
        _lastDestination = Vector3.zero;

        // Re-find targets every reactivation — Start() only runs once on first spawn,
        // so pooled CuBots would have stale or null target references without this.
        FindTargets();

        // Sync NavMesh speed to the freshly recalculated stats (level scaling may have changed it)
        if (_Agent != null && _CuBotTemplate != null)
        {
            _Agent.isStopped = false;
            _Agent.speed = Stats.MoveSpeed.GetValue();
            _Agent.ResetPath(); // Clear any leftover path from the previous activation
        }

        OnControllerReset();
    }
}

public enum CuBotAIState
{
    Patrolling,
    Idle,
    ChasingPlayer,
    ChasingPanoharra,
    Attacking
}