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


    #region Wave Scaling
    // Called by WaveManager right after activating this CuBot from the pool.
    // waveNumber is 0-indexed (wave 0 = no scaling, wave 1 = first scale-up, etc.)
    public void ApplyWaveScaling(int waveNumber)
    {
        if (_CuBotTemplate == null || waveNumber <= 0) return;

        // We scale HP and ATK using compound growth: base * (1 + rate)^wave, capped at max.
        // This matches Table 12 in the thesis design document.
        MaxHealth.BaseValue = ScaleStat(_CuBotTemplate.MaxHealth, _CuBotTemplate.MaxHealthScaling, waveNumber);
        AttackPower.BaseValue = ScaleStat(_CuBotTemplate.AttackPower, _CuBotTemplate.AttackPowerScaling, waveNumber);

        // Only scale AP if this CuBot type uses it (e.g. Bernie, Toxion)
        if (_CuBotTemplate.AbilityPower > 0)
        {
            AbilityPower.BaseValue = ScaleStat(_CuBotTemplate.AbilityPower, _CuBotTemplate.AbilityPowerScaling, waveNumber);
        }

        // After scaling MaxHealth, also set current health to the new max (fresh spawn)
        _CurrentHealth = MaxHealth.Value( );
    }

    // Applies compound growth capped at a maximum value.
    // Formula: scaledValue = baseValue * (1 + growthRate)^waveNumber
    private float ScaleStat(float baseValue, float growthRate, int waveNumber)
    {
        float scaled = baseValue * Mathf.Pow(1f + growthRate, waveNumber);
        return scaled;
    }
    #endregion


    #region OnEnable/OnDisable
    private void OnEnable( )
    {
        //Mb_WaveManager.OnWaveEnd += LevelUp;
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


    //protected override void LevelUp( )
    //{
    //    _CharacterLevel++;
    //    Debug.Log($"{_CharacterName} leveled up to level {_CharacterLevel}!");

    //    foreach (var statType in _StatScaling.Keys)
    //    {
    //        float scalingAmount = _StatScaling[statType];   // in percentage
    //        if (scalingAmount != 0)
    //        {
    //            // Increase the base value of the stat according to the scaling amount
    //            switch (statType)
    //            {
    //                case StatType.MaxHealth:
    //                    MaxHealth.BaseValue *= ( 1 + scalingAmount );
    //                    break;
    //                case StatType.AttackPower:
    //                    AttackPower.BaseValue *= ( 1 + scalingAmount );
    //                    break;
    //                case StatType.AbilityPower:
    //                    AbilityPower.BaseValue *= ( 1 + scalingAmount );
    //                    break;

    //            }
    //        }

    //    }
    //}


}
