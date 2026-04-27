// Mb_GuardianBase.cs
// Base class for all playable Guardian characters.
// Reads from SO_Guardian to populate Stats and Health via the component system.
//
// Derived classes: Mb_PlayerController (Rajah Bagwis), and future guardian classes.

using UnityEngine;

public abstract class Mb_GuardianBase : Mb_CharacterBase
{
    [Header("Guardian Template")]
    [SerializeField] protected SO_Guardian _GuardianTemplate;

    public Mb_GuardianAnimator GuardianAnimator;


    protected override void Awake()
    {
        base.Awake();
        GuardianAnimator = GetComponent<Mb_GuardianAnimator>();
    }


    /// <summary>
    /// Populates stats and health from the Guardian SO, then wires up abilities.
    /// Called automatically by Mb_CharacterBase.Awake().
    /// </summary>
    protected override void InitializeFromTemplate()
    {
        if (_GuardianTemplate == null)
        {
            Debug.LogError($"[Mb_GuardianBase] No SO_Guardian template assigned on {gameObject.name}.");
            return;
        }

        _CharacterName = _GuardianTemplate.name;

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
    }
}