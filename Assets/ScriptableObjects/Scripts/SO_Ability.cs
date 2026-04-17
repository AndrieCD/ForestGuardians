// SO_Ability.cs
// Holds all data for one ability — info, cooldown, visual assets, and per-level scaling.
// Ability scripts read from this SO instead of hardcoding any numbers.

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAbility_SO", menuName = "Abilities/SO_Ability")]
public class SO_Ability : ScriptableObject
{
    [Header("Ability Info")]
    public string AbilityName;
    public Sprite Icon;

    [Header("General Settings")]
    public float Cooldown;
    public GameObject ProjectileModel;

    [Header("Progression")]
    [Tooltip("How many times this ability can be upgraded. " +
             "Level 1 is the starting level, so MaxLevel 6 means 5 upgrades are possible.")]
    public int MaxLevel = 6;

    [Header("Per-Level Scaling")]
    [Tooltip("Add one entry per combat effect this ability has. " +
             "e.g. Sky Rend gets a 'Damage' entry and a 'Shield' entry.")]
    public List<Sc_AbilityScalingEntry> ScalingStats;


    /// <summary>
    /// Looks up a stat by name and returns its computed value for the given
    /// ability level and user stats. Ability scripts call this instead of
    /// hardcoding arrays — all numbers live in the Inspector.
    /// </summary>
    /// <param name="statName">e.g. "Damage", "Shield", "DoT"</param>
    /// <param name="abilityLevel">Current ability level (1-based).</param>
    /// <param name="atk">User's AttackPower.Value() — pass 0 if unused.</param>
    /// <param name="ap">User's AbilityPower.Value() — pass 0 if unused.</param>
    public float GetStat(string statName, int abilityLevel, float atk = 0f, float ap = 0f)
    {
        foreach (var entry in ScalingStats)
        {
            if (entry.StatName == statName)
                return entry.GetValue(abilityLevel, atk, ap);
        }

        Debug.LogWarning($"SO_Ability '{AbilityName}': No scaling entry named '{statName}' found.");
        return 0f;
    }
}