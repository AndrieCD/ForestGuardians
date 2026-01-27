using UnityEngine;

public class Passive_Ability : Sc_BaseAbility
{
    public Passive_Ability(SO_Ability abilityObject, Mb_GuardianBase user) : base(abilityObject, user)
    {
    }

    // 1. Called when the Character spawns (Setup Passive)
    public override void OnEquip(Mb_GuardianBase user) 
    {

        // Debug log to confirm activation
        Debug.Log($"{user.name} has equipped {this._AbilityData.AbilityName}, increasing Max Health by 20%.");
    }

    // 3. Called when Character dies or ability is swapped (Cleanup)
    public override void OnUnequip(Mb_GuardianBase user)
    {
        
        // Debug log to confirm deactivation
        Debug.Log($"{user.name} has unequipped {this._AbilityData.AbilityName}, removing Max Health bonus.");
    }
}
