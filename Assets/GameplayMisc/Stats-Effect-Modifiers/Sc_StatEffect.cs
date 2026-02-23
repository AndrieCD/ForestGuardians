using System;

public enum StatModType { Flat, Percent }
// Flat = a flat value added to the stat
// Percent = a percentage increase to the stat

public enum StatType
{
    MaxHealth,
    HealthRegen,
    MoveSpeed,
    AttackSpeed ,
    AttackPower,
    AbilityPower ,
    CooldownReduction,
    CriticalChance,
    CriticalDamage,
    Lifesteal,
    Shielding
}

public class Sc_StatEffect
{
    public readonly StatType TargetStat;
    public readonly float Value;
    public readonly StatModType Type;   // 0 = Flat, 1 = PercentAdd, 2 = PercentMult
    public readonly float Duration;

    public Sc_StatEffect(StatType stat, float value, StatModType type, float duration = float.PositiveInfinity)
    {
        TargetStat = stat;
        Value = value;
        Type = type;
        Duration = duration;
    }
}