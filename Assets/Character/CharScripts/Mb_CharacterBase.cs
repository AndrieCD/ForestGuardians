using System.Collections.Generic;
using UnityEngine;

public abstract class Mb_CharacterBase : MonoBehaviour, I_Damageable, I_StatModifiable
{
    [Header("Identity")]
    protected string _CharacterName;
    public Mb_Movement Movement { get; protected set; }

    protected int _CharacterLevel;  // Starts at 1 on wave 1, increases by 1 each wave up to level 15

    #region Stats
    public Sc_Stat MaxHealth { get; protected set; }
    public Sc_Stat HealthRegen { get; protected set; }
    public Sc_Stat MoveSpeed { get; protected set; }
    public Sc_Stat AttackSpeed { get; protected set; }
    public Sc_Stat AttackPower { get; protected set; }
    public Sc_Stat AbilityPower { get; protected set; }
    public Sc_Stat CooldownReduction { get; protected set; }
    public Sc_Stat CriticalChance { get; protected set; }
    public Sc_Stat CriticalDamage { get; protected set; }
    public Sc_Stat Lifesteal { get; protected set; }
    public Sc_Stat Shielding { get; protected set; }
    #endregion

    #region StatScaling
    protected Dictionary<StatType, float> _StatScaling = new Dictionary<StatType, float>( );
    #endregion

    #region Runtime
    protected float _CurrentHealth;
    public bool IsDead { get; protected set; }
    #endregion

    #region Modifiers
    protected readonly List<Sc_Modifier> _ActiveModifiers = new( );
    #endregion

    protected virtual void Awake( )
    {
        InitializeFromTemplate( );
    }


    /// <summary>
    /// Called during Awake. Derived classes must initialize stats here.
    /// </summary>
    protected abstract void InitializeFromTemplate( );

    #region Damage & Healing
    public virtual void TakeDamage(float amount)
    {
        Debug.Log($"{_CharacterName} takes {amount} damage.");

        if (IsDead) return;

        _CurrentHealth -= amount;

        if (_CurrentHealth <= 0f)
            Die( );
    }

    public virtual void Heal(float amount)
    {
        if (IsDead) return;

        _CurrentHealth = Mathf.Min(
            _CurrentHealth + amount,
            MaxHealth.Value( )
        );
    }


    // Die method can be overridden by derived classes to implement custom death behavior
    protected virtual void Die( )
    {
        IsDead = true;
        Debug.Log($"{_CharacterName} has died.");
    }
    #endregion

    protected abstract void LevelUp( );

    #region Modifiers
    public void AddModifier(Sc_Modifier modifier)
    {
        if (!_ActiveModifiers.Contains(modifier))
            _ActiveModifiers.Add(modifier);
    }

    public void RemoveModifier(Sc_Modifier modifier)
    {
        _ActiveModifiers.Remove(modifier);
    }

    public void ClearAllModifiers( )
    {
        _ActiveModifiers.Clear( );
    }
    #endregion
}
