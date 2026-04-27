using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Sc_Stat
{
    // The stat's value as defined in the ScriptableObject — never changes after construction.
    // All level scaling is computed from this so growth is always linear off the original number.
    private float _originalBaseValue;

    // The "live" base used by GetValue(). SetLevel() rewrites this each level-up.
    public float BaseValue;

    public float _scalingPerLevel; // e.g. 0.40 means +40% of original base per level gained

    // A simple list of effects (effects are stat-specific buff/debuff instances)
    public List<Sc_StatEffect> Effects = new List<Sc_StatEffect>( );

    private float currentValue;

    public float CurrentValue => currentValue;

    // Constructor to initialize the base value of the stat
    public Sc_Stat(float baseValue, float scalingPerLevel)
    {
        _originalBaseValue = baseValue;
        BaseValue = baseValue;
        _scalingPerLevel = scalingPerLevel;
        Recalculate(); // initialize currentValue
    }

    /// <summary>
    /// Recomputes BaseValue for the given character level using the Python formula:
    ///   stat = originalBase * (1 + scalingPerLevel * levelsGained)
    /// "levelsGained" is (level - 1) because at level 1 there is no bonus yet.
    /// This always reads from _originalBaseValue, so calling it multiple times
    /// never compounds — it's safe to call on every level-up.
    /// </summary>
    public void SetLevel(int level)
    {
        int levelsGained = level - 1; // level 1 = 0 bonus, level 2 = 1 bonus, etc.
        BaseValue = Mathf.FloorToInt(_originalBaseValue * (1f + _scalingPerLevel * levelsGained));
    }

    // Add a new effect to the stat and start a coroutine to remove it after its duration if it's not infinite
    public void AddEffect(Sc_StatEffect effect, MonoBehaviour runner)
    {
        Effects.Add(effect);

        if (effect.Duration == float.PositiveInfinity) return;
        runner.StartCoroutine(RemoveAfterDuration(effect));
    }

    IEnumerator RemoveAfterDuration(Sc_StatEffect effect)
    {
        yield return new WaitForSeconds(effect.Duration);
        RemoveEffect(effect);
    }

    public void RemoveEffect(Sc_StatEffect effect)
    {
        Effects.Remove(effect);
    }

    // Returns the final value of this stat after applying all active modifiers.
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

    // Returns only the bonus granted by modifiers, not the base value itself.
    public float BonusValue()
    {
        float bonusValue = 0f;
        float percentBonus = 0f;

        foreach (Sc_StatEffect effect in Effects)
        {
            if (effect.Type == StatModType.Percent)
                percentBonus += effect.Value;
            else
                bonusValue += effect.Value;
        }

        return bonusValue + (BaseValue * percentBonus);
    }

    public void Recalculate(bool resetCurrent = false)
    {
        float newMax = GetValue();

        if (resetCurrent)
        {
            currentValue = newMax;
        }
        else
        {
            // Preserve ratio when max changes
            float ratio = currentValue / Mathf.Max(1f, newMax);
            currentValue = newMax * ratio;
        }
    }

    public float Reduce(float amount)
    {
        float absorbed = Mathf.Min(currentValue, amount);
        currentValue -= absorbed;
        return absorbed; // how much was actually consumed
    }

    public void Restore(float amount)
    {
        currentValue = Mathf.Min(currentValue + amount, GetValue());
    }
}