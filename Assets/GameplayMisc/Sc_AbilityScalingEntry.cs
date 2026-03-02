using UnityEngine;


[System.Serializable]
public class Sc_AbilityScalingEntry
{
    [Tooltip("Label for this stat — e.g. 'Damage', 'Shield', 'DoT', 'Slow', 'Lifesteal'")]
    public string StatName;

    [Tooltip("The starting value of the ability (before any stat scaling is applied)")]
    public float[] BaseValuePerLevel;

    [Tooltip("ATK multiplier added per level. Index 0 = level 1, index 5 = level 6. " +
             "e.g. Sky Rend Damage ATK scaling = {0.15, 0.35, 0.40, 0.50, 0.65, 0.80}")]
    public float[] ATKScalingPerLevel;

    [Tooltip("AP multiplier added per level. Leave all zeros if this stat doesn't scale with AP.")]
    public float[] APScalingPerLevel;

    // ----------------------------------------------------
    // Returns the total value of this stat at the given ability level,
    // factoring in the user's current ATK and AP stats.
    //
    // abilityLevel is 1-based (level 1 = index 0 in the arrays).
    // ----------------------------------------------------
    public float GetValue(int abilityLevel, float atk, float ap)
    {
        // Clamp to valid range so we never go out of bounds on the arrays
        int index = Mathf.Clamp(abilityLevel - 1, 0, Mathf.Min(
            ATKScalingPerLevel.Length,
            APScalingPerLevel.Length
        ) - 1);

        float atkBonus = atk * ATKScalingPerLevel[index];
        float apBonus = ap * APScalingPerLevel[index];


        int baseValueIndex = Mathf.Clamp(abilityLevel - 1, 0, BaseValuePerLevel.Length - 1);
        float baseValue = BaseValuePerLevel[baseValueIndex];

        return baseValue + atkBonus + apBonus;
    }
}