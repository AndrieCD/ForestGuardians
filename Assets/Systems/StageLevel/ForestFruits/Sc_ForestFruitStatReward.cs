using System;
using UnityEngine;

[Serializable]
public class Sc_ForestFruitStatReward
{
    public bool Enabled = true;
    public StatType TargetStat;
    public float BonusAmount;
    public StatModType ModifierType = StatModType.Flat;
    public string DisplayStatName;
    public bool DisplayAsPercent;
    public Sprite Icon;

    public Sc_ForestFruitStatReward() { }

    public Sc_ForestFruitStatReward(StatType targetStat, float bonusAmount, string displayStatName, bool displayAsPercent)
    {
        TargetStat = targetStat;
        BonusAmount = bonusAmount;
        ModifierType = StatModType.Flat;
        DisplayStatName = displayStatName;
        DisplayAsPercent = displayAsPercent;
    }

    public string BuildTitle()
    {
        return $"+{FormatBonusAmount()} {GetDisplayStatName()}";
    }

    public string BuildDescription()
    {
        return $"Permanently gain +{FormatBonusAmount()} {GetDisplayStatName()} for this stage run.";
    }

    public string GetDisplayStatName()
    {
        if (!string.IsNullOrWhiteSpace(DisplayStatName))
            return DisplayStatName;

        return TargetStat switch
        {
            StatType.MaxHealth => "Max HP",
            StatType.HealthRegen => "HP Regen",
            StatType.MoveSpeed => "MS",
            StatType.AttackSpeed => "AS",
            StatType.AttackPower => "ATK",
            StatType.AbilityPower => "AP",
            StatType.Haste => "HST",
            StatType.CriticalChance => "CRT%",
            StatType.CriticalDamage => "Critical Damage",
            StatType.Lifesteal => "Lifesteal",
            StatType.Shielding => "Shielding",
            _ => TargetStat.ToString(),
        };
    }

    private string FormatBonusAmount()
    {
        if (!DisplayAsPercent)
            return FormatNumber(BonusAmount);

        float percentValue = TargetStat == StatType.Haste
            ? BonusAmount
            : BonusAmount * 100f;

        return $"{FormatNumber(percentValue)}%";
    }

    private static string FormatNumber(float value)
    {
        if (Mathf.Approximately(value, Mathf.Round(value)))
            return Mathf.RoundToInt(value).ToString();

        return value.ToString("0.##");
    }
}
