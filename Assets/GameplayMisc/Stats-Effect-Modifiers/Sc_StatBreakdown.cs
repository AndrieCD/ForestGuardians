using System;

/// <summary>
/// Read-only computed information for one runtime stat.
/// Values use the same semantics as Sc_Stat.GetValue().
/// </summary>
public readonly struct Sc_StatBreakdown
{
    public readonly float BaseValue;
    public readonly float FlatBonus;
    public readonly float PercentBonus;
    public readonly float FinalValue;
    public readonly int ActiveEffectCount;

    public Sc_StatBreakdown(
        float baseValue,
        float flatBonus,
        float percentBonus,
        float finalValue,
        int activeEffectCount)
    {
        BaseValue = baseValue;
        FlatBonus = flatBonus;
        PercentBonus = percentBonus;
        FinalValue = finalValue;
        ActiveEffectCount = activeEffectCount;
    }
}
