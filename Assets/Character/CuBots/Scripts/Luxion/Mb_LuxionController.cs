using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Luxion final boss controller.
/// Handles phase switching, phase-specific attacks, Flash Photography, and
/// direct NavMesh movement without relying on the single-range CuBot controller.
/// </summary>
public class Mb_LuxionController : MB_CuBotBase
{
    [Header("Phase Thresholds")]
    [SerializeField] private float _AcquisitionHealthThreshold = 0.75f;
    [SerializeField] private float _ConsumptionHealthThreshold = 0.50f;
    [SerializeField] private float _PhaseTransitionDuration = 1.0f;

    [Header("Targeting")]
    [SerializeField] private float _TargetHeightOffset = 1.0f;
    [SerializeField] private LayerMask _LineOfSightMask = Physics.DefaultRaycastLayers;

    [Header("Phase 1 - Extraction")]
    [SerializeField] private float _AxeSlashRange = 3.0f;
    [SerializeField] private float _AxeSlashRadius = 3.0f;
    [SerializeField] private float _AxeSlashArcDegrees = 140f;
    [SerializeField] private float _AxeSlashCooldown = 1.35f;
    [SerializeField] private float _SpinHarvestRange = 10f;
    [SerializeField] private float _SpinHarvestRadius = 2.2f;
    [SerializeField] private float _SpinHarvestDuration = 1.25f;
    [SerializeField] private float _SpinHarvestSpeed = 12f;
    [SerializeField] private float _SpinHarvestDamageInterval = 0.25f;
    [SerializeField] private float _SpinHarvestCooldown = 7f;

    [Header("Phase 2 - Acquisition")]
    [SerializeField] private float _RifleShotRange = 28f;
    [SerializeField] private float _RifleShotCooldown = 1.2f;
    [SerializeField] private float _BulletRainMinRange = 6f;
    [SerializeField] private float _BulletRainRange = 28f;
    [SerializeField] private int _BulletRainVolleyCount = 5;
    [SerializeField] private int _BulletRainProjectilesPerVolley = 5;
    [SerializeField] private float _BulletRainSpreadAngle = 35f;
    [SerializeField] private float _BulletRainVolleyInterval = 0.2f;
    [SerializeField] private float _BulletRainCooldown = 8f;

    [Header("Phase 3 - Consumption")]
    [SerializeField] private float _ConsumptionMoveSpeedMultiplier = 0.65f;
    [SerializeField] private float _ConsumptionMaxHealthBonus = 0.25f;
    [SerializeField] private float _PickaxeSlamRange = 3.5f;
    [SerializeField] private float _PickaxeSlamRadius = 4.0f;
    [SerializeField] private float _PickaxeSlamCooldown = 1.75f;
    [SerializeField] private float _EarthBreakerRange = 12f;
    [SerializeField] private float _EarthBreakerLeapDuration = 0.45f;
    [SerializeField] private float _EarthBreakerImpactRadius = 4.5f;
    [SerializeField] private float _EarthBreakerCooldown = 8f;
    [SerializeField] private float _CrackedAreaRadius = 4.0f;
    [SerializeField] private float _CrackedAreaDuration = 4.0f;
    [SerializeField] private float _CrackedAreaTickInterval = 0.5f;

    [Header("Flash Photography")]
    [SerializeField] private float _EyeContactRequiredDuration = 1.0f;
    [SerializeField] private float _FlashRange = 22f;
    [SerializeField] private float _FlashWindupDuration = 0.8f;
    [SerializeField] private float _BlindDuration = 2.0f;
    [SerializeField] private float _FacingLuxionDotThreshold = 0.5f;

    private NavMeshAgent _agent;
    private Mb_CuBotAnimator _cuBotAnimator;
    private Mb_ProjectileLauncher _projectileLauncher;
    private GameObject _projectilePrefab;

    private Transform _playerTarget;
    private Transform _panoharraTarget;
    private LuxionPhase _currentPhase = LuxionPhase.Extraction;
    private Sc_Modifier _consumptionModifier;

    private bool _isCasting;
    private bool _isTransitioning;
    private bool _flashUsedThisPhase;
    private float _eyeContactTimer;

    private float _axeSlashCooldownRemaining;
    private float _spinHarvestCooldownRemaining;
    private float _rifleShotCooldownRemaining;
    private float _bulletRainCooldownRemaining;
    private float _pickaxeSlamCooldownRemaining;
    private float _earthBreakerCooldownRemaining;

    protected override void Awake()
    {
        base.Awake();

        _agent = GetComponent<NavMeshAgent>();
        _cuBotAnimator = GetComponent<Mb_CuBotAnimator>();
        _projectileLauncher = GetComponent<Mb_ProjectileLauncher>();

        if (_agent == null)
            Debug.LogError($"[Mb_LuxionController] No NavMeshAgent found on {gameObject.name}.");

        if (_projectileLauncher == null)
            Debug.LogError($"[Mb_LuxionController] No Mb_ProjectileLauncher found on {gameObject.name}.");

        Mb_AbilityPrefabRegistry registry = GetComponent<Mb_AbilityPrefabRegistry>();
        _projectilePrefab = registry?.GetPrefab(AbilityPrefabID.Luxion_BulletProjectile);

        if (_projectilePrefab == null)
            _projectilePrefab = registry?.GetPrefab(AbilityPrefabID.Hunter_BulletProjectile);

        if (_projectilePrefab == null)
            _projectilePrefab = registry?.GetPrefab(AbilityPrefabID.Generic_Projectile);

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
        if (Health == null || Health.IsDead) return;

        FindTargets();
        TickCooldowns();
        TryStartPhaseTransition();
        UpdateFlashEyeContact();
        UpdateAnimator();

        if (_isCasting || _isTransitioning) return;

        RunPhaseLogic();
    }

    protected override void AssignAbilities()
    {
        // Luxion's boss actions are controlled directly by this controller.
    }

    protected override void Reset()
    {
        base.Reset();

        _currentPhase = LuxionPhase.Extraction;
        _isCasting = false;
        _isTransitioning = false;
        _flashUsedThisPhase = false;
        _eyeContactTimer = 0f;
        Health.IsUntargetable = false;

        ClearConsumptionModifier();
        ResetCooldowns();
        FindTargets();
        RefreshAgentMoveSpeed();
    }

    private void RunPhaseLogic()
    {
        Transform target = GetActiveTarget();
        if (target == null || _agent == null) return;

        if (CanUseFlashPhotography(target))
        {
            StartCoroutine(FlashPhotographyRoutine(target));
            return;
        }

        float distance = Vector3.Distance(transform.position, target.position);

        switch (_currentPhase)
        {
            case LuxionPhase.Extraction:
                RunExtractionLogic(target, distance);
                break;

            case LuxionPhase.Acquisition:
                RunAcquisitionLogic(target, distance);
                break;

            case LuxionPhase.Consumption:
                RunConsumptionLogic(target, distance);
                break;
        }
    }

    private void RunExtractionLogic(Transform target, float distance)
    {
        if (distance <= _AxeSlashRange && _axeSlashCooldownRemaining <= 0f)
        {
            StartCoroutine(AxeSlashRoutine(target));
            return;
        }

        if (distance > _AxeSlashRange &&
            distance <= _SpinHarvestRange &&
            _spinHarvestCooldownRemaining <= 0f)
        {
            StartCoroutine(SpinHarvestRoutine(target));
            return;
        }

        MoveToward(target.position, 0f);
    }

    private void RunAcquisitionLogic(Transform target, float distance)
    {
        bool hasLineOfSight = HasLineOfSight(target);

        if (distance >= _BulletRainMinRange &&
            distance <= _BulletRainRange &&
            hasLineOfSight &&
            _bulletRainCooldownRemaining <= 0f)
        {
            StartCoroutine(BulletRainRoutine(target));
            return;
        }

        if (distance <= _RifleShotRange && hasLineOfSight && _rifleShotCooldownRemaining <= 0f)
        {
            StartCoroutine(RifleShotRoutine(target));
            return;
        }

        if (distance > _RifleShotRange)
            MoveToward(target.position, _RifleShotRange * 0.8f);
        else
            StopAgent();
    }

    private void RunConsumptionLogic(Transform target, float distance)
    {
        if (distance <= _PickaxeSlamRange && _pickaxeSlamCooldownRemaining <= 0f)
        {
            StartCoroutine(PickaxeSlamRoutine(target));
            return;
        }

        if (distance > _PickaxeSlamRange &&
            distance <= _EarthBreakerRange &&
            _earthBreakerCooldownRemaining <= 0f)
        {
            StartCoroutine(EarthBreakerRoutine(target));
            return;
        }

        MoveToward(target.position, 0f);
    }

    private IEnumerator AxeSlashRoutine(Transform target)
    {
        BeginCast(target);
        yield return new WaitForSeconds(0.35f);

        DealArcDamage(
            _CuBotTemplate.PrimaryAttack,
            _AxeSlashRadius,
            _AxeSlashArcDegrees,
            1.0f
        );

        _axeSlashCooldownRemaining = _AxeSlashCooldown;
        EndCast();
    }

    private IEnumerator SpinHarvestRoutine(Transform target)
    {
        BeginCast(target);

        float elapsed = 0f;
        float damageTimer = 0f;
        HashSet<Mb_CharacterBase> damagedThisTick = new HashSet<Mb_CharacterBase>();

        while (elapsed < _SpinHarvestDuration)
        {
            elapsed += Time.deltaTime;
            damageTimer -= Time.deltaTime;

            Vector3 destination = target != null ? target.position : transform.position + transform.forward;
            Vector3 direction = destination - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                MoveByWarp(transform.position + direction.normalized * _SpinHarvestSpeed * Time.deltaTime);
            }

            if (damageTimer <= 0f)
            {
                damagedThisTick.Clear();
                DealRadiusDamage(_CuBotTemplate.AbilityQ, _SpinHarvestRadius, 0.45f, damagedThisTick);
                damageTimer = _SpinHarvestDamageInterval;
            }

            yield return null;
        }

        _spinHarvestCooldownRemaining = _SpinHarvestCooldown;
        EndCast();
    }

    private IEnumerator RifleShotRoutine(Transform target)
    {
        BeginCast(target);
        yield return new WaitForSeconds(0.2f);

        FireProjectileAt(target, _CuBotTemplate.SecondaryAttack, 1.0f);

        _rifleShotCooldownRemaining = _RifleShotCooldown;
        EndCast();
    }

    private IEnumerator BulletRainRoutine(Transform target)
    {
        BeginCast(target);

        Vector3 aimDirection = GetDirectionToTarget(target);

        for (int volley = 0; volley < _BulletRainVolleyCount; volley++)
        {
            FireProjectileSpread(aimDirection, _CuBotTemplate.AbilityE);
            yield return new WaitForSeconds(_BulletRainVolleyInterval);
        }

        _bulletRainCooldownRemaining = _BulletRainCooldown;
        EndCast();
    }

    private IEnumerator PickaxeSlamRoutine(Transform target)
    {
        BeginCast(target);
        yield return new WaitForSeconds(0.45f);

        DealRadiusDamage(_CuBotTemplate.PrimaryAttack, _PickaxeSlamRadius, 1.15f);

        _pickaxeSlamCooldownRemaining = _PickaxeSlamCooldown;
        EndCast();
    }

    private IEnumerator EarthBreakerRoutine(Transform target)
    {
        BeginCast(target);

        Vector3 start = transform.position;
        Vector3 end = target != null ? target.position : start + transform.forward * _EarthBreakerRange;
        end.y = start.y;

        float elapsed = 0f;
        while (elapsed < _EarthBreakerLeapDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _EarthBreakerLeapDuration);
            MoveByWarp(Vector3.Lerp(start, end, t));
            yield return null;
        }

        DealRadiusDamage(_CuBotTemplate.AbilityR, _EarthBreakerImpactRadius, 1.0f);
        SpawnCrackedArea(transform.position);

        _earthBreakerCooldownRemaining = _EarthBreakerCooldown;
        EndCast();
    }

    private IEnumerator FlashPhotographyRoutine(Transform target)
    {
        BeginCast(target);
        _flashUsedThisPhase = true;
        _eyeContactTimer = 0f;

        yield return new WaitForSeconds(_FlashWindupDuration);

        if (IsPlayerFacingLuxion() && IsPlayerInFlashRange() && HasLineOfSight(_playerTarget))
            Mb_LuxionBlindOverlay.RequestBlind(_BlindDuration);

        EndCast();
    }

    private IEnumerator PhaseTransitionRoutine(LuxionPhase nextPhase)
    {
        _isTransitioning = true;
        _isCasting = true;
        StopAgent();

        if (Health != null)
            Health.IsUntargetable = true;

        yield return new WaitForSeconds(_PhaseTransitionDuration);

        _currentPhase = nextPhase;
        _flashUsedThisPhase = false;
        _eyeContactTimer = 0f;
        ApplyPhaseModifiers(nextPhase);

        if (Health != null)
            Health.IsUntargetable = false;

        _isCasting = false;
        _isTransitioning = false;
    }

    private void DealArcDamage(SO_Ability abilityData, float radius, float arcDegrees, float fallbackMultiplier)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up, radius);
        float halfArc = arcDegrees * 0.5f;

        foreach (Collider hit in hits)
        {
            Mb_CharacterBase target = hit.GetComponentInParent<Mb_CharacterBase>();
            if (!IsValidDamageTarget(target)) continue;

            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f) continue;
            if (Vector3.Angle(transform.forward, direction.normalized) > halfArc) continue;

            target.Health.TakeDamage(GetDamage(abilityData, fallbackMultiplier));
            break;
        }
    }

    private void DealRadiusDamage(SO_Ability abilityData, float radius, float fallbackMultiplier)
    {
        DealRadiusDamage(abilityData, radius, fallbackMultiplier, null);
    }

    private void DealRadiusDamage(
        SO_Ability abilityData,
        float radius,
        float fallbackMultiplier,
        HashSet<Mb_CharacterBase> damagedTargets)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up, radius);
        float damage = GetDamage(abilityData, fallbackMultiplier);

        foreach (Collider hit in hits)
        {
            Mb_CharacterBase target = hit.GetComponentInParent<Mb_CharacterBase>();
            if (!IsValidDamageTarget(target)) continue;
            if (damagedTargets != null && !damagedTargets.Add(target)) continue;

            target.Health.TakeDamage(damage);
        }
    }

    private void FireProjectileAt(Transform target, SO_Ability abilityData, float fallbackMultiplier)
    {
        if (_projectileLauncher == null || _projectilePrefab == null || target == null) return;

        SO_ProjectileData projectileData = ResolveProjectileData(abilityData);
        if (projectileData == null) return;

        Vector3 direction = GetDirectionToTarget(target);
        _projectileLauncher.FireToward(
            _projectilePrefab,
            projectileData,
            this,
            GetDamage(abilityData, fallbackMultiplier),
            direction
        );
    }

    private void FireProjectileSpread(Vector3 centerDirection, SO_Ability abilityData)
    {
        if (_projectileLauncher == null || _projectilePrefab == null) return;

        SO_ProjectileData projectileData = ResolveProjectileData(abilityData);
        if (projectileData == null) return;

        int projectileCount = Mathf.Max(1, _BulletRainProjectilesPerVolley);
        float damage = GetDamage(abilityData, 0.55f);

        for (int i = 0; i < projectileCount; i++)
        {
            float t = projectileCount == 1 ? 0.5f : i / (float)(projectileCount - 1);
            float angle = Mathf.Lerp(-_BulletRainSpreadAngle * 0.5f, _BulletRainSpreadAngle * 0.5f, t);
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * centerDirection;

            _projectileLauncher.FireToward(
                _projectilePrefab,
                projectileData,
                this,
                damage,
                direction
            );
        }
    }

    private void SpawnCrackedArea(Vector3 position)
    {
        GameObject crackedAreaObject = new GameObject("Luxion Cracked Area");
        crackedAreaObject.transform.position = position;

        SphereCollider collider = crackedAreaObject.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = _CrackedAreaRadius;

        Rigidbody rigidbody = crackedAreaObject.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;

        Mb_LuxionCrackedArea crackedArea = crackedAreaObject.AddComponent<Mb_LuxionCrackedArea>();
        crackedArea.Initialize(
            GetDamage(_CuBotTemplate.AbilityR, 0.2f),
            _CrackedAreaTickInterval,
            _CrackedAreaDuration
        );
    }

    private void TryStartPhaseTransition()
    {
        if (_isTransitioning || Health == null) return;

        LuxionPhase desiredPhase = GetDesiredPhase();
        if (desiredPhase == _currentPhase) return;

        StartCoroutine(PhaseTransitionRoutine(desiredPhase));
    }

    private LuxionPhase GetDesiredPhase()
    {
        float healthRatio = Health.GetMaxHealth() > 0f
            ? Health.CurrentHealth / Health.GetMaxHealth()
            : 1f;

        if (healthRatio <= _ConsumptionHealthThreshold)
            return LuxionPhase.Consumption;

        if (healthRatio <= _AcquisitionHealthThreshold)
            return LuxionPhase.Acquisition;

        return LuxionPhase.Extraction;
    }

    private void ApplyPhaseModifiers(LuxionPhase phase)
    {
        ClearConsumptionModifier();

        if (phase != LuxionPhase.Consumption || Stats == null) return;

        _consumptionModifier = new Sc_Modifier(
            "Luxion Consumption Phase",
            ModifierSource.Ability,
            new List<Sc_StatEffect>
            {
                new Sc_StatEffect(StatType.MoveSpeed, _ConsumptionMoveSpeedMultiplier - 1f, StatModType.Percent),
                new Sc_StatEffect(StatType.MaxHealth, _ConsumptionMaxHealthBonus, StatModType.Percent)
            }
        );

        Stats.AddModifier(_consumptionModifier);
    }

    private void ClearConsumptionModifier()
    {
        if (_consumptionModifier == null || Stats == null) return;

        Stats.RemoveModifier(_consumptionModifier);
        _consumptionModifier = null;
    }

    private bool CanUseFlashPhotography(Transform target)
    {
        return !_flashUsedThisPhase &&
               target != null &&
               _eyeContactTimer >= _EyeContactRequiredDuration &&
               IsPlayerInFlashRange() &&
               HasLineOfSight(target);
    }

    private void UpdateFlashEyeContact()
    {
        if (_flashUsedThisPhase || _playerTarget == null)
        {
            _eyeContactTimer = 0f;
            return;
        }

        if (IsPlayerInFlashRange() && IsPlayerFacingLuxion() && HasLineOfSight(_playerTarget))
            _eyeContactTimer += Time.deltaTime;
        else
            _eyeContactTimer = 0f;
    }

    private bool IsPlayerFacingLuxion()
    {
        if (_playerTarget == null) return false;

        Vector3 directionToLuxion = transform.position - _playerTarget.position;
        directionToLuxion.y = 0f;

        if (directionToLuxion.sqrMagnitude <= 0.001f) return false;

        return Vector3.Dot(_playerTarget.forward, directionToLuxion.normalized) >= _FacingLuxionDotThreshold;
    }

    private bool IsPlayerInFlashRange()
    {
        return _playerTarget != null &&
               Vector3.Distance(transform.position, _playerTarget.position) <= _FlashRange;
    }

    private bool HasLineOfSight(Transform target)
    {
        if (target == null) return false;

        Vector3 origin = transform.position + Vector3.up * _TargetHeightOffset;
        Vector3 targetPosition = target.position + Vector3.up * _TargetHeightOffset;
        Vector3 direction = targetPosition - origin;
        float distance = direction.magnitude;

        if (distance <= 0.01f) return true;

        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance, _LineOfSightMask))
            return hit.transform == target || hit.transform.IsChildOf(target);

        return true;
    }

    private bool IsValidDamageTarget(Mb_CharacterBase target)
    {
        return target != null &&
               target != this &&
               target.Health != null &&
               !target.Health.IsDead &&
               !target.CompareTag("CuBot");
    }

    private Transform GetActiveTarget()
    {
        if (_playerTarget != null)
            return _playerTarget;

        return _panoharraTarget;
    }

    private Vector3 GetDirectionToTarget(Transform target)
    {
        if (target == null) return transform.forward;

        Vector3 targetPosition = target.position + Vector3.up * _TargetHeightOffset;
        Vector3 direction = targetPosition - transform.position;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
    }

    private SO_ProjectileData ResolveProjectileData(SO_Ability abilityData)
    {
        if (abilityData != null && abilityData.ProjectileData != null)
            return abilityData.ProjectileData;

        if (_CuBotTemplate.SecondaryAttack != null && _CuBotTemplate.SecondaryAttack.ProjectileData != null)
            return _CuBotTemplate.SecondaryAttack.ProjectileData;

        return _CuBotTemplate.PrimaryAttack != null ? _CuBotTemplate.PrimaryAttack.ProjectileData : null;
    }

    private float GetDamage(SO_Ability abilityData, float fallbackAtkMultiplier)
    {
        if (abilityData != null &&
            abilityData.ScalingStats != null &&
            abilityData.ScalingStats.Count > 0)
        {
            return abilityData.GetStat(
                "Damage",
                1,
                Stats.AttackPower.GetValue(),
                Stats.AbilityPower.GetValue()
            );
        }

        return Stats.AttackPower.GetValue() * Mathf.Max(0f, fallbackAtkMultiplier);
    }

    private void BeginCast(Transform target)
    {
        _isCasting = true;
        StopAgent();

        if (target != null)
            FaceTarget(target);

        _cuBotAnimator?.TriggerAttack();
    }

    private void EndCast()
    {
        _isCasting = false;
    }

    private void FaceTarget(Transform target)
    {
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void MoveToward(Vector3 destination, float stoppingDistance)
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;

        _agent.isStopped = false;
        _agent.stoppingDistance = stoppingDistance;
        _agent.SetDestination(destination);
    }

    private void StopAgent()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;

        _agent.isStopped = true;
        _agent.ResetPath();
    }

    private void MoveByWarp(Vector3 position)
    {
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.Warp(position);
            return;
        }

        transform.position = position;
    }

    private void RefreshAgentMoveSpeed()
    {
        if (_agent == null || Stats == null || Stats.MoveSpeed == null) return;

        _agent.speed = Mathf.Max(0f, Stats.MoveSpeed.GetValue());
    }

    private void FindTargets()
    {
        if (_playerTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _playerTarget = player.transform;
        }

        if (_panoharraTarget == null)
        {
            GameObject panoharra = GameObject.FindGameObjectWithTag("Panoharra");
            if (panoharra != null)
                _panoharraTarget = panoharra.transform;
        }
    }

    private void TickCooldowns()
    {
        float deltaTime = Time.deltaTime;

        _axeSlashCooldownRemaining = Mathf.Max(0f, _axeSlashCooldownRemaining - deltaTime);
        _spinHarvestCooldownRemaining = Mathf.Max(0f, _spinHarvestCooldownRemaining - deltaTime);
        _rifleShotCooldownRemaining = Mathf.Max(0f, _rifleShotCooldownRemaining - deltaTime);
        _bulletRainCooldownRemaining = Mathf.Max(0f, _bulletRainCooldownRemaining - deltaTime);
        _pickaxeSlamCooldownRemaining = Mathf.Max(0f, _pickaxeSlamCooldownRemaining - deltaTime);
        _earthBreakerCooldownRemaining = Mathf.Max(0f, _earthBreakerCooldownRemaining - deltaTime);
    }

    private void ResetCooldowns()
    {
        _axeSlashCooldownRemaining = 0f;
        _spinHarvestCooldownRemaining = 0f;
        _rifleShotCooldownRemaining = 0f;
        _bulletRainCooldownRemaining = 0f;
        _pickaxeSlamCooldownRemaining = 0f;
        _earthBreakerCooldownRemaining = 0f;
    }

    private void UpdateAnimator()
    {
        if (_cuBotAnimator == null || _agent == null) return;

        _cuBotAnimator.SetSpeed(_agent.velocity.magnitude);
    }
}

public enum LuxionPhase
{
    Extraction,
    Acquisition,
    Consumption
}
