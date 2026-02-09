// Guardian_SO.cs
/// <summary>
/// This ScriptableObject holds the base stats and configuration for a Guardian character.
/// </summary>
using UnityEngine;

[CreateAssetMenu(fileName = "NewGuardian_SO", menuName = "Guardians/Guardians_SO")]
public class SO_Guardian : ScriptableObject
{
    [Header("General")]
    public string CharacterName;
    //public Sprite icon;
    //public GameObject ModelPrefab;          // The 3D model specific to this guardian

    [Header("Base Stats")]
    public float MaxHealth = 500f;          // Maximum health points
    public float HealthRegen = 10f;         // Health regenerated per second
    public float MoveSpeed = 8f;            // Units per second
    public float AttackSpeed = 1.0f;        // Attacks per second
    public float AttackPower = 100f;        // "Physical" attack damage
    public float AbilityPower = 80f;        // "Magical" ability damage
    public float CooldownReduction = 0f;    // Percentage reduction in ability cooldowns
    public float CriticalChance = 0.0f;     // Chance to deal critical damage (0.0 to 1.0)
    public float CriticalDamage = 1.5f;     // Multiplier for critical damage
    // additional stats
    public float LifeSteal = 0f;            // Percentage of damage dealt returned as health
    public float Shielding = 0f;            // Current shield value

    [Header("Ability SObjects")]
    public SO_Ability PassiveAbility;       // Passive Ability
    public SO_Ability AbilityQ;             // Q Ability
    public SO_Ability AbilityE;             // E Ability
    public SO_Ability AbilityR;             // R Ability
    public SO_Ability PrimaryAttack;        // Primary Attack or Left Click
    public SO_Ability SecondaryAttack;      // Secondary Attack or Right Click

}