using System;
using UnityEngine;

public class MB_CuBotBase : Mb_CharacterBase
{
    [Header("CuBot Template")]
    [SerializeField] protected SO_CuBots _CuBotTemplate;

    #region Events
    // EVENTS
    public static event Action OnCuBotSpawn; // Event triggered when a CuBot spawns, can be used to update UI, trigger effects, etc.
    public static event Action OnCuBotDeath; // Event triggered when a CuBot dies, can be used to update UI, trigger effects, etc.
    #endregion

    #region Abilities
    protected Sc_BaseAbility _PrimaryAttack;
    #endregion


    #region Initializations
    protected override void InitializeFromTemplate( )
    {
        if (_CuBotTemplate == null) return;

        _CharacterName = _CuBotTemplate.name;

        MaxHealth = new Sc_Stat(_CuBotTemplate.MaxHealth);
        HealthRegen = new Sc_Stat(_CuBotTemplate.HealthRegen);
        MoveSpeed = new Sc_Stat(_CuBotTemplate.MoveSpeed);
        AttackSpeed = new Sc_Stat(_CuBotTemplate.AttackSpeed);
        AttackPower = new Sc_Stat(_CuBotTemplate.AttackPower);
        AbilityPower = new Sc_Stat(_CuBotTemplate.AbilityPower);
        CooldownReduction = new Sc_Stat(_CuBotTemplate.CooldownReduction);
        CriticalChance = new Sc_Stat(_CuBotTemplate.CriticalChance);
        CriticalDamage = new Sc_Stat(_CuBotTemplate.CriticalDamage);
        Lifesteal = new Sc_Stat(_CuBotTemplate.LifeSteal);
        Shielding = new Sc_Stat(_CuBotTemplate.Shielding);

        _CurrentHealth = MaxHealth.Value( );
        IsDead = false;
        InitializeAbilities( );
    }

    private void InitializeAbilities( )
    {
        // Equip Abilities (Starts Passives)
        _PrimaryAttack?.OnEquip(this);
    }
    #endregion


    #region OnEnable/OnDisable
    private void OnEnable( )
    {
        OnCuBotSpawn?.Invoke( );
        // Reset the CuBot's state when it is enabled (spawned)
        Reset( );
    }
    
    #endregion


    // For prototyping, CuBot takes damage from projectiles on collision //
    private void OnCollisionEnter(Collision collision)
    {
        Mb_Projectile projectile = collision.gameObject.GetComponent<Mb_Projectile>( );
        if (projectile == null) return;

        TakeDamage(projectile.GetDamageAmount( ));
    }

    protected void TryUsePrimaryAttack( )
    {
        _PrimaryAttack?.Activate(this);
    }

    protected override void Die( )
    {
        base.Die( );
        gameObject.SetActive(false);    // Deactivate the CuBot GameObject in the pool instead of destroying it, so it can be reused later.
        OnCuBotDeath?.Invoke();
    }


    // Reset restores the character to its initial state, including health and modifiers. Can be called when respawning.
    protected virtual void Reset( )
    {
        InitializeFromTemplate( );
    }
}
