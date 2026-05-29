using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds a single stat's base value, level scaling, and active effects list.
/// Does NOT own a "current" value — resource tracking (e.g. current HP) belongs
/// on Mb_HealthComponent, not here.
///
/// External systems never write to Effects directly — always go through Mb_StatBlock,
/// which calls NotifyChanged() after applying or removing effects.
/// </summary>
[System.Serializable]
public class Sc_Stat
{
    // ─── Fields ────────────────────────────────────────────────────────────

    // Set once from the ScriptableObject template. Never changes after construction.
    // SetLevel() always scales off this so growth is always linear.
    private float _originalBaseValue;

    // Live base value. Rewritten by SetLevel() on level-up.
    public float BaseValue;

    // e.g. 0.40 = +40% of original base per level gained
    public float _scalingPerLevel;

    // Effects currently applied to this stat. Written exclusively by Mb_StatBlock.
    // Do not write to this list from anywhere else.
    public List<Sc_StatEffect> Effects = new List<Sc_StatEffect>();


    // ─── Events ────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired by Mb_StatBlock whenever this stat's effects or base value change.
    /// UI elements should subscribe here rather than polling GetValue() in Update().
    ///
    /// Passes the new computed value so subscribers don't need to call GetValue() themselves.
    ///   e.g. healthBar.OnMaxHealthChanged(float newMax)
    /// </summary>
    public event Action<float> OnStatChanged;


    // ─── Constructor ───────────────────────────────────────────────────────

    public Sc_Stat(float baseValue, float scalingPerLevel)
    {
        _originalBaseValue = baseValue;
        BaseValue = baseValue;
        _scalingPerLevel = scalingPerLevel;
    }


    // ─── Level Scaling ─────────────────────────────────────────────────────

    /// <summary>
    /// Recomputes BaseValue for the given character level.
    /// Formula: base * (1 + scalingPerLevel * levelsGained)
    /// Safe to call multiple times — always reads from _originalBaseValue, never compounds.
    /// </summary>
    public void SetLevel(int level)
    {
        int levelsGained = level - 1;
        BaseValue = _originalBaseValue * (1f + (_scalingPerLevel * levelsGained));
        NotifyChanged();
    }


    // ─── Value Computation ─────────────────────────────────────────────────

    /// <summary>
    /// Returns the final computed value: base + flat effects, scaled by percent effects.
    /// This is the value UI and gameplay systems should read.
    /// </summary>
    public float GetValue()
    {
        float finalValue = BaseValue;
        float percentBonus = 0f;

        foreach (Sc_StatEffect effect in Effects)
        {
            if (effect.Type == StatModType.Percent)
                percentBonus += effect.Value;
            else
                finalValue += effect.Value;
        }

        return finalValue * (1f + percentBonus);
    }

    /// <summary>
    /// Returns only the bonus from active effects, not the base value.
    /// Useful for UI tooltips that show "base + bonus" breakdowns,
    /// or augment screens showing how much a modifier contributes.
    /// </summary>
    public float BonusValue()
    {
        float flatBonus = 0f;
        float percentBonus = 0f;

        foreach (Sc_StatEffect effect in Effects)
        {
            if (effect.Type == StatModType.Percent)
                percentBonus += effect.Value;
            else
                flatBonus += effect.Value;
        }

        return flatBonus + (BaseValue * percentBonus);
    }


    // ─── Internal Notification ─────────────────────────────────────────────

    /// <summary>
    /// Called by Mb_StatBlock after it writes to Effects, and by SetLevel().
    /// Fires OnStatChanged with the freshly computed value so subscribers
    /// receive the new number without needing to call GetValue() themselves.
    ///
    /// Do not call this from outside Mb_StatBlock or Sc_Stat itself.
    /// </summary>
    public void NotifyChanged()
    {
        OnStatChanged?.Invoke(GetValue());
    }
}