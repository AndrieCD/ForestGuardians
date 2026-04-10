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

[Serializable]
public class Sc_StatEffect
{
    // Make fields non-readonly so Unity can serialize and display them in the Inspector
    public StatType TargetStat;
    public float Value;
    public StatModType Type;   // Flat, Percent
    public float Duration = float.PositiveInfinity;

    // Parameterless constructor is helpful for Unity serialization / default-initialized entries in the inspector
    public Sc_StatEffect() { }

    public Sc_StatEffect(StatType stat, float value, StatModType type, float duration = float.PositiveInfinity)
    {
        TargetStat = stat;
        Value = value;
        Type = type;
        Duration = duration;
    }
}