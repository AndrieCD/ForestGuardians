using UnityEngine;

[System.Serializable]
public class Sc_AbilityScalingEntry
{
    [Tooltip("Label for this stat - e.g. 'Damage', 'Shield', 'DoT', 'Slow', 'Lifesteal'")]
    public string StatName;

    [Tooltip("The starting value of the ability before any stat scaling is applied.")]
    public float[] BaseValuePerLevel;

    [Tooltip("ATK multiplier added per level. Index 0 = level 1, index 5 = level 6.")]
    public float[] ATKScalingPerLevel;

    [Tooltip("AP multiplier added per level. Leave all zeros if this stat does not scale with AP.")]
    public float[] APScalingPerLevel;

    /// <summary>
    /// Returns the total value of this stat at the given ability level,
    /// factoring in the user's current ATK and AP stats.
    /// </summary>
    public float GetValue(int abilityLevel, float atk, float ap)
    {
        int index = Mathf.Max(0, abilityLevel - 1);

        float baseValue = GetArrayValue(BaseValuePerLevel, index);
        float atkBonus = atk * GetArrayValue(ATKScalingPerLevel, index);
        float apBonus = ap * GetArrayValue(APScalingPerLevel, index);

        return baseValue + atkBonus + apBonus;
    }

    private float GetArrayValue(float[] values, int index)
    {
        if (values == null || values.Length == 0)
            return 0f;

        return values[Mathf.Clamp(index, 0, values.Length - 1)];
    }
}
