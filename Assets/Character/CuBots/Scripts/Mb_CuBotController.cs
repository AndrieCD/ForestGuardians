using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Abstract base controller for all CuBot enemies.
/// Extends MB_CuBotBase to add targeting logic, NavMesh movement, and aggro switching.
///
/// TARGETING RULES:
///   - Default target is the Panoharra Tree (tag "Panoharra")
///   - If the Player enters AggroRange, switch target to Player
///   - If the Player exits DeAggroRange, switch back to Panoharra
///
/// DERIVED CLASSES must implement:
///   - OnInAttackRange()  — called every Update when within attack range of current target
///   - UpdateAnimator()   — called every Update, drive your Animator parameters here
///
/// DERIVED CLASSES may override:
///   - OnTargetChanged()  — react when the target switches
///   - OnControllerReset() — clean up derived-class state on pool reuse
/// </summary>
public abstract class Mb_CuBotController : MB_CuBotBase
{
    [Header("Aggro Settings")]
    [SerializeField] float AggroRange = 20f;     // Player enters this range → switch target to player
    [SerializeField] float DeAggroRange = 40f;  // Player exits this range → switch back to Panoharra


    // Component references
    protected NavMeshAgent _Agent;
    protected Animator _Animator;


    // Targeting
    protected Transform _CurrentTarget;
    private Transform _PanoharraTarget;
    private Transform _PlayerTarget;
    //private bool _IsAggroed = false;    // True when currently chasing the player
    private CuBotAIState _aiState = CuBotAIState.ChasingPanoharra;


    #region
    public static event Action<MB_CuBotBase, Transform> OnAggroChanged; // cubot, newTarget

    #endregion


    protected override void Awake()
    {
        base.Awake();

        _Agent = GetComponent<NavMeshAgent>();
        _Animator = GetComponent<Animator>();

        if (_Agent == null)
            Debug.LogError($"[Mb_CuBotController] No NavMeshAgent found on {gameObject.name}.");
    }

    private void Start()
    {
        FindTargets();
    }

    private void FindTargets()
    {
        GameObject panoharra = GameObject.FindGameObjectWithTag("Panoharra");
        if (panoharra != null)
            _PanoharraTarget = panoharra.transform;
        else
            Debug.LogWarning($"[Mb_CuBotController] No GameObject with tag 'Panoharra' found.");

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _PlayerTarget = player.transform;
        else
            Debug.LogWarning($"[Mb_CuBotController] No GameObject with tag 'Player' found.");

        _CurrentTarget = _PanoharraTarget;

        // Sync NavMeshAgent speed from StatBlock — must happen here since Start() runs
        // after Awake(), guaranteeing Stats are built from the template before this line
        if (_Agent != null)
            _Agent.speed = Stats.MoveSpeed.GetValue();
    }

    private void Update()
    {
        if (Health.IsDead) return;

#if UNITY_EDITOR
        Debug.Log($"Current Target: {_CurrentTarget}");
#endif

        UpdateAggroState();
        UpdateMovement();
        UpdateAnimator();
    }

    // -------------------------------------------------------------------------
    // Aggro Logic
    // -------------------------------------------------------------------------

    private void UpdateAggroState()
    {
        if (_PlayerTarget == null) return;

        float distToPlayer = Vector3.Distance(transform.position, _PlayerTarget.position);

        if (_aiState != CuBotAIState.ChasingPlayer && distToPlayer <= AggroRange)
        {
            // Player just entered aggro range — switch to chasing player
            _aiState = CuBotAIState.ChasingPlayer;
            SetTarget(_PlayerTarget);
        }
        else if (_aiState == CuBotAIState.ChasingPlayer && distToPlayer > DeAggroRange)
        {
            // Player ran far enough away — go back to Panoharra
            _aiState = CuBotAIState.ChasingPanoharra;
            SetTarget(_PanoharraTarget);
        }
    }

    private void SetTarget(Transform newTarget)
    {
        if (newTarget == _CurrentTarget) return;
        _CurrentTarget = newTarget;
        OnTargetChanged(_CurrentTarget);
        OnAggroChanged?.Invoke(this, _CurrentTarget);
    }

    // -------------------------------------------------------------------------
    // Movement
    // -------------------------------------------------------------------------

    private void UpdateMovement()
    {
        if (_CurrentTarget == null || _Agent == null) return;

        float distToTarget = Vector3.Distance(transform.position, _CurrentTarget.position);
        float attackRange = _CuBotTemplate != null ? _CuBotTemplate.AttackRange : 2f;

        if (distToTarget <= attackRange)
        {
            // In range — stop moving and let the derived class decide what to do
            _Agent.isStopped = true;
            OnInAttackRange();
        }
        else
        {
#if UNITY_EDITOR
            Debug.Log($"Updating movement towards target: {_CurrentTarget.name}");
            Debug.Log($"Speed: {_Agent.speed}, IsStopped: {_Agent.isStopped}");
#endif
            // Out of range — keep chasing
            _Agent.isStopped = false;
            _Agent.SetDestination(_CurrentTarget.position);
        }
    }

    // -------------------------------------------------------------------------
    // Abstract & Virtual Hooks
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called every Update frame when the CuBot is within attack range of its target.
    /// Derived classes use this to trigger attacks.
    /// </summary>
    protected abstract void OnInAttackRange();

    /// <summary>
    /// Called every Update. Drive Animator parameters here in derived classes.
    /// </summary>
    protected abstract void UpdateAnimator();

    /// <summary>
    /// Called when the current target changes between Player and Panoharra.
    /// Optional — override if your CuBot needs to react to the switch.
    /// </summary>
    protected virtual void OnTargetChanged(Transform newTarget) { }

    /// <summary>
    /// Called at the end of Reset() so derived classes can clean up their own state.
    /// Override to reset flags, cancel coroutines, etc.
    /// </summary>
    protected virtual void OnControllerReset() { }

    // -------------------------------------------------------------------------
    // Pool Reset
    // -------------------------------------------------------------------------

    protected override void Reset()
    {
        base.Reset(); // Calls InitializeFromTemplate() — restores stats and health

        _aiState = CuBotAIState.ChasingPanoharra;

        // Re-sync NavMeshAgent speed in case stats were changed by wave scaling
        if (_Agent != null && _CuBotTemplate != null)
        {
            _Agent.isStopped = false;
            _Agent.speed = Stats.MoveSpeed.GetValue();
        }

        // Reset target back to Panoharra on pool reuse
        SetTarget(_PanoharraTarget);

        OnControllerReset();
    }
}

public enum CuBotAIState { Patrolling, Idle, ChasingPlayer, ChasingPanoharra, Attacking }
