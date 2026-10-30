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
    public event Action<Sc_Modifier> OnModifierAdded;
    public event Action<Sc_Modifier> OnModifierRemoved;


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
    //public Sc_Stat Shielding { get; private set; }
    public Sc_Stat JumpPower { get; private set; }

    #endregion                  //----------------------------------------


    // Active modifier list — source of truth for everything currently applied
    private readonly List<Sc_Modifier> _activeModifiers = new();
    private readonly Dictionary<long, Sc_Modifier> _activeModifiersById = new();
    private readonly Dictionary<long, Coroutine> _modifierCoroutines = new();

    /// <summary>
    /// Read-only view of all modifiers currently applied to this character.
    /// </summary>
    public IReadOnlyList<Sc_Modifier> ActiveModifiers => _activeModifiers;


    // Maps StatType enum to the actual Sc_Stat instance — built after BuildFromTemplate()
    // so modifiers can look up stats by type without a switch statement
    private Dictionary<StatType, Sc_Stat> _statLookup;


    // Event fired whenever any stat changes — UI or ability scripts can subscribe
    public event Action OnStatsChanged;

    // Nested batches allow one logical operation to update several stats while
    // publishing only one block-level notification.
    private int _statChangeBatchDepth;
    private bool _statChangePending;


    #region Build From Template         //----------------------------------------

    /// <summary>
    /// Builds guardian stats from ScriptableObject base values and scaling values.
    /// Call before health initialization or ability setup.
    /// </summary>
    public void BuildFromTemplate(SO_Guardian template)
    {
        MaxHealth = new Sc_Stat(template.MaxHealth, template.MaxHealthScaling);
        HealthRegen = new Sc_Stat(template.HealthRegen, template.HealthRegenScaling);

        MoveSpeed = new Sc_Stat(
            template.MoveSpeed,
            template.MoveSpeedScaling,
            useStrongestNegativePercentOnly: true,
            minimumValue: 1f
        );
        AttackSpeed = new Sc_Stat(
            template.AttackSpeed,
            template.AttackSpeedScaling,
            useStrongestNegativePercentOnly: true
        );
        AttackPower = new Sc_Stat(template.AttackPower, template.AttackPowerScaling);
        AbilityPower = new Sc_Stat(template.AbilityPower, template.AbilityPowerScaling);
        Haste = new Sc_Stat(template.Haste, template.HasteScaling);
        CriticalChance = new Sc_Stat(template.CriticalChance, template.CriticalChanceScaling);
        CriticalDamage = new Sc_Stat(template.CriticalDamage, template.CriticalDamageScaling);
        Lifesteal = new Sc_Stat(template.LifeSteal, template.LifeStealScaling);
        //Shielding = new Sc_Stat(template.Shielding, template.ShieldingScaling);
        JumpPower = new Sc_Stat(template.JumpPower, 0f);

        SubscribeToStatChanges();
        BuildLookup();
    }


    /// <summary>
    /// Builds CuBot stats from ScriptableObject base values and scaling values.
    /// Use this during pool reset so reused enemies start from clean base stats.
    /// </summary>
    public void BuildFromTemplate(SO_CuBots template)
    {
        BuildFromTemplate(template, 1f);
    }

    /// <summary>
    /// Builds CuBot stats from ScriptableObject base values with a stage combat multiplier.
    /// Use this during pool reset so reused enemies start from clean base stats.
    /// </summary>
    public void BuildFromTemplate(SO_CuBots template, float baseStatMultiplier)
    {
        baseStatMultiplier = Mathf.Max(0f, baseStatMultiplier);

        MaxHealth = new Sc_Stat(template.MaxHealth * baseStatMultiplier, template.MaxHealthScaling);
        HealthRegen = new Sc_Stat(template.HealthRegen, template.HealthRegenScaling);
        MoveSpeed = new Sc_Stat(
            template.MoveSpeed,
            template.MoveSpeedScaling,
            useStrongestNegativePercentOnly: true,
            minimumValue: 1f
        );
        AttackSpeed = new Sc_Stat(
            template.AttackSpeed,
            template.AttackSpeedScaling,
            useStrongestNegativePercentOnly: true
        );
        AttackPower = new Sc_Stat(template.AttackPower * baseStatMultiplier, template.AttackPowerScaling);
        AbilityPower = new Sc_Stat(template.AbilityPower * baseStatMultiplier, template.AbilityPowerScaling);
        Haste = new Sc_Stat(template.Haste, template.HasteScaling);
        CriticalChance = new Sc_Stat(template.CriticalChance, template.CriticalChanceScaling);
        CriticalDamage = new Sc_Stat(template.CriticalDamage, template.CriticalDamageScaling);
        Lifesteal = new Sc_Stat(template.LifeSteal, template.LifeStealScaling);
        //Shielding = new Sc_Stat(template.Shielding, template.ShieldingScaling);
        // JumpPower omitted — CuBots don't jump

        SubscribeToStatChanges();
        BuildLookup();
    }

    /// <summary>
    /// Builds Panoharra stats from ScriptableObject base values.
    /// Panoharra only uses health-related stats.
    /// </summary>
    public void BuildFromTemplate(SO_Panoharra template)
    {
        MaxHealth = new Sc_Stat(template.MaxHealth, 0);
        HealthRegen = new Sc_Stat(template.HealthRegen, 0);

        BuildLookup();
    }

    // Builds the StatType → Sc_Stat dictionary after stats are created
    // so modifiers can look up any stat by enum value
    private void BuildLookup()
    {
        _statLookup = new Dictionary<StatType, Sc_Stat>();
        RegisterStat(StatType.MaxHealth, MaxHealth);
        RegisterStat(StatType.HealthRegen, HealthRegen);
        RegisterStat(StatType.MoveSpeed, MoveSpeed);
        RegisterStat(StatType.AttackSpeed, AttackSpeed);
        RegisterStat(StatType.AttackPower, AttackPower);
        RegisterStat(StatType.AbilityPower, AbilityPower);
        RegisterStat(StatType.Haste, Haste);
        RegisterStat(StatType.CriticalChance, CriticalChance);
        RegisterStat(StatType.CriticalDamage, CriticalDamage);
        RegisterStat(StatType.Lifesteal, Lifesteal);
    }

    private void RegisterStat(StatType statType, Sc_Stat stat)
    {
        if (stat != null)
            _statLookup[statType] = stat;
    }

    private void SubscribeToStatChanges()
    {
        SubscribeToStat(MaxHealth);
        SubscribeToStat(HealthRegen);
        SubscribeToStat(MoveSpeed);
        SubscribeToStat(AttackSpeed);
        SubscribeToStat(AttackPower);
        SubscribeToStat(AbilityPower);
        SubscribeToStat(Haste);
        SubscribeToStat(CriticalChance);
        SubscribeToStat(CriticalDamage);
        SubscribeToStat(Lifesteal);
    }

    private void SubscribeToStat(Sc_Stat stat)
    {
        if (stat != null)
            stat.OnStatChanged += HandleStatChanged;
    }

    private void HandleStatChanged(float _)
    {
        if (_statChangeBatchDepth > 0)
        {
            _statChangePending = true;
            return;
        }

        OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// Gets a runtime stat by its enum value without exposing the lookup dictionary.
    /// </summary>
    public bool TryGetStat(StatType statType, out Sc_Stat stat)
    {
        if (_statLookup != null && _statLookup.TryGetValue(statType, out stat) && stat != null)
            return true;

        stat = null;
        return false;
    }

    /// <summary>
    /// Returns a read-only computed breakdown for the requested stat.
    /// </summary>
    public bool TryGetStatBreakdown(StatType statType, out Sc_StatBreakdown breakdown)
    {
        if (TryGetStat(statType, out Sc_Stat stat))
        {
            breakdown = stat.GetBreakdown();
            return true;
        }

        breakdown = default;
        return false;
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

        BeginStatChangeBatch();
        try
        {
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
        }
        finally
        {
            EndStatChangeBatch();
        }

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
        ApplyModifier(modifier);
    }

    /// <summary>
    /// Applies a modifier and returns the identity of this exact runtime instance.
    /// Use the returned handle when a caller may hold multiple otherwise-identical
    /// modifiers and needs to remove only one of them.
    /// </summary>
    public Sc_ModifierHandle ApplyModifier(Sc_Modifier modifier)
    {
        if (modifier == null)
        {
            Debug.LogWarning("[Mb_StatBlock] AddModifier received null modifier.");
            return default;
        }

        if (_statLookup == null)
        {
            Debug.LogWarning($"[Mb_StatBlock] Cannot apply {modifier}: stats have not been built.");
            return default;
        }

        if (_activeModifiersById.ContainsKey(modifier.RuntimeId))
        {
            Debug.LogWarning($"[Mb_StatBlock] Ignored duplicate application of {modifier} on {name}.");
            return default;
        }

        if (!ValidateModifierDuration(modifier))
            return default;

        BeginStatChangeBatch();
        try
        {
            int appliedEffectCount = ApplyEffects(modifier);
            if (appliedEffectCount == 0)
            {
                Debug.LogWarning($"[Mb_StatBlock] Ignored {modifier} because it contains no supported effects.");
                return default;
            }

            _activeModifiers.Add(modifier);
            _activeModifiersById.Add(modifier.RuntimeId, modifier);

            if (modifier.Duration != float.PositiveInfinity)
            {
                // Modifiers from StatusEffect source are expected to be infinite-duration.
                // The StatusEffectController owns their timer.
                if (modifier.Source == ModifierSource.StatusEffect)
                    Debug.LogWarning($"[Mb_StatBlock] Modifier '{modifier.ModifierName}' from StatusEffect " +
                                     $"source has a finite duration — Mb_StatusEffectController owns this lifetime. " +
                                     $"Set duration to float.PositiveInfinity.");
                else
                {
                    Coroutine routine = StartCoroutine(RemoveAfterDuration(new Sc_ModifierHandle(modifier.RuntimeId)));
                    _modifierCoroutines[modifier.RuntimeId] = routine;
                }
            }

            OnModifierAdded?.Invoke(modifier);
            return new Sc_ModifierHandle(modifier.RuntimeId);
        }
        finally
        {
            EndStatChangeBatch();
        }
    }


    /// <summary>
    /// Removes a specific modifier and reverses its effects.
    /// </summary>
    public void RemoveModifier(Sc_Modifier modifier)
    {
        if (modifier == null) return;
        RemoveModifier(new Sc_ModifierHandle(modifier.RuntimeId));
    }

    /// <summary>
    /// Removes the exact modifier associated with a runtime handle.
    /// Returns false when the handle is invalid, stale, or already removed.
    /// </summary>
    public bool RemoveModifier(Sc_ModifierHandle handle)
    {
        if (!handle.IsValid || !_activeModifiersById.TryGetValue(handle.RuntimeId, out Sc_Modifier modifier))
            return false;

        BeginStatChangeBatch();
        try
        {
            RemoveModifierInternal(handle, modifier);
            return true;
        }
        finally
        {
            EndStatChangeBatch();
        }
    }

    /// <summary>
    /// Resolves an active modifier handle for diagnostics or ownership tracking.
    /// </summary>
    public bool TryGetModifier(Sc_ModifierHandle handle, out Sc_Modifier modifier)
    {
        if (handle.IsValid && _activeModifiersById.TryGetValue(handle.RuntimeId, out modifier))
            return true;

        modifier = null;
        return false;
    }

    private void RemoveModifierInternal(Sc_ModifierHandle handle, Sc_Modifier modifier)
    {
        _activeModifiersById.Remove(handle.RuntimeId);
        _activeModifiers.Remove(modifier);
        StopModifierCoroutine(handle.RuntimeId);
        RemoveEffects(modifier);
        OnModifierRemoved?.Invoke(modifier);
    }

    private void BeginStatChangeBatch()
    {
        _statChangeBatchDepth++;
    }

    private void EndStatChangeBatch()
    {
        _statChangeBatchDepth--;
        if (_statChangeBatchDepth > 0)
            return;

        _statChangeBatchDepth = 0;
        if (!_statChangePending)
            return;

        _statChangePending = false;
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

        BeginStatChangeBatch();
        try
        {
            // Now remove each modifier in the removal list and its effects
            foreach (var modifier in toRemove)
            {
                if (_activeModifiersById.TryGetValue(modifier.RuntimeId, out Sc_Modifier activeModifier))
                    RemoveModifierInternal(new Sc_ModifierHandle(modifier.RuntimeId), activeModifier);
            }
        }
        finally
        {
            EndStatChangeBatch();
        }
    }


    /// <summary>
    /// Removes every modifier regardless of source and clears all stat effects.
    /// Use this on CuBot pool reset or full character reset.
    /// </summary>
    public void RemoveAllModifiers()
    {
        var toRemove = new List<Sc_Modifier>(_activeModifiers);
        BeginStatChangeBatch();
        try
        {
            foreach (Sc_Modifier modifier in toRemove)
            {
                if (_activeModifiersById.TryGetValue(modifier.RuntimeId, out Sc_Modifier activeModifier))
                    RemoveModifierInternal(new Sc_ModifierHandle(modifier.RuntimeId), activeModifier);
            }
        }
        finally
        {
            EndStatChangeBatch();
        }
    }

    #endregion              //----------------------------------------


    #region Internal Effect Application         //----------------------------------------

    /// <summary>
    /// Applies all effects from the specified modifier to their corresponding target stat.
    /// </summary>
    /// <param name="modifier">The modifier containing the collection of effects to apply. Cannot be null.</param>
    private int ApplyEffects(Sc_Modifier modifier)
    {
        int appliedEffectCount = 0;
        foreach (var effect in modifier.Effects)
        {
            if (effect == null)
            {
                Debug.LogWarning($"[Mb_StatBlock] {modifier} contains a null effect. Skipped.");
                continue;
            }

            if (TryGetStat(effect.TargetStat, out var stat))
            {
                stat.AddEffect(effect);
                stat.NotifyChanged(); // stat-level event fires here
                appliedEffectCount++;
            }
            else
            {
                Debug.LogWarning($"[Mb_StatBlock] Modifier '{modifier.ModifierName}' targets unsupported stat '{effect.TargetStat}'.");
            }

        }

        return appliedEffectCount;
    }


    /// <summary>
    /// Removes all effects from the specified modifier from their corresponding target stats.
    /// </summary>
    private void RemoveEffects(Sc_Modifier modifier)
    {
        foreach (var effect in modifier.Effects)
        {
            if (effect != null && TryGetStat(effect.TargetStat, out var stat))
            {
                stat.RemoveEffect(effect);
                stat.NotifyChanged(); // stat-level event fires here
            }
        }
    }

    private bool ValidateModifierDuration(Sc_Modifier modifier)
    {
        if (float.IsNaN(modifier.Duration) || modifier.Duration < 0f)
        {
            Debug.LogWarning($"[Mb_StatBlock] Ignored {modifier} because its duration is invalid: {modifier.Duration}.");
            return false;
        }

        if (modifier.Source == ModifierSource.StatusEffect && modifier.Duration != float.PositiveInfinity)
        {
            Debug.LogWarning($"[Mb_StatBlock] {modifier} has a finite StatusEffect duration. " +
                             "Mb_StatusEffectController must own the status lifetime.");
        }

        return true;
    }

    private void StopModifierCoroutine(long runtimeId)
    {
        if (_modifierCoroutines.TryGetValue(runtimeId, out Coroutine routine))
        {
            if (routine != null)
                StopCoroutine(routine);

            _modifierCoroutines.Remove(runtimeId);
        }
    }


    private IEnumerator RemoveAfterDuration(Sc_ModifierHandle handle)
    {
        if (!TryGetModifier(handle, out Sc_Modifier modifier))
            yield break;

        yield return new WaitForSeconds(modifier.Duration);
        RemoveModifier(handle);
    }

    #endregion          //----------------------------------------


    /// <summary>
    /// Manually notifies listeners that a stat changed when a caller updates stat-adjacent state.
    /// </summary>
    public void NotifyStatsChanged()
    {
        if (_statChangeBatchDepth > 0)
        {
            _statChangePending = true;
            return;
        }

        OnStatsChanged?.Invoke();
    }
}
