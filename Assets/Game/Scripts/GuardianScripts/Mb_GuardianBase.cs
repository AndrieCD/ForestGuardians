// GuardianBase_Sc.cs
/// <summary>
/// The base class for all Guardian characters in the game.
/// </summary>
using UnityEngine;
using System;

public abstract class Mb_GuardianBase : MonoBehaviour, I_Damageable
{
    public SO_Guardian _GuardianTemplate;
    protected string _GuardianName;

    // STATS //
    public Sc_Stat MaxHealth { get; set; }          // Maximum health points
    public Sc_Stat HealthRegen { get; set; }        // Health regenerated per second
    public Sc_Stat MoveSpeed { get; set; }          // Units per second
    public Sc_Stat AttackSpeed { get; set; }        // Attacks per second
    public Sc_Stat AttackPower { get; set; }        // "Physical" attack damage
    public Sc_Stat AbilityPower { get; set; }       // "Magical" ability damage
    public Sc_Stat CooldownReduction { get; set; }  // Percentage reduction in ability cooldowns
    public Sc_Stat CriticalChance { get; set; }     // Chance to deal critical damage (0.0 to 1.0)
    public Sc_Stat CriticalDamage { get; set; }     // Critical damage multiplier
    public Sc_Stat Lifesteal { get; set; }          // Percentage of damage dealt returned as health
    public Sc_Stat CurrentShield { get; set; }      // Current shield value

    [Header("Abilities")]
    protected Sc_BaseAbility _PassiveAbility; // Passive Ability
    protected Sc_BaseAbility _QAbility;       // Q Ability
    protected Sc_BaseAbility _EAbility;       // E Ability 
    protected Sc_BaseAbility _RAbility;       // R Ability 


    // Runtime Stats (These change during gameplay)
    protected float _CurrentHealth;
    public bool IsDead { get; private set; }

    #region Events
    //// Events for UI or Sound to listen to
    //public event Action<float> OnHealthChanged;
    //public event Action OnDeath;
    #endregion

    protected virtual void Awake( )
    {
        if (_GuardianTemplate != null)
            InitializeGuardian( );
    }

    // Reset stats to the base values from the ScriptableObject
    public virtual void InitializeGuardian( )
    {
        // Init the wrappers using the immutable data from the SO
        _GuardianName = _GuardianTemplate.name;
        MaxHealth = new Sc_Stat(_GuardianTemplate.MaxHealth);
        HealthRegen = new Sc_Stat(_GuardianTemplate.HealthRegen);
        MoveSpeed = new Sc_Stat(_GuardianTemplate.MoveSpeed);
        AttackSpeed = new Sc_Stat(_GuardianTemplate.AttackSpeed);
        AttackPower = new Sc_Stat(_GuardianTemplate.AttackPower);
        AbilityPower = new Sc_Stat(_GuardianTemplate.AbilityPower);
        CooldownReduction = new Sc_Stat(_GuardianTemplate.CooldownReduction);
        CriticalChance = new Sc_Stat(_GuardianTemplate.CriticalChance);
        CriticalDamage = new Sc_Stat(_GuardianTemplate.CriticalDamage);
        Lifesteal = new Sc_Stat(_GuardianTemplate.LifeSteal);
        CurrentShield = new Sc_Stat(_GuardianTemplate.CurrentShield);


        // Initialize Abilities
        _PassiveAbility = new Passive_Ability(_GuardianTemplate.PassiveAbility);
        _QAbility = new Q_Ability(_GuardianTemplate.AbilityQ);
        _EAbility = new Rajah_E_Ability(_GuardianTemplate.AbilityE);
        _RAbility = new R_Ability(_GuardianTemplate.AbilityR);

        // Set current health to max at start
        _CurrentHealth = MaxHealth.Value();
        IsDead = false;
    }

    /// <summary>
    /// Applies damage to the Guardian.
    /// </summary>
    public virtual void TakeDamage(float amount)
    {
        if (IsDead) return;

        _CurrentHealth -= amount;

        // Update UI
        //OnHealthChanged?.Invoke(_CurrentHealth / baseStats.maxHealth);

        if (_CurrentHealth <= 0)
        {
            Die( );
        }
    }

    /// <summary>
    /// Heals the Guardian by the specified amount.
    /// </summary>
    public virtual void Heal(float amount)
    {
        if (IsDead) return;
        _CurrentHealth += amount;
        _CurrentHealth = Mathf.Min(_CurrentHealth, MaxHealth.Value( )); // Clamp to max health if healing exceeds it
    }

    protected virtual void Die( )
    {
        IsDead = true;
        //OnDeath?.Invoke( );
        Debug.Log($"{gameObject.name} has died.");
        // Standard cleanup or animation trigger here
    }
}