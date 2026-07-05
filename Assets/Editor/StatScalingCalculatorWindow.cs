// StatScalingCalculatorWindow.cs
// Editor-only calculator for checking final character stats, cooldowns, and
// SO_Ability scaling outputs without entering Play Mode.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class StatScalingCalculatorWindow : EditorWindow
{
    private StatCalculatorSource _source = StatCalculatorSource.Guardian;
    private SO_Guardian _guardian;
    private SO_CuBots _cuBot;
    private SO_Ability _ability;

    private int _characterLevel = 1;
    private int _abilityLevel = 1;
    private CooldownPreviewMode _cooldownMode = CooldownPreviewMode.AbilityUsesHaste;

    private Vector2 _scroll;

    private readonly List<StatModifierPreview> _modifiers = new List<StatModifierPreview>
    {
        new StatModifierPreview { TargetStat = StatType.AttackPower },
        new StatModifierPreview { TargetStat = StatType.AbilityPower },
        new StatModifierPreview { TargetStat = StatType.Haste },
        new StatModifierPreview { TargetStat = StatType.AttackSpeed },
        new StatModifierPreview { TargetStat = StatType.CriticalChance },
        new StatModifierPreview { TargetStat = StatType.CriticalDamage }
    };

    [MenuItem("Tools/Forest Guardians/Debug/Stat Scaling Calculator")]
    public static void Open()
    {
        GetWindow<StatScalingCalculatorWindow>("Stat Calculator");
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawSourceSection();
        EditorGUILayout.Space(8f);

        StatSnapshot stats = BuildStats();
        DrawStatBreakdown(stats);
        EditorGUILayout.Space(8f);

        DrawModifierSection();
        EditorGUILayout.Space(8f);

        DrawAbilitySection(stats);

        EditorGUILayout.EndScrollView();
    }

    private void DrawSourceSection()
    {
        EditorGUILayout.LabelField("Character Source", EditorStyles.boldLabel);

        _source = (StatCalculatorSource)EditorGUILayout.EnumPopup("Source", _source);

        switch (_source)
        {
            case StatCalculatorSource.Guardian:
                _guardian = (SO_Guardian)EditorGUILayout.ObjectField("Guardian", _guardian, typeof(SO_Guardian), false);
                break;

            case StatCalculatorSource.CuBot:
                _cuBot = (SO_CuBots)EditorGUILayout.ObjectField("CuBot", _cuBot, typeof(SO_CuBots), false);
                break;

            case StatCalculatorSource.Manual:
                EditorGUILayout.HelpBox("Manual mode uses the editable base values below.", MessageType.Info);
                break;
        }

        _characterLevel = EditorGUILayout.IntSlider("Character Level", _characterLevel, 1, 30);
    }

    private void DrawStatBreakdown(StatSnapshot stats)
    {
        EditorGUILayout.LabelField("Final Stats", EditorStyles.boldLabel);

        if (!stats.IsValid)
        {
            EditorGUILayout.HelpBox("Select a Guardian or CuBot, or switch to Manual mode.", MessageType.Warning);
            return;
        }

        DrawStatRow("Max Health", stats.MaxHealth);
        DrawStatRow("Health Regen", stats.HealthRegen);
        DrawStatRow("Move Speed", stats.MoveSpeed);
        DrawStatRow("Attack Speed", stats.AttackSpeed);
        DrawStatRow("Attack Power", stats.AttackPower);
        DrawStatRow("Ability Power", stats.AbilityPower);
        DrawStatRow("Haste", stats.Haste);
        DrawStatRow("Critical Chance", stats.CriticalChance);
        DrawStatRow("Critical Damage", stats.CriticalDamage);
        DrawStatRow("Lifesteal", stats.Lifesteal);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Expected Damage Multiplier", FormatFloat(GetExpectedCritMultiplier(stats)));
    }

    private void DrawStatRow(string label, StatPreview stat)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(150f));
        EditorGUILayout.LabelField($"Base@Level: {FormatFloat(stat.LeveledBase)}", GUILayout.Width(140f));
        EditorGUILayout.LabelField($"Flat: {FormatFloat(stat.FlatBonus)}", GUILayout.Width(90f));
        EditorGUILayout.LabelField($"Percent: {FormatPercent(stat.PercentBonus)}", GUILayout.Width(110f));
        EditorGUILayout.LabelField($"Final: {FormatFloat(stat.FinalValue)}");
        EditorGUILayout.EndHorizontal();
    }

    private void DrawModifierSection()
    {
        EditorGUILayout.LabelField("Temporary Modifier Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Percent values use decimal format, matching Sc_StatEffect. Example: 0.25 = +25%.", MessageType.None);

        int removeIndex = -1;

        for (int i = 0; i < _modifiers.Count; i++)
        {
            StatModifierPreview modifier = _modifiers[i];

            EditorGUILayout.BeginHorizontal();
            modifier.Enabled = EditorGUILayout.Toggle(modifier.Enabled, GUILayout.Width(20f));
            modifier.TargetStat = (StatType)EditorGUILayout.EnumPopup(modifier.TargetStat, GUILayout.Width(140f));
            modifier.FlatBonus = EditorGUILayout.FloatField("Flat", modifier.FlatBonus);
            modifier.PercentBonus = EditorGUILayout.FloatField("Percent", modifier.PercentBonus);

            if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                removeIndex = i;

            EditorGUILayout.EndHorizontal();
        }

        if (removeIndex >= 0)
            _modifiers.RemoveAt(removeIndex);

        if (GUILayout.Button("Add Modifier Row"))
            _modifiers.Add(new StatModifierPreview());
    }

    private void DrawAbilitySection(StatSnapshot stats)
    {
        EditorGUILayout.LabelField("Ability Scaling", EditorStyles.boldLabel);

        _ability = (SO_Ability)EditorGUILayout.ObjectField("Ability", _ability, typeof(SO_Ability), false);

        if (_ability == null)
        {
            EditorGUILayout.HelpBox("Select an SO_Ability to preview cooldowns and scaling entries.", MessageType.Info);
            return;
        }

        int maxAbilityLevel = Mathf.Max(1, _ability.MaxLevel);
        _abilityLevel = EditorGUILayout.IntSlider("Ability Level", _abilityLevel, 1, maxAbilityLevel);
        _cooldownMode = (CooldownPreviewMode)EditorGUILayout.EnumPopup("Cooldown Mode", _cooldownMode);

        DrawCooldownPreview(stats);
        DrawScalingEntries(stats);
    }

    private void DrawCooldownPreview(StatSnapshot stats)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Cooldown", EditorStyles.boldLabel);

        float baseCooldown = GetAbilityCooldownAtLevel(_ability, _abilityLevel);
        float finalCooldown = baseCooldown;

        if (_cooldownMode == CooldownPreviewMode.AbilityUsesHaste)
            finalCooldown = baseCooldown / (1f + stats.Haste.FinalValue / 100f);
        else if (_cooldownMode == CooldownPreviewMode.BasicAttackUsesAttackSpeed)
            finalCooldown = stats.AttackSpeed.FinalValue > 0f ? 1f / stats.AttackSpeed.FinalValue : 1f;

        EditorGUILayout.LabelField("Base Cooldown", FormatFloat(baseCooldown));
        EditorGUILayout.LabelField("Final Cooldown", FormatFloat(finalCooldown));
    }

    private void DrawScalingEntries(StatSnapshot stats)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Scaling Entries", EditorStyles.boldLabel);

        if (_ability.ScalingStats == null || _ability.ScalingStats.Count == 0)
        {
            EditorGUILayout.HelpBox("This ability has no ScalingStats entries.", MessageType.Info);
            return;
        }

        foreach (Sc_AbilityScalingEntry entry in _ability.ScalingStats)
        {
            if (entry == null)
                continue;

            int index = GetSafeScalingIndex(entry, _abilityLevel);
            float baseValue = GetArrayValue(entry.BaseValuePerLevel, index);
            float atkScale = GetArrayValue(entry.ATKScalingPerLevel, index);
            float apScale = GetArrayValue(entry.APScalingPerLevel, index);
            float finalValue = baseValue +
                               stats.AttackPower.FinalValue * atkScale +
                               stats.AbilityPower.FinalValue * apScale;
            float expectedCritValue = finalValue * GetExpectedCritMultiplier(stats);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(entry.StatName) ? "(Unnamed Scaling Entry)" : entry.StatName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Base Value", FormatFloat(baseValue));
            EditorGUILayout.LabelField("ATK Scaling", $"{FormatPercent(atkScale)} = {FormatFloat(stats.AttackPower.FinalValue * atkScale)}");
            EditorGUILayout.LabelField("AP Scaling", $"{FormatPercent(apScale)} = {FormatFloat(stats.AbilityPower.FinalValue * apScale)}");
            EditorGUILayout.LabelField("Final Value", FormatFloat(finalValue));
            EditorGUILayout.LabelField("Expected Crit-Weighted Value", FormatFloat(expectedCritValue));
            EditorGUILayout.EndVertical();
        }
    }

    private StatSnapshot BuildStats()
    {
        StatSnapshot stats = _source switch
        {
            StatCalculatorSource.Guardian when _guardian != null => StatSnapshot.FromGuardian(_guardian, _characterLevel),
            StatCalculatorSource.CuBot when _cuBot != null => StatSnapshot.FromCuBot(_cuBot, _characterLevel),
            StatCalculatorSource.Manual => StatSnapshot.Manual(_characterLevel),
            _ => new StatSnapshot()
        };

        foreach (StatModifierPreview modifier in _modifiers)
        {
            if (modifier.Enabled)
                stats.ApplyModifier(modifier);
        }

        return stats;
    }

    private float GetExpectedCritMultiplier(StatSnapshot stats)
    {
        float critChance = Mathf.Max(0f, stats.CriticalChance.FinalValue);
        float critDamage = Mathf.Max(1f, stats.CriticalDamage.FinalValue);
        return 1f + critChance * (critDamage - 1f);
    }

    private float GetAbilityCooldownAtLevel(SO_Ability ability, int abilityLevel)
    {
        if (ability.Cooldown == null || ability.Cooldown.Count == 0)
            return 0f;

        int index = Mathf.Clamp(abilityLevel - 1, 0, ability.Cooldown.Count - 1);
        return ability.Cooldown[index];
    }

    private int GetSafeScalingIndex(Sc_AbilityScalingEntry entry, int abilityLevel)
    {
        int length = int.MaxValue;
        if (entry.BaseValuePerLevel != null && entry.BaseValuePerLevel.Length > 0)
            length = Mathf.Min(length, entry.BaseValuePerLevel.Length);
        if (entry.ATKScalingPerLevel != null && entry.ATKScalingPerLevel.Length > 0)
            length = Mathf.Min(length, entry.ATKScalingPerLevel.Length);
        if (entry.APScalingPerLevel != null && entry.APScalingPerLevel.Length > 0)
            length = Mathf.Min(length, entry.APScalingPerLevel.Length);

        if (length == int.MaxValue)
            return 0;

        return Mathf.Clamp(abilityLevel - 1, 0, Mathf.Max(0, length - 1));
    }

    private float GetArrayValue(float[] values, int index)
    {
        if (values == null || values.Length == 0)
            return 0f;

        return values[Mathf.Clamp(index, 0, values.Length - 1)];
    }

    private string FormatFloat(float value)
    {
        return value.ToString("0.###");
    }

    private string FormatPercent(float value)
    {
        return $"{value:0.###} ({value * 100f:0.#}%)";
    }
}

public enum StatCalculatorSource
{
    Guardian,
    CuBot,
    Manual
}

public enum CooldownPreviewMode
{
    AbilityUsesHaste,
    BasicAttackUsesAttackSpeed,
    RawCooldown
}

[Serializable]
public class StatModifierPreview
{
    public bool Enabled = true;
    public StatType TargetStat;
    public float FlatBonus;
    public float PercentBonus;
}

public struct StatSnapshot
{
    public bool IsValid;
    public StatPreview MaxHealth;
    public StatPreview HealthRegen;
    public StatPreview MoveSpeed;
    public StatPreview AttackSpeed;
    public StatPreview AttackPower;
    public StatPreview AbilityPower;
    public StatPreview Haste;
    public StatPreview CriticalChance;
    public StatPreview CriticalDamage;
    public StatPreview Lifesteal;

    public static StatSnapshot FromGuardian(SO_Guardian guardian, int level)
    {
        return new StatSnapshot
        {
            IsValid = true,
            MaxHealth = StatPreview.Build(guardian.MaxHealth, guardian.MaxHealthScaling, level),
            HealthRegen = StatPreview.Build(guardian.HealthRegen, guardian.HealthRegenScaling, level),
            MoveSpeed = StatPreview.Build(guardian.MoveSpeed, guardian.MoveSpeedScaling, level),
            AttackSpeed = StatPreview.Build(guardian.AttackSpeed, guardian.AttackSpeedScaling, level),
            AttackPower = StatPreview.Build(guardian.AttackPower, guardian.AttackPowerScaling, level),
            AbilityPower = StatPreview.Build(guardian.AbilityPower, guardian.AbilityPowerScaling, level),
            Haste = StatPreview.Build(guardian.Haste, guardian.HasteScaling, level),
            CriticalChance = StatPreview.Build(guardian.CriticalChance, guardian.CriticalChanceScaling, level),
            CriticalDamage = StatPreview.Build(guardian.CriticalDamage, guardian.CriticalDamageScaling, level),
            Lifesteal = StatPreview.Build(guardian.LifeSteal, guardian.LifeStealScaling, level)
        };
    }

    public static StatSnapshot FromCuBot(SO_CuBots cuBot, int level)
    {
        return new StatSnapshot
        {
            IsValid = true,
            MaxHealth = StatPreview.Build(cuBot.MaxHealth, cuBot.MaxHealthScaling, level),
            HealthRegen = StatPreview.Build(cuBot.HealthRegen, cuBot.HealthRegenScaling, level),
            MoveSpeed = StatPreview.Build(cuBot.MoveSpeed, cuBot.MoveSpeedScaling, level),
            AttackSpeed = StatPreview.Build(cuBot.AttackSpeed, cuBot.AttackSpeedScaling, level),
            AttackPower = StatPreview.Build(cuBot.AttackPower, cuBot.AttackPowerScaling, level),
            AbilityPower = StatPreview.Build(cuBot.AbilityPower, cuBot.AbilityPowerScaling, level),
            Haste = StatPreview.Build(cuBot.Haste, cuBot.HasteScaling, level),
            CriticalChance = StatPreview.Build(cuBot.CriticalChance, cuBot.CriticalChanceScaling, level),
            CriticalDamage = StatPreview.Build(cuBot.CriticalDamage, cuBot.CriticalDamageScaling, level),
            Lifesteal = StatPreview.Build(cuBot.LifeSteal, cuBot.LifeStealScaling, level)
        };
    }

    public static StatSnapshot Manual(int level)
    {
        return new StatSnapshot
        {
            IsValid = true,
            MaxHealth = StatPreview.Build(0f, 0f, level),
            HealthRegen = StatPreview.Build(0f, 0f, level),
            MoveSpeed = StatPreview.Build(0f, 0f, level),
            AttackSpeed = StatPreview.Build(1f, 0f, level),
            AttackPower = StatPreview.Build(0f, 0f, level),
            AbilityPower = StatPreview.Build(0f, 0f, level),
            Haste = StatPreview.Build(0f, 0f, level),
            CriticalChance = StatPreview.Build(0f, 0f, level),
            CriticalDamage = StatPreview.Build(1.5f, 0f, level),
            Lifesteal = StatPreview.Build(0f, 0f, level)
        };
    }

    public void ApplyModifier(StatModifierPreview modifier)
    {
        switch (modifier.TargetStat)
        {
            case StatType.MaxHealth: MaxHealth.Apply(modifier); break;
            case StatType.HealthRegen: HealthRegen.Apply(modifier); break;
            case StatType.MoveSpeed: MoveSpeed.Apply(modifier); break;
            case StatType.AttackSpeed: AttackSpeed.Apply(modifier); break;
            case StatType.AttackPower: AttackPower.Apply(modifier); break;
            case StatType.AbilityPower: AbilityPower.Apply(modifier); break;
            case StatType.Haste: Haste.Apply(modifier); break;
            case StatType.CriticalChance: CriticalChance.Apply(modifier); break;
            case StatType.CriticalDamage: CriticalDamage.Apply(modifier); break;
            case StatType.Lifesteal: Lifesteal.Apply(modifier); break;
        }
    }
}

public struct StatPreview
{
    public float LeveledBase;
    public float FlatBonus;
    public float PercentBonus;
    public float FinalValue;

    public static StatPreview Build(float baseValue, float scalingPerLevel, int level)
    {
        int levelsGained = Mathf.Max(0, level - 1);
        float leveledBase = baseValue * (1f + scalingPerLevel * levelsGained);

        return new StatPreview
        {
            LeveledBase = leveledBase,
            FinalValue = leveledBase
        };
    }

    public void Apply(StatModifierPreview modifier)
    {
        FlatBonus += modifier.FlatBonus;
        PercentBonus += modifier.PercentBonus;
        FinalValue = (LeveledBase + FlatBonus) * (1f + PercentBonus);
    }
}
