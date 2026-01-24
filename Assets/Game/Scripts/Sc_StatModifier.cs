using System;

public enum StatModType { Flat, Percent }
// Flat = a flat value added to the stat
// Percent = a percentage increase to the stat
public class Sc_StatModifier
{
    public readonly float Value;
    public readonly StatModType Type;   // 0 = Flat, 1 = PercentAdd, 2 = PercentMult
    public readonly float Duration;

    public Sc_StatModifier(float value, StatModType type, float duration = float.PositiveInfinity)
    {
        Value = value;
        Type = type;
        Duration = duration;
    }
}