using UnityEngine;

public class Passive_Ability : Sc_BaseAbility
{
    public Passive_Ability(SO_Ability abilityObject) : base(abilityObject)
    {
    }

    // 1. Called when the Character spawns (Setup Passive)
    public override void OnEquip(Mb_GuardianBase user) 
    {
        // Example: Increase user's Max Health by 1% when this passive is equipped
        user.MaxHealth.AddModifier(new Sc_StatModifier(1f, StatModType.Percent, float.PositiveInfinity), user); 
        user.Heal(user.MaxHealth.Value()); // Heal to full after increasing max health

        // Debug log to confirm activation
        Debug.Log($"{user.name} has equipped {this._AbilityData.AbilityName}, increasing Max Health by 20%.");
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
