using UnityEngine;

public class Mari_Passive_Ability : Sc_BaseAbility
{
    public Mari_Passive_Ability(SO_Ability abilityObject) : base(abilityObject)
    {
    }

    // 1. Called when the Character spawns (Setup Passive)
    public override void OnEquip(Mb_GuardianBase user) 
    {
        // Example: Increase user's Ability Power by 1% when this passive is equipped
        user.AbilityPower.AddModifier(new Sc_StatModifier(1f, StatModType.Percent, float.PositiveInfinity), user); 

        // Debug log to confirm activation
        Debug.Log($"{user.name} has equipped {this._AbilityData.AbilityName}, increasing Ability Power by 1%.");
    }

    // 3. Called when Character dies or ability is swapped (Cleanup)
    public override void OnUnequip(Mb_GuardianBase user)
    {
        // Remove the Max Health modifier when the passive is unequipped
        user.MaxHealth.RemoveModifier(new Sc_StatModifier(20f, StatModType.Flat));
        // Debug log to confirm deactivation
        Debug.Log($"{user.name} has unequipped {this._AbilityData.AbilityName}, removing Max Health bonus.");
    }
}
