// GuardianBase_Sc.cs
/// <summary>
/// The base class for all Guardian characters in the game.
/// </summary>
using UnityEngine;
using System.Collections.Generic;
using System;

public abstract class Mb_GuardianBase : Mb_CharacterBase
{
    [Header("Guardian Template")]
    [SerializeField] protected SO_Guardian _GuardianTemplate;

    public Sc_Stat JumpPower { get; protected set; }

    #region Abilities
    protected Sc_BaseAbility _PassiveAbility;
    protected Sc_BaseAbility _QAbility;
    protected Sc_BaseAbility _EAbility;
    protected Sc_BaseAbility _RAbility;
    protected Sc_BaseAbility _PrimaryAttack;
    protected Sc_BaseAbility _SecondaryAttack;
    #endregion

    #region Events
    //// Events for UI or Sound to listen to
    public event Action<float> OnHealthChanged;
    public event Action OnDeath;
    #endregion

    protected override void Awake( )
    {
        if (_GuardianTemplate != null)
            InitializeFromTemplate( );
        this.Movement = GetComponent<Mb_Movement>( );
    }
    #region Initialization
    // Reset stats to the base values from the ScriptableObject
    protected override void InitializeFromTemplate( )
    {
        // Init the wrappers using the immutable data from the SO
        _CharacterName = _GuardianTemplate.name;
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
        Shielding = new Sc_Stat(_GuardianTemplate.Shielding);
        JumpPower = new Sc_Stat(8f);    // ( 8 ) Default jump power


        // Initialize Abilities
        _PassiveAbility = new Passive_Ability(_GuardianTemplate.PassiveAbility, this);
        _QAbility = new Rajah_Q_Ability(_GuardianTemplate.AbilityQ, this);
        _EAbility = new Rajah_E_Ability(_GuardianTemplate.AbilityE, this);
        //_RAbility = new R_Ability(_GuardianTemplate.AbilityR, this);
        _PrimaryAttack = new Rajah_Primary(_GuardianTemplate.PrimaryAttack, this);
        _SecondaryAttack = new Rajah_Secondary(_GuardianTemplate.SecondaryAttack, this);

        // Set current health to max at start
        _CurrentHealth = MaxHealth.Value( );
        IsDead = false;
        InitializeAbilities( );
    }

    private void InitializeAbilities( )
    {
        // Equip Abilities (Starts Passives)
        _PassiveAbility?.OnEquip(this);
        _QAbility?.OnEquip(this);
        _EAbility?.OnEquip(this);
        _RAbility?.OnEquip(this);
    }
    #endregion
}