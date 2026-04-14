using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Sc_Stat
{
    public float BaseValue;

    public float _scalingPerLevel;

    // A simple list of effects (effects are stat-specific buff/debuff instances)
    public List<Sc_StatEffect> Effects = new List<Sc_StatEffect>( );

    private float currentValue;

    public float CurrentValue => currentValue;

    // Constructor to initialize the base value of the stat
    public Sc_Stat(float baseValue, float scalingPerLevel)
    {
        BaseValue = baseValue;
        _scalingPerLevel = scalingPerLevel;
        Recalculate(); // initialize currentValue
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

    // Calculate the final value of the stat by applying all effects in order
    public float GetValue( )
    {
        float finalValue = BaseValue;
        float percentBonus = 0;

        // Apply stat modifiers in order
        foreach (Sc_StatEffect effect in Effects)
        {
            if (effect.Type == StatModType.Percent)
            {
                // We add up all percentages first (e.g., 0.1 + 0.1 = 20%)
                percentBonus += effect.Value;
            } else
            {
                // Add flat amounts (e.g., +10 HP)
                finalValue += effect.Value;
            }
        }

        // Apply the percentage at the end
        return finalValue * ( 1 + percentBonus );
    }

    // Get only the bonus stat value from effects, excluding the base value
    public float BonusValue( )
    {
        float bonusValue = 0;
        float percentBonus = 0;

        foreach (Sc_StatEffect effect in Effects)
        {
            if (effect.Type == StatModType.Percent)
            {
                percentBonus += effect.Value;
            } else
            {
                bonusValue += effect.Value;
            }
        }

        return bonusValue + ( BaseValue * percentBonus );
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