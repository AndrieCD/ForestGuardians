using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns all stat properties and the modifier system for a character.
/// 
/// This is the single authority for applying and removing stat changes.
/// External systems (abilities, augments, environment) call AddModifier()
/// and RemoveModifier() — they never touch Sc_Stat.Effects directly.
/// </summary>
public class Mb_StatBlock : MonoBehaviour
{

    #region Stats               //----------------------------------------

    public Sc_Stat MaxHealth { get; private set; }
    public Sc_Stat HealthRegen { get; private set; }
    public Sc_Stat MoveSpeed { get; private set; }
    public Sc_Stat AttackSpeed { get; private set; }
    public Sc_Stat AttackPower { get; private set; }
    public Sc_Stat AbilityPower { get; private set; }
    public Sc_Stat Haste { get; private set; }
    public Sc_Stat CriticalChance { get; private set; }
    public Sc_Stat CriticalDamage { get; private set; }
    public Sc_Stat Lifesteal { get; private set; }
    public Sc_Stat Shielding { get; private set; }
    public Sc_Stat JumpPower { get; private set; }

    #endregion                  //----------------------------------------


    // Active modifier list — source of truth for everything currently applied
    private readonly List<Sc_Modifier> _activeModifiers = new();


    // Maps StatType enum to the actual Sc_Stat instance — built after BuildFromTemplate()
    // so modifiers can look up stats by type without a switch statement
    private Dictionary<StatType, Sc_Stat> _statLookup;


    // Event fired whenever any stat changes — UI or ability scripts can subscribe
    public event Action OnStatsChanged;

    #region Build From Template         //----------------------------------------

    public void BuildFromTemplate(SO_Guardian template)
    {
        MaxHealth = new Sc_Stat(template.MaxHealth, template.MaxHealthScaling);
        HealthRegen = new Sc_Stat(template.HealthRegen, template.HealthRegenScaling);
        MoveSpeed = new Sc_Stat(template.MoveSpeed, template.MoveSpeedScaling);
        AttackSpeed = new Sc_Stat(template.AttackSpeed, template.AttackSpeedScaling);
        AttackPower = new Sc_Stat(template.AttackPower, template.AttackPowerScaling);
        AbilityPower = new Sc_Stat(template.AbilityPower, template.AbilityPowerScaling);
        Haste = new Sc_Stat(template.Haste, template.HasteScaling);
        CriticalChance = new Sc_Stat(template.CriticalChance, template.CriticalChanceScaling);
        CriticalDamage = new Sc_Stat(template.CriticalDamage, template.CriticalDamageScaling);
        Lifesteal = new Sc_Stat(template.LifeSteal, template.LifeStealScaling);
        //Shielding = new Sc_Stat(template.Shielding, template.ShieldingScaling);
        JumpPower = new Sc_Stat(template.JumpPower, 0f); 
        BuildLookup();
    }


    public void BuildFromTemplate(SO_CuBots template)
    {
        MaxHealth = new Sc_Stat(template.MaxHealth, template.MaxHealthScaling);
        HealthRegen = new Sc_Stat(template.HealthRegen, template.HealthRegenScaling);
        MoveSpeed = new Sc_Stat(template.MoveSpeed, template.MoveSpeedScaling);
        AttackSpeed = new Sc_Stat(template.AttackSpeed, template.AttackSpeedScaling);
        AttackPower = new Sc_Stat(template.AttackPower, template.AttackPowerScaling);
        AbilityPower = new Sc_Stat(template.AbilityPower, template.AbilityPowerScaling);
        Haste = new Sc_Stat(template.Haste, template.HasteScaling);
        CriticalChance = new Sc_Stat(template.CriticalChance, template.CriticalChanceScaling);
        CriticalDamage = new Sc_Stat(template.CriticalDamage, template.CriticalDamageScaling);
        Lifesteal = new Sc_Stat(template.LifeSteal, template.LifeStealScaling);
        //Shielding = new Sc_Stat(template.Shielding, template.ShieldingScaling);
        // JumpPower omitted — CuBots don't jump
        BuildLookup();
    }


    // Builds the StatType → Sc_Stat dictionary after stats are created
    // so modifiers can look up any stat by enum value
    private void BuildLookup()
    {
        _statLookup = new Dictionary<StatType, Sc_Stat>
        {
            { StatType.MaxHealth,         MaxHealth         },
            { StatType.HealthRegen,       HealthRegen       },
            { StatType.MoveSpeed,         MoveSpeed         },
            { StatType.AttackSpeed,       AttackSpeed       },
            { StatType.AttackPower,       AttackPower       },
            { StatType.AbilityPower,      AbilityPower      },
            { StatType.Haste, Haste },
            { StatType.CriticalChance,    CriticalChance    },
            { StatType.CriticalDamage,    CriticalDamage    },
            { StatType.Lifesteal,         Lifesteal         },
            { StatType.Shielding,         Shielding         },
        };
    }

    #endregion                  //----------------------------------------


    #region Scaling API     //----------------------------------------

    /// <summary>
    /// Updates all stat BaseValues for the given character level.
    /// Fires OnStatsChanged so anything listening (UI, abilities) refreshes automatically.
    ///
    /// Returns the MaxHealth delta so the caller (Mb_CharacterBase.LevelUp) can
    /// heal the character by that amount — keeping current HP proportional on level-up.
    /// </summary>
    public float SetLevel(int level)
    {
        float oldMaxHP = MaxHealth.GetValue();

        // Each stat recalculates its BaseValue from its own original value + scaling.
        // Because SetLevel always reads from _originalBaseValue inside Sc_Stat,
        // calling this at level 5 gives the same result as calling it 4 times in a row.
        MaxHealth.SetLevel(level);
        HealthRegen.SetLevel(level);
        MoveSpeed.SetLevel(level);
        AttackSpeed.SetLevel(level);
        AttackPower.SetLevel(level);
        AbilityPower.SetLevel(level);
        Haste.SetLevel(level);
        CriticalChance.SetLevel(level);
        CriticalDamage.SetLevel(level);
        Lifesteal.SetLevel(level);
        // JumpPower and Shielding intentionally excluded — they don't scale with level

        OnStatsChanged?.Invoke();

        // Return how much MaxHealth grew so the caller can top up HP by the same amount
        return MaxHealth.GetValue() - oldMaxHP;
    }

    #endregion              //----------------------------------------


    #region Modifier API        //----------------------------------------

    /// <summary>
    /// Applies a modifier to this character's stats.
    /// If the modifier is timed, StatBlock handles its removal automatically.
    /// </summary>
    public void AddModifier(Sc_Modifier modifier)
    {
        _activeModifiers.Add(modifier);     // Add the modifier to the active list
        ApplyEffects(modifier);             // Apply the effects within the modifier to the target stats

        // If timed, start the removal coroutine here — StatBlock is always active
        if (modifier.Duration != float.PositiveInfinity)
            StartCoroutine(RemoveAfterDuration(modifier));

        OnStatsChanged?.Invoke();
    }


    /// <summary>
    /// Removes a specific modifier and reverses its effects.
    /// </summary>
    public void RemoveModifier(Sc_Modifier modifier)
    {
        if (!_activeModifiers.Remove(modifier)) return; // If it wasn't in the list, do nothing
        RemoveEffects(modifier);
        OnStatsChanged?.Invoke();
    }


    /// <summary>
    /// Removes all modifiers from a specific source.
    /// Use this to strip augments between runs, or clear environmental debuffs on zone exit.
    /// </summary>
    public void RemoveAllFromSource(ModifierSource sourceArg)
    {
        // Build removal list first — never modify a list while iterating it
        var toRemove = new List<Sc_Modifier>();
        foreach (var modifier in _activeModifiers)
        {
            // Add any modifier from the specified source to the removal list
            if (modifier.Source == sourceArg)
                toRemove.Add(modifier);
        }

        // Now remove each modifier in the removal list and its effects
        foreach (var modifier in toRemove)
        {
            _activeModifiers.Remove(modifier);
            RemoveEffects(modifier);
        }

        // Invoke change event if we removed anything — avoids unnecessary UI updates
        if (toRemove.Count > 0)
            OnStatsChanged?.Invoke();
    }


    /// <summary>
    /// Removes every modifier regardless of source and clears all stat effects.
    /// Use this on CuBot pool reset or full character reset.
    /// </summary>
    public void RemoveAllModifiers()
    {
        _activeModifiers.Clear();

        // Clear effects directly from each stat — safe since we cleared the modifier list too
        MaxHealth?.Effects.Clear();
        HealthRegen?.Effects.Clear();
        MoveSpeed?.Effects.Clear();
        AttackSpeed?.Effects.Clear();
        AttackPower?.Effects.Clear();
        AbilityPower?.Effects.Clear();
        Haste?.Effects.Clear();
        CriticalChance?.Effects.Clear();
        CriticalDamage?.Effects.Clear();
        Lifesteal?.Effects.Clear();
        Shielding?.Effects.Clear();
        JumpPower?.Effects.Clear();

        OnStatsChanged?.Invoke();
    }

    #endregion              //----------------------------------------


    #region Internal Effect Application         //----------------------------------------

    /// <summary>
    /// Applies all effects from the specified modifier to their corresponding target stat.
    /// </summary>
    /// <param name="modifier">The modifier containing the collection of effects to apply. Cannot be null.</param>
    private void ApplyEffects(Sc_Modifier modifier)
    {
        foreach (var effect in modifier.Effects)
        {
            if (_statLookup.TryGetValue(effect.TargetStat, out var stat))
                stat.Effects.Add(effect);
        }
    }


    private void RemoveEffects(Sc_Modifier modifier)
    {
        foreach (var effect in modifier.Effects)
        {
            if (_statLookup.TryGetValue(effect.TargetStat, out var stat))
                stat.Effects.Remove(effect);
        }
    }


    private IEnumerator RemoveAfterDuration(Sc_Modifier modifier)
    {
        yield return new WaitForSeconds(modifier.Duration);
        RemoveModifier(modifier);
    }

    #endregion          //----------------------------------------

}