using UnityEngine;

/// <summary>
/// Drilly - burrowing kamikaze melee CuBot.
/// Starts burrowed and untargetable, then surfaces near its target before launching
/// toward a snapshotted target position.
/// </summary>
public class Mb_DrillyController : Mb_CuBotController
{
    [Header("Burrow Visuals")]
    [SerializeField] private Transform _VisualRoot;
    [SerializeField] private float _BurrowedVisualYOffset = -0.75f;

    [Header("Burrow Strike")]
    [SerializeField] private float _WindupDuration = 0.75f;
    [SerializeField] private float _LaunchDuration = 0.35f;
    [SerializeField] private float _ImpactRadius = 1.5f;

    private Vector3 _visualRootDefaultLocalPosition;
    private bool _hasTriggeredAttack;

    protected override void Awake()
    {
        base.Awake();

        if (_VisualRoot == null && transform.childCount > 0)
            _VisualRoot = transform.GetChild(0);

        if (_VisualRoot != null)
            _visualRootDefaultLocalPosition = _VisualRoot.localPosition;
    }

    protected override void AssignAbilities()
    {
        if (_CuBotTemplate.PrimaryAttack == null)
        {
            Debug.LogError("[Mb_DrillyController] PrimaryAttack SO_Ability is not assigned on the SO_CuBots template.");
            return;
        }

        Abilities.SetPrimarySlot(new Sc_DrillyBurrowStrike(
            _CuBotTemplate.PrimaryAttack,
            this,
            getCurrentTarget: () => _CurrentTarget,
            onSurface: BeginAttackVulnerableState,
            windupDuration: _WindupDuration,
            launchDuration: _LaunchDuration,
            impactRadius: _ImpactRadius
        ));
    }

    protected override void OnInAttackRange()
    {
        if (_hasTriggeredAttack) return;

        _hasTriggeredAttack = true;

        if (_CurrentTarget != null)
            transform.LookAt(_CurrentTarget);

        if (_Agent != null && _Agent.enabled && _Agent.isOnNavMesh)
        {
            _Agent.isStopped = true;
            _Agent.ResetPath();
        }

        TryUsePrimaryAttack();
    }

    protected override void UpdateAnimator()
    {
        if (_BasicCuBotAnimator == null || _Agent == null) return;

        _BasicCuBotAnimator.SetSpeed(_Agent.velocity.magnitude);
    }

    protected override void OnControllerReset()
    {
        _hasTriggeredAttack = false;
        SetBurrowedState(true);
    }

    protected override bool ShouldHoldMovement => _hasTriggeredAttack;

    private void BeginAttackVulnerableState()
    {
        SetBurrowedState(false);
    }

    private void SetBurrowedState(bool isBurrowed)
    {
        if (Health != null)
            Health.IsUntargetable = isBurrowed;

        if (_VisualRoot == null) return;

        Vector3 offset = isBurrowed
            ? Vector3.up * _BurrowedVisualYOffset
            : Vector3.zero;

        _VisualRoot.localPosition = _visualRootDefaultLocalPosition + offset;
    }
}
