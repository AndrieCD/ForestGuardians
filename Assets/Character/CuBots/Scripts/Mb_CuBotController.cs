using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Abstract base controller for all CuBot enemies.
/// Handles aggro switching, NavMesh movement, reachability checks, and hit response.
///
/// AGGRO RULES:
///   - Default target: Panoharra
///   - Player enters AggroRange + path is reachable → chase player
///   - Player exits DeAggroRange OR becomes unreachable → return to Panoharra
///   - CuBot is in AttackingPanoharra state → never switch, even when hit
///   - CuBot takes damage + not in AttackingPanoharra → immediately chase player
///   - Failed reachability check starts AggroRetryCooldown to prevent juggle cheese
/// </summary>
public abstract class Mb_CuBotController : MB_CuBotBase
{
    [Header("Aggro Settings")]
    [SerializeField] private float AggroRange = 20f;
    [SerializeField] private float DeAggroRange = 40f;

    [Tooltip("Seconds to wait before re-checking player reachability after a failed path check. " +
             "Prevents the player from juggling CuBot aggro by hopping on and off high ground.")]
    [SerializeField] private float AggroRetryCooldown = 5f;

    // Components
    protected NavMeshAgent _Agent;
    protected Animator _Animator;
    protected Mb_CuBotAnimator _BasicCuBotAnimator;

    // Targets
    protected Transform _CurrentTarget;
    private Transform _PanoharraTarget;
    private Transform _PlayerTarget;

    // Collider cache for closest-point targeting on large objects
    private Collider _CurrentTargetCollider;

    // Destination throttling — avoids re-pathing every frame on large targets
    private Vector3 _lastDestination;
    private const float DESTINATION_UPDATE_THRESHOLD = 0.5f;

    // Aggro state
    private CuBotAIState _aiState = CuBotAIState.ChasingPanoharra;

    // Cooldown timer — counts down after a failed reachability check
    // so the player can't juggle CuBot aggro by stepping on and off high ground
    private float _aggroRetryTimer = 0f;

    public static event Action<MB_CuBotBase, Transform> OnAggroChanged;


    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    protected override void Awake()
    {
        base.Awake();

        _Agent = GetComponent<NavMeshAgent>();
        _Animator = GetComponent<Animator>();
        _BasicCuBotAnimator = GetComponent<Mb_CuBotAnimator>();

        if (_Agent == null)
            Debug.LogError($"[Mb_CuBotController] No NavMeshAgent found on {gameObject.name}.");
    }

    private void Start()
    {
        FindTargets();

        if (_Agent != null)
            _Agent.speed = Stats.MoveSpeed.GetValue();
    }

    private void Update()
    {
        if (Health.IsDead) return;

        // Count down the aggro retry cooldown each frame
        if (_aggroRetryTimer > 0f)
            _aggroRetryTimer -= Time.deltaTime;

        UpdateAggroState();
        UpdateMovement();
        UpdateAnimator();
    }


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

        // Direct assign bypasses the SetTarget equality guard on first assignment
        _CurrentTarget = _PanoharraTarget;
        CacheTargetCollider(_PanoharraTarget);
    }


    // -------------------------------------------------------------------------
    // Aggro Logic
    // -------------------------------------------------------------------------

    private void UpdateAggroState()
    {
        if (_PlayerTarget == null) return;

        // AttackingPanoharra is a fully locked state — nothing switches it
        if (_aiState == CuBotAIState.AttackingPanoharra) return;

        float distToPlayer = Vector3.Distance(transform.position, _PlayerTarget.position);

        if (_aiState != CuBotAIState.ChasingPlayer && distToPlayer <= AggroRange)
        {
            // Only attempt the path check if the retry cooldown has expired —
            // this prevents the player from juggling aggro by hopping on/off high ground
            if (_aggroRetryTimer > 0f) return;

            if (IsPathReachable(_PlayerTarget.position))
            {
                SwitchToPlayer();
            }
            else
            {
                // Path failed — start cooldown so we don't check again immediately
                _aggroRetryTimer = AggroRetryCooldown;
                Debug.Log($"[{gameObject.name}] Player unreachable — retry in {AggroRetryCooldown}s.");
            }
        }
        else if (_aiState == CuBotAIState.ChasingPlayer && distToPlayer > DeAggroRange)
        {
            SwitchToPanoharra();
        }
    }


    /// <summary>
    /// Called from MB_CuBotBase.HandleDamageTaken via the OnHitReceived hook.
    /// Switches aggro to the player immediately when hit —
    /// unless already locked into attacking the Panoharra.
    /// </summary>
    protected override void OnHitReceived()
    {
        // Never break off a Panoharra attack — the CuBot is committed
        if (_aiState == CuBotAIState.AttackingPanoharra) return;

        // Player reference may be null on very first spawn before Start() runs —
        // guard here so a hit during that window doesn't crash
        if (_PlayerTarget == null) return;

        // Switch immediately — no path check needed since the hit proves
        // the player (or their projectile) reached us, so we're reachable
        SwitchToPlayer();
    }


    private void SwitchToPlayer()
    {
        _aiState = CuBotAIState.ChasingPlayer;
        SetTarget(_PlayerTarget);
    }

    private void SwitchToPanoharra()
    {
        _aiState = CuBotAIState.ChasingPanoharra;
        SetTarget(_PanoharraTarget);
    }

    private void SetTarget(Transform newTarget)
    {
        if (newTarget == null) return;
        if (_CurrentTarget == newTarget) return;

        _CurrentTarget = newTarget;
        _lastDestination = Vector3.zero; // Force immediate re-path to new target

        CacheTargetCollider(newTarget);
        OnTargetChanged(_CurrentTarget);
        OnAggroChanged?.Invoke(this, _CurrentTarget);
    }

    private void CacheTargetCollider(Transform target)
    {
        if (target == null)
        {
            _CurrentTargetCollider = null;
            return;
        }

        _CurrentTargetCollider = target.GetComponent<Collider>();

        if (_CurrentTargetCollider == null)
            _CurrentTargetCollider = target.GetComponentInChildren<Collider>();
    }

    /// <summary>
    /// Calculates a NavMesh path to the target position and returns true only if
    /// a complete path exists. Called once when the player enters AggroRange —
    /// not every frame — to avoid per-frame path calculation overhead.
    /// </summary>
    private bool IsPathReachable(Vector3 targetPosition)
    {
        NavMeshPath path = new NavMeshPath();
        _Agent.CalculatePath(targetPosition, path);
        return path.status == NavMeshPathStatus.PathComplete;
    }


    // -------------------------------------------------------------------------
    // Movement
    // -------------------------------------------------------------------------

    private void UpdateMovement()
    {
        if (_CurrentTarget == null || _Agent == null) return;

        float attackRange = _CuBotTemplate != null ? _CuBotTemplate.AttackRange : 2f;

        Vector3 targetPoint = GetTargetPoint();
        float distToTarget = Vector3.Distance(transform.position, targetPoint);

        if (distToTarget <= attackRange)
        {
            _Agent.isStopped = true;

            // Track when we're actively attacking the Panoharra so aggro can't interrupt
            if (_aiState == CuBotAIState.ChasingPanoharra)
                _aiState = CuBotAIState.AttackingPanoharra;

            OnInAttackRange();
        }
        else
        {
            // If we were attacking Panoharra but moved out of range, revert state
            if (_aiState == CuBotAIState.AttackingPanoharra)
                _aiState = CuBotAIState.ChasingPanoharra;

            // Only re-path if target has moved far enough — prevents per-frame path thrash
            if (Vector3.Distance(targetPoint, _lastDestination) > DESTINATION_UPDATE_THRESHOLD)
            {
                _lastDestination = targetPoint;
                _Agent.isStopped = false;
                _Agent.stoppingDistance = 0f;
                _Agent.SetDestination(targetPoint);
            }
        }
    }

    private Vector3 GetTargetPoint()
    {
        if (_CurrentTargetCollider != null)
            return _CurrentTargetCollider.ClosestPoint(transform.position);

        return _CurrentTarget.position;
    }


    // -------------------------------------------------------------------------
    // Abstract / Virtual Hooks
    // -------------------------------------------------------------------------

    protected abstract void OnInAttackRange();
    protected abstract void UpdateAnimator();
    protected virtual void OnTargetChanged(Transform newTarget)
    {
        // Only react when aggro switches TO the player — not when falling back to Panoharra.
        // Panoharra fallback is a retreat, not an alert moment, so no feedback there.
        if (newTarget != _PlayerTarget) return;

        // Animator aggro reaction
        if (_BasicCuBotAnimator != null)
            _BasicCuBotAnimator.TriggerAggro();

        // Audio — alert bark
        Mb_AudioManager.PlaySFX(CombatSFX.CuBot_Aggro_Generic, transform.position);

        // VFX — exclamation burst above the CuBot's head
        // Height offset positions it above the mesh; tune _AggroVFXHeightOffset per CuBot size
        //Vector3 vfxPos = transform.position + Vector3.up * _AggroVFXHeightOffset;
        //Mb_VFXManager.Play(VFXType.CuBot_Aggro, vfxPos);
    }
    protected virtual void OnControllerReset() { }


    // -------------------------------------------------------------------------
    // Pool Reset
    // -------------------------------------------------------------------------

    protected override void Reset()
    {
        base.Reset();

        _aiState = CuBotAIState.ChasingPanoharra;
        _lastDestination = Vector3.zero;
        _aggroRetryTimer = 0f;

        FindTargets();

        if (_Agent != null && _CuBotTemplate != null)
        {
            _Agent.isStopped = false;
            _Agent.speed = Stats.MoveSpeed.GetValue();
            _Agent.ResetPath();
        }

        OnControllerReset();
    }
}

public enum CuBotAIState
{
    Idle,
    ChasingPlayer,
    ChasingPanoharra,
    AttackingPanoharra
}