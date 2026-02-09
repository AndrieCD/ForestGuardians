using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Sc_Stat
{
    public float BaseValue;
    // A simple list of effects (effects are stat-specific buff/debuff instances)
    public List<Sc_StatEffect> Effects = new List<Sc_StatEffect>( );

    public Sc_Stat(float baseValue)
    {
        BaseValue = baseValue;
    }

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

    public float Value( )
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
}