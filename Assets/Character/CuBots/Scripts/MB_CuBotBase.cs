using System;
using UnityEngine;

/// <summary>
/// Base class for all CuBot enemy types.
/// Reads from SO_CuBots to populate Stats and Health via the component system.
///
/// Uses object pooling — instead of being destroyed on death, CuBots are
/// deactivated and Reset() when reactivated from the pool.
///
/// Derived classes: Mb_Chopper, Mb_Hunter, Mb_Minny, etc.
/// </summary>
public class MB_CuBotBase : Mb_CharacterBase
{
    [Header("CuBot Template")]
    [SerializeField] protected SO_CuBots _CuBotTemplate;

    // Tracks whether Awake has completed so OnEnable knows if it's safe to Reset()
    private bool _isInitialized = false;

    #region Events
    // Static events so WaveManager can listen without a direct reference to each CuBot
    public static event Action OnCuBotSpawn;
    public static event Action OnCuBotDeath;
    #endregion

    protected override void Awake()
    {
        // Base Awake fetches Stats, Health, Abilities, Movement components
        // then calls InitializeFromTemplate()
        base.Awake();
        _isInitialized = true;
    }

    private void OnEnable()
    {
        OnCuBotSpawn?.Invoke();

        // Guard: skip Reset on the very first enable because Awake hasn't run yet.
        // On first spawn, Awake handles initialization. On every pool reuse after
        // that, Reset() re-runs so the CuBot starts clean.
        if (_isInitialized)
            Reset();
    }

    /// <summary>
    /// Populates stats and health from the CuBot SO.
    /// Called automatically by Mb_CharacterBase.Awake() on first spawn.
    /// Also called by Reset() on every subsequent pool reuse.
    /// </summary>
    protected override void InitializeFromTemplate()
    {
        if (_CuBotTemplate == null)
        {
            Debug.LogError($"[MB_CuBotBase] No SO_CuBots template assigned on {gameObject.name}.");
            return;
        }

        _CharacterName = _CuBotTemplate.name;

        // Clear any leftover stat effects from the previous wave before repopulating
        Stats.RemoveAllModifiers();

        // Repopulate stats from the ScriptableObject base values
        Stats.BuildFromTemplate(_CuBotTemplate);

        // Restore full health and clear the dead flag.
        // Must happen AFTER Stats.BuildFromTemplate() so MaxHealth is ready.
        Health.Initialize();

        // Unsubscribe before resubscribing to avoid stacking duplicate listeners
        // across multiple pool reuses
        Health.OnDeath -= HandleDeath;
        Health.OnDeath += HandleDeath;

        // Let derived classes assign their specific ability
        AssignAbilities();
    }

    /// <summary>
    /// Override in derived CuBot classes to assign their primary attack.
    /// Called every time InitializeFromTemplate() runs, including pool reuses.
    /// </summary>
    protected virtual void AssignAbilities()
    {
        // Base has no ability — derived classes override this
    }

    /// <summary>
    /// Resets the CuBot fully so it's clean for reuse from the pool.
    /// </summary>
    protected virtual void Reset()
    {
        InitializeFromTemplate();
    }

    private void HandleDeath()
    {
        gameObject.SetActive(false);
        OnCuBotDeath?.Invoke();
    }

    // For prototyping: CuBot takes damage when a projectile collides with it
    private void OnCollisionEnter(Collision collision)
    {
        Mb_Projectile projectile = collision.gameObject.GetComponent<Mb_Projectile>();
        if (projectile == null) return;

        Health.TakeDamage(projectile.GetDamageAmount());
    }

    /// <summary>
    /// Called from derived CuBot AI logic to fire the primary attack.
    /// Routed through AbilityController so pause blocking applies automatically.
    /// </summary>
    protected void TryUsePrimaryAttack()
    {
        Abilities.ActivatePrimaryAsAI();
    }
}