using UnityEngine;

/// <summary>
/// Base class for all playable Guardian characters.
/// Reads from SO_Guardian to populate Stats and Health via the component system.
///
/// Derived classes: Mb_PlayerController (and future guardian-specific classes)
/// </summary>
public abstract class Mb_GuardianBase : Mb_CharacterBase
{
    [Header("Guardian Template")]
    [SerializeField] protected SO_Guardian _GuardianTemplate;

    public Mb_GuardianAnimator GuardianAnimator;
    protected override void Awake()
    {
        // Base Awake fetches all components (Stats, Health, Abilities, Movement)
        // then calls InitializeFromTemplate() below
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

        // Populate all stats from the ScriptableObject
        Stats.BuildFromTemplate(_GuardianTemplate);

        // Set health to full and clear the dead flag
        // Must happen AFTER Stats.BuildFromTemplate() so MaxHealth is ready
        Health.Initialize();

        // Subscribe to death so we can respond (play animation, trigger game over, etc.)
        Health.OnDeath += HandleDeath;

        // Wire up ability slots — derived classes override this to assign
        // the correct ability implementations for their specific guardian
        AssignAbilities();
    }

    /// <summary>
    /// Override in each guardian subclass to assign ability slots.
    /// Example: Abilities.SetSlots(new Passive_Ability(...), new Rajah_Q_Ability(...), ...)
    /// </summary>
    protected abstract void AssignAbilities();

    /// <summary>
    /// Called when Health.OnDeath fires.
    /// Override in subclasses to add specific death behavior (animations, game over, etc.)
    /// </summary>
    protected virtual void HandleDeath()
    {
        Debug.Log($"[{_CharacterName}] Guardian has died.");
        // TODO: Trigger death animation, fire game over event, etc.
    }
}