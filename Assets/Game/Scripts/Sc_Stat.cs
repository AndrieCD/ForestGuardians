using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Sc_Stat
{
    public float BaseValue;
    // A simple list of modifiers
    public List<Sc_StatModifier> Modifiers = new List<Sc_StatModifier>( );

    public Sc_Stat(float baseValue)
    {
        BaseValue = baseValue;
    }

    public void AddModifier(Sc_StatModifier mod, MonoBehaviour runner)
    {
        Modifiers.Add(mod);

        if (mod.Duration == float.PositiveInfinity) return;
        runner.StartCoroutine(RemoveAfterDuration(mod));
    }

    IEnumerator RemoveAfterDuration(Sc_StatModifier mod)
    {
        yield return new WaitForSeconds(mod.Duration);
        RemoveModifier(mod);
    }

    public void RemoveModifier(Sc_StatModifier mod)
    {
        Modifiers.Remove(mod);
    }

    // This is the core logic. It's just a loop!
    public float Value( )
    {
        float finalValue = BaseValue;
        float percentBonus = 0;

        // Apply stat modifiers in order
        foreach (Sc_StatModifier mod in Modifiers)
        {
            if (mod.Type == StatModType.Percent)
            {
                // We add up all percentages first (e.g., 0.1 + 0.1 = 20%)
                percentBonus += mod.Value;
            } else
            {
                // Add flat amounts (e.g., +10 HP)
                finalValue += mod.Value;
            }
        }

        // Apply the percentage at the end
        return finalValue * ( 1 + percentBonus );
    }
}