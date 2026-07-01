// Mb_GuardianBase.cs
// Base class for all playable Guardian characters.
// Reads from SO_Guardian to populate Stats and Health via the component system.
//
// Derived classes: Mb_PlayerController (Rajah Bagwis), and future guardian classes.

using System;
using UnityEngine;

public abstract class Mb_GuardianBase : Mb_CharacterBase
{
    public static Mb_GuardianBase CurrentGuardian { get; private set; }
    public static event Action<Mb_GuardianBase> OnActiveGuardianChanged;

    [Header("Guardian Template")]
    [SerializeField] protected SO_Guardian _GuardianTemplate;

    public Mb_GuardianAnimator GuardianAnimator;
    public SO_Guardian GuardianTemplate => _GuardianTemplate;

    // On Mb_GuardianBase or Mb_PlayerController:
    [SerializeField] public Transform ProjectileOrigin;

    protected override void Awake()
    {
        Sc_BuildLogger.Trace($"Awake called on {gameObject.name} ({GetType().Name}).");
        base.Awake();
        GuardianAnimator = GetComponent<Mb_GuardianAnimator>();

        SetCurrentGuardian(this);
    }

    protected virtual void OnEnable()
    {
        SetCurrentGuardian(this);
    }

    protected virtual void OnDisable()
    {
        if (CurrentGuardian == this)
            SetCurrentGuardian(null);
    }

    private static void SetCurrentGuardian(Mb_GuardianBase guardian)
    {
        if (CurrentGuardian == guardian)
            return;

        CurrentGuardian = guardian;
        OnActiveGuardianChanged?.Invoke(CurrentGuardian);
    }

    /// <summary>
    /// Populates stats and health from the Guardian SO, then wires up abilities.
    /// Called automatically by Mb_CharacterBase.Awake().
    /// </summary>
    protected override void InitializeFromTemplate()
    {
        Sc_BuildLogger.Trace($"[{gameObject.name}] Initializing from template.");

        if (_GuardianTemplate == null)
        {
            Debug.LogError($"[Mb_GuardianBase] No SO_Guardian template assigned on {gameObject.name}.");
            return;
        }

        _CharacterName = _GuardianTemplate.name;

        Debug.Log($"Health Regen: {_GuardianTemplate.HealthRegen}");

        Stats.BuildFromTemplate(_GuardianTemplate);

        // Must happen AFTER Stats.BuildFromTemplate() so MaxHealth is ready
        Health.Initialize();

        Health.OnDeath += HandleDeath;

        AssignAbilities();
    }


    /// <summary>
    /// Override in each guardian subclass to assign ability slots.
    /// </summary>
    protected abstract void AssignAbilities();


    /// <summary>
    /// Override in each guardian subclass to define the two ultimate branch options.
    /// Each option pairs display data (SO_UltimateBranch) with a factory delegate
    /// that creates the correct ability instance when the player makes their choice.
    ///
    /// The base class returns (null, null) so guardians without an ultimate
    /// (e.g. during early development) don't crash the rewards system.
    /// Mb_RewardsManager checks for null before opening the branch panel.
    /// </summary>
    protected virtual (Sc_BranchOption branch1, Sc_BranchOption branch2) DefineBranches()
    {
        return (null, null);
    }


    /// <summary>
    /// Called by Mb_RewardsManager to retrieve this guardian's branch options.
    /// Returns the result of DefineBranches() — no casting or guardian-specific
    /// logic needed in the rewards system.
    /// </summary>
    public (Sc_BranchOption branch1, Sc_BranchOption branch2) GetBranchOptions()
    {
        return DefineBranches();
    }


    /// <summary>
    /// Called when Health.OnDeath fires.
    /// Override in subclasses to add specific death behavior.
    /// </summary>
    protected virtual void HandleDeath()
    {
        Debug.Log($"[{_CharacterName}] Guardian has died.");
        // TODO: Trigger death animation, fire game over event, etc.
        GuardianAnimator?.TriggerDefeat();
    }


    // Add to Mb_PlayerController.cs
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_GuardianTemplate == null) return;

        Vector3 center = transform.position + Vector3.up * .8f; // adjust this value

        // Primary slash hitbox
        Gizmos.color = Color.red;
        Vector3 slashCenter = center + transform.forward * 1.5f;
        Gizmos.DrawWireSphere(slashCenter, 1.5f);

        // Q dash hit radius
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, 1.4f);

        // R1 slashes hitbox
        float HIT_RADIUS = 5f;
        float HIT_OFFSET = 5f;
        Gizmos.color = Color.yellow;
        center = transform.position + transform.forward * HIT_OFFSET;
        Gizmos.DrawWireSphere(center, HIT_RADIUS);


        // Feet
        Vector3 feet = transform.position + Vector3.up * 0.0f; // adjust this value

        // FEET radius
        Gizmos.color = Color.pink;
        Gizmos.DrawWireSphere(feet, 0.2f);
    }
#endif
}
