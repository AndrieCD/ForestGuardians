using System.Collections.Generic;
using UnityEngine;

// Holds all data for one ability — info, cooldown, visual assets, and all per-level scaling.
// Ability scripts read from this SO instead of hardcoding any numbers.
[CreateAssetMenu(fileName = "NewAbility_SO", menuName = "Abilities/Ability_SO")]
public class SO_Ability : ScriptableObject
{
    [Header("Ability Info")]
    public string AbilityName;
    // public Sprite Icon; // TODO: Uncomment when UI is ready

    [Header("General Settings")]
    public float Cooldown;
    public GameObject ProjectileModel;

    [Header("Per-Level Scaling")]
    [Tooltip("Add one entry per combat effect this ability has. " +
             "e.g. Sky Rend gets a 'Damage' entry and a 'Shield' entry.")]
    public List<Sc_AbilityScalingEntry> ScalingStats;

    // ----------------------------------------------------
    // Convenience method: looks up a stat by name and returns its computed value.
    // Ability scripts call this instead of hardcoding arrays.
    //
    // Example: _AbilityData.GetStat("Damage", currentLevel, user.AttackPower.Value(), user.AbilityPower.Value())
    // ----------------------------------------------------

    /// <summary>
    /// Looks up the given stat name in the ScalingStats list and returns the computed value for the given ability level and user stats.
    /// </summary>
    /// <param name="statName"></param> The name of the stat to look up — e.g. "Damage", "Shield", "DoT", "Slow", "Lifesteal". 
    /// <param name="abilityLevel"></param> The current level of the ability (1-based, so level 1 = index 0 in the arrays). This is used to determine which values to pull from the arrays in the SO.
    /// <param name="atk"></param> The user's current Attack Power stat, used for calculating any ATK-based scaling. Optional — only needed if the stat being looked up has ATK scaling.
    /// <param name="ap"></param> The user's current Ability Power stat, used for calculating any AP-based scaling. Optional — only needed if the stat being looked up has AP scaling.
    /// <returns></returns>
    public float GetStat(string statName, int abilityLevel, float atk = 0f, float ap = 0f)
    {
        foreach (var entry in ScalingStats)
        {
            if (entry.StatName == statName)
                return entry.GetValue(abilityLevel, atk, ap);
        }

        // If the stat name wasn't found, warn the developer — it's probably a typo
        Debug.LogWarning($"SO_Ability '{AbilityName}': No scaling entry named '{statName}' found.");
        return 0f;
    }
}