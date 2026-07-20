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
    private const float NAVMESH_SPAWN_SAMPLE_RADIUS = 5.0f;
    private const float SCATTER_REFRESH_MIN_INTERVAL = 2.0f;
    private const float SCATTER_REFRESH_MAX_INTERVAL = 4.0f;
    private const float SCATTER_INITIAL_FAILED_RETRY_INTERVAL = 0.25f;
    private const float SCATTER_SAMPLE_MIN_RADIUS = 3.5f;
    private const float SCATTER_SAMPLE_RADIUS = 10.0f;
    private const float SCATTER_NAVMESH_SAMPLE_RADIUS = 3.0f;
    private const float SCATTER_MIN_TARGET_DISTANCE = 4.0f;
    private const float SCATTER_DIRECT_APPROACH_BUFFER = 5.0f;
    private const float SCATTER_LOOKAHEAD_DISTANCE = 10.0f;
    private const float SCATTER_REACHED_DISTANCE = 1.5f;
    private const float SCATTER_MIN_FORWARD_PROGRESS = 1.0f;
    private const int SCATTER_SAMPLE_ATTEMPTS = 24;
    private const float MIN_AGENT_AVOIDANCE_RADIUS = 0.35f;
    private const float OFFSET_EDGE_CLEARANCE_PADDING = 0.2f;
    private const float STUCK_CHECK_MIN_REMAINING_DISTANCE = 1.25f;
    private const float STUCK_MIN_MOVE_DISTANCE = 0.08f;
    private const float STUCK_TIME_THRESHOLD = 0.8f;
    private const float DIRECT_PURSUIT_RECOVERY_DURATION = 1.25f;

    private Vector3 _scatterDestination = Vector3.zero;
    private Vector3 _lastProgressPosition = Vector3.zero;
    private float _nextScatterRefreshTime = 0f;
    private float _stuckTimer = 0f;
    private float _directPursuitUntilTime = 0f;
    private Transform _scatterTarget;
    private bool _hasScatterDestination = false;
    private NavMeshPath _pursuitPath;
    private NavMeshPath _reachabilityPath;

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
        _pursuitPath = new NavMeshPath();
        _reachabilityPath = new NavMeshPath();

        if (_Agent == null)
            Debug.LogError($"[Mb_CuBotController] No NavMeshAgent found on {gameObject.name}.");
        else
            ConfigureAgentAvoidance();

        if (Stats != null)
        {
            Stats.OnStatsChanged -= RefreshAgentMoveSpeed;
            Stats.OnStatsChanged += RefreshAgentMoveSpeed;
        }
    }

    private void Start()
    {
        FindTargets();

        RefreshAgentMoveSpeed();
    }

    private void OnDestroy()
    {
        if (Stats != null)
            Stats.OnStatsChanged -= RefreshAgentMoveSpeed;
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
        ResetScatterDestination();
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
        ResetScatterDestination();

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
        if (_Agent == null || !_Agent.enabled || !_Agent.isOnNavMesh) return false;
        if (_reachabilityPath == null)
            _reachabilityPath = new NavMeshPath();

        _Agent.CalculatePath(targetPosition, _reachabilityPath);
        return _reachabilityPath.status == NavMeshPathStatus.PathComplete;
    }


    // -------------------------------------------------------------------------
    // Movement
    // -------------------------------------------------------------------------

    private void UpdateMovement()
    {
        if (_CurrentTarget == null || _Agent == null) return;
        if (!EnsureAgentOnNavMesh()) return;

        if (ShouldHoldMovement)
        {
            _Agent.isStopped = true;
            _Agent.ResetPath();
            return;
        }

        float attackRange = _CuBotTemplate != null ? _CuBotTemplate.AttackRange : 2f;

        Vector3 targetPoint = GetTargetPoint();
        UpdateStuckRecovery(targetPoint);

        Vector3 movementDestination = GetMovementDestination(targetPoint, attackRange);
        float distToTarget = Vector3.Distance(transform.position, targetPoint);

        if (distToTarget <= attackRange)
        {
            if (RequiresLineOfSightToAttack && !HasLineOfSightToCurrentTarget())
            {
                if (_aiState == CuBotAIState.AttackingPanoharra)
                    _aiState = CuBotAIState.ChasingPanoharra;

                _Agent.isStopped = false;
                _Agent.stoppingDistance = 0f;
                _Agent.SetDestination(targetPoint);
                return;
            }

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

            // Always resume if the agent was previously stopped in attack range.
            // Otherwise, only re-path if the destination moved far enough to avoid path thrash.
            if (_Agent.isStopped || Vector3.Distance(movementDestination, _lastDestination) > DESTINATION_UPDATE_THRESHOLD)
            {
                _lastDestination = movementDestination;
                _Agent.isStopped = false;
                _Agent.stoppingDistance = 0f;
                _Agent.SetDestination(movementDestination);
            }
        }
    }

    private Vector3 GetTargetPoint()
    {
        if (_CurrentTargetCollider != null)
            return _CurrentTargetCollider.ClosestPoint(transform.position);

        return _CurrentTarget.position;
    }

    private Vector3 GetMovementDestination(Vector3 targetPoint, float attackRange)
    {
        if (_CurrentTarget == null) return targetPoint;
        if (!UsesScatterMovement || _aiState == CuBotAIState.ChasingPlayer) return targetPoint;
        if (Time.time < _directPursuitUntilTime) return targetPoint;

        float distanceToTarget = Vector3.Distance(transform.position, targetPoint);
        if (distanceToTarget <= Mathf.Max(
            SCATTER_MIN_TARGET_DISTANCE,
            attackRange + SCATTER_DIRECT_APPROACH_BUFFER))
        {
            return targetPoint;
        }

        if (_hasScatterDestination &&
            Time.time < _nextScatterRefreshTime &&
            Vector3.Distance(transform.position, _scatterDestination) <= SCATTER_REACHED_DISTANCE)
        {
            HoldDirectPursuitUntilNextScatterRefresh(targetPoint);
            return targetPoint;
        }

        if (!_hasScatterDestination && Time.time < _nextScatterRefreshTime)
            return targetPoint;

        bool isInitialScatterRefresh = !_hasScatterDestination || _scatterTarget != _CurrentTarget;
        bool shouldRefreshDestination = !_hasScatterDestination ||
            _scatterTarget != _CurrentTarget ||
            Time.time >= _nextScatterRefreshTime;

        if (!shouldRefreshDestination)
            return _scatterDestination;

        return TryRefreshScatterDestination(
                targetPoint,
                isInitialScatterRefresh,
                out Vector3 scatterDestination)
            ? scatterDestination
            : targetPoint;
    }

    private bool TryRefreshScatterDestination(
        Vector3 targetPoint,
        bool isInitialScatterRefresh,
        out Vector3 scatterDestination)
    {
        scatterDestination = Vector3.zero;

        Vector3 scatterCenter = GetScatterCenter(targetPoint);
        float currentTargetDistance = Vector3.Distance(transform.position, targetPoint);
        Vector3 selectedDestination = Vector3.zero;
        int validCandidateCount = 0;

        for (int i = 0; i < SCATTER_SAMPLE_ATTEMPTS; i++)
        {
            Vector2 randomDirection = UnityEngine.Random.insideUnitCircle.normalized;
            if (randomDirection.sqrMagnitude <= 0.001f)
                randomDirection = Vector2.right;

            float scatterDistance = UnityEngine.Random.Range(
                SCATTER_SAMPLE_MIN_RADIUS,
                SCATTER_SAMPLE_RADIUS);

            Vector2 randomCircle = randomDirection * scatterDistance;
            Vector3 desiredDestination = scatterCenter + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (!TryResolveScatterCandidate(
                desiredDestination,
                scatterCenter,
                targetPoint,
                currentTargetDistance,
                out Vector3 candidate))
            {
                continue;
            }

            validCandidateCount++;

            if (UnityEngine.Random.Range(0, validCandidateCount) == 0)
                selectedDestination = candidate;
        }

        if (validCandidateCount == 0)
        {
            if (isInitialScatterRefresh)
                _nextScatterRefreshTime = Time.time + SCATTER_INITIAL_FAILED_RETRY_INTERVAL;
            else
                HoldDirectPursuitForScatterInterval(targetPoint);

            return false;
        }

        _scatterDestination = selectedDestination;
        _scatterTarget = _CurrentTarget;
        _nextScatterRefreshTime = Time.time + GetRandomScatterRefreshInterval();
        _hasScatterDestination = true;

        scatterDestination = _scatterDestination;
        return true;
    }

    private void HoldDirectPursuitUntilNextScatterRefresh(Vector3 targetPoint)
    {
        float directUntilTime = _nextScatterRefreshTime > Time.time
            ? _nextScatterRefreshTime
            : Time.time + GetRandomScatterRefreshInterval();

        HoldDirectPursuit(targetPoint, directUntilTime);
    }

    private void HoldDirectPursuitForScatterInterval(Vector3 targetPoint)
    {
        HoldDirectPursuit(targetPoint, Time.time + GetRandomScatterRefreshInterval());
    }

    private void HoldDirectPursuit(Vector3 targetPoint, float directUntilTime)
    {
        _scatterDestination = targetPoint;
        _scatterTarget = _CurrentTarget;
        _nextScatterRefreshTime = directUntilTime;
        _directPursuitUntilTime = directUntilTime;
        _hasScatterDestination = true;
        _lastDestination = Vector3.zero;
    }

    private float GetRandomScatterRefreshInterval()
    {
        return UnityEngine.Random.Range(
            SCATTER_REFRESH_MIN_INTERVAL,
            SCATTER_REFRESH_MAX_INTERVAL
        );
    }

    private bool TryResolveScatterCandidate(
        Vector3 desiredDestination,
        Vector3 scatterCenter,
        Vector3 targetPoint,
        float currentTargetDistance,
        out Vector3 candidate)
    {
        candidate = Vector3.zero;

        if (!NavMesh.SamplePosition(
            desiredDestination,
            out NavMeshHit hit,
            SCATTER_NAVMESH_SAMPLE_RADIUS,
            NavMesh.AllAreas))
        {
            return false;
        }

        if (Vector3.Distance(hit.position, scatterCenter) < SCATTER_SAMPLE_MIN_RADIUS)
            return false;

        if (!HasEnoughNavMeshClearance(hit.position))
            return false;

        if (_pursuitPath == null)
            _pursuitPath = new NavMeshPath();

        _Agent.CalculatePath(hit.position, _pursuitPath);
        if (_pursuitPath.status != NavMeshPathStatus.PathComplete)
            return false;

        float candidateTargetDistance = Vector3.Distance(hit.position, targetPoint);
        if (candidateTargetDistance > currentTargetDistance - SCATTER_MIN_FORWARD_PROGRESS)
            return false;

        Vector3 currentToTarget = targetPoint - transform.position;
        Vector3 currentToCandidate = hit.position - transform.position;
        currentToTarget.y = 0f;
        currentToCandidate.y = 0f;

        if (currentToTarget.sqrMagnitude <= 0.001f ||
            currentToCandidate.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        float forwardDot = Vector3.Dot(currentToTarget.normalized, currentToCandidate.normalized);
        if (forwardDot <= 0.15f)
            return false;

        candidate = hit.position;
        return true;
    }

    private bool HasEnoughNavMeshClearance(Vector3 position)
    {
        if (_Agent == null) return false;

        if (!NavMesh.FindClosestEdge(
            position,
            out NavMeshHit edgeHit,
            NavMesh.AllAreas))
        {
            return false;
        }

        return edgeHit.distance >= _Agent.radius + OFFSET_EDGE_CLEARANCE_PADDING;
    }

    private Vector3 GetScatterCenter(Vector3 targetPoint)
    {
        if (_Agent == null || !_Agent.enabled || !_Agent.isOnNavMesh)
            return targetPoint;

        if (_pursuitPath == null)
            _pursuitPath = new NavMeshPath();

        _Agent.CalculatePath(targetPoint, _pursuitPath);
        if (_pursuitPath.status != NavMeshPathStatus.PathComplete ||
            _pursuitPath.corners == null ||
            _pursuitPath.corners.Length < 2)
        {
            return targetPoint;
        }

        Vector3 previousCorner = transform.position;
        float travelledDistance = 0f;

        for (int i = 1; i < _pursuitPath.corners.Length; i++)
        {
            Vector3 corner = _pursuitPath.corners[i];
            float segmentDistance = Vector3.Distance(previousCorner, corner);

            if (travelledDistance + segmentDistance >= SCATTER_LOOKAHEAD_DISTANCE)
            {
                float segmentT = Mathf.Clamp01((SCATTER_LOOKAHEAD_DISTANCE - travelledDistance) / segmentDistance);
                return Vector3.Lerp(previousCorner, corner, segmentT);
            }

            travelledDistance += segmentDistance;
            previousCorner = corner;
        }

        return previousCorner;
    }

    private void ResetScatterDestination()
    {
        _scatterDestination = Vector3.zero;
        _lastProgressPosition = transform.position;
        _stuckTimer = 0f;
        _directPursuitUntilTime = 0f;
        _scatterTarget = null;
        _nextScatterRefreshTime = 0f;
        _hasScatterDestination = false;
    }

    private void UpdateStuckRecovery(Vector3 targetPoint)
    {
        if (_Agent == null || !_Agent.enabled || !_Agent.isOnNavMesh) return;

        if (_Agent.isStopped ||
            _Agent.pathPending ||
            !_Agent.hasPath ||
            Vector3.Distance(transform.position, targetPoint) <= STUCK_CHECK_MIN_REMAINING_DISTANCE)
        {
            ResetStuckTimer();
            return;
        }

        float movedDistance = Vector3.Distance(transform.position, _lastProgressPosition);
        if (movedDistance >= STUCK_MIN_MOVE_DISTANCE)
        {
            ResetStuckTimer();
            return;
        }

        _stuckTimer += Time.deltaTime;

        if (_stuckTimer < STUCK_TIME_THRESHOLD) return;

        _directPursuitUntilTime = Time.time + DIRECT_PURSUIT_RECOVERY_DURATION;
        _lastDestination = Vector3.zero;
        _scatterDestination = targetPoint;
        _stuckTimer = 0f;
    }

    private void ResetStuckTimer()
    {
        _stuckTimer = 0f;
        _lastProgressPosition = transform.position;
    }


    // -------------------------------------------------------------------------
    // Abstract / Virtual Hooks
    // -------------------------------------------------------------------------

    protected abstract void OnInAttackRange();
    protected abstract void UpdateAnimator();

    protected virtual bool ShouldHoldMovement => false;

    protected virtual bool RequiresLineOfSightToAttack => false;

    protected virtual bool UsesScatterMovement => true;

    protected virtual Transform AttackLineOfSightOrigin => transform;

    protected bool HasLineOfSightToCurrentTarget(float targetHeightOffset = 1.0f)
    {
        if (_CurrentTarget == null) return false;

        Transform originTransform = AttackLineOfSightOrigin != null
            ? AttackLineOfSightOrigin
            : transform;

        Vector3 origin = originTransform.position;
        Vector3 targetPosition = _CurrentTarget.position + Vector3.up * targetHeightOffset;
        Vector3 direction = (targetPosition - origin).normalized;
        float distance = Vector3.Distance(origin, targetPosition);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, distance))
            return hit.transform.IsChildOf(_CurrentTarget) || hit.transform == _CurrentTarget;

        return true;
    }

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

    private bool EnsureAgentOnNavMesh()
    {
        if (_Agent == null) return false;

        if (!_Agent.enabled)
            _Agent.enabled = true;

        if (_Agent.isOnNavMesh)
            return true;

        if (NavMesh.SamplePosition(
            transform.position,
            out NavMeshHit hit,
            NAVMESH_SPAWN_SAMPLE_RADIUS,
            NavMesh.AllAreas))
        {
            return _Agent.Warp(hit.position);
        }

        Debug.LogWarning($"[Mb_CuBotController] {gameObject.name} could not be placed on a NavMesh near {transform.position}.");
        return false;
    }

    private void RefreshAgentMoveSpeed()
    {
        if (_Agent == null || Stats == null || Stats.MoveSpeed == null) return;

        _Agent.speed = Mathf.Max(0f, Stats.MoveSpeed.GetValue());
    }

    private void ConfigureAgentAvoidance()
    {
        if (_Agent == null) return;

        if (_Agent.radius < MIN_AGENT_AVOIDANCE_RADIUS)
            _Agent.radius = MIN_AGENT_AVOIDANCE_RADIUS;

        _Agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        _Agent.avoidancePriority = UnityEngine.Random.Range(30, 71);
    }


    // -------------------------------------------------------------------------
    // Pool Reset
    // -------------------------------------------------------------------------

    protected override void Reset()
    {
        base.Reset();

        _aiState = CuBotAIState.ChasingPanoharra;
        _lastDestination = Vector3.zero;
        _aggroRetryTimer = 0f;
        ResetScatterDestination();

        FindTargets();

        if (_Agent != null && _CuBotTemplate != null)
        {
            if (!EnsureAgentOnNavMesh()) return;

            _Agent.isStopped = false;
            ConfigureAgentAvoidance();
            RefreshAgentMoveSpeed();
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
