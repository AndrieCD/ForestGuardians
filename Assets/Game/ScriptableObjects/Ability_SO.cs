// AbilitySO.cs (Updated)
using UnityEngine;

[CreateAssetMenu(fileName = "NewAbility_SO", menuName = "Abilities/Ability_SO")]
public class SO_Ability : ScriptableObject
{
    [Header("Ability Info")]
    public string AbilityName;  // Name of the ability
    //public Sprite icon;

    [Header("General Ability Stats")]
    public float Cooldown;      // Cooldown time in seconds
    public float BaseDamage;    // Base damage of the ability
    public float ATKScaling;    // Scaling factor based on Attack Power (ATK)
    public float APScaling;     // Scaling factor based on Ability Power (AP
    public float BaseHealing;   // Base healing of the ability
    public float BaseShield;    // Base shield amount of the ability

}