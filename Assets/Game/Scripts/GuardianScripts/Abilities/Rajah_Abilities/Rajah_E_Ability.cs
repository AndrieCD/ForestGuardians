using UnityEngine;

public class Rajah_E_Ability : Sc_BaseAbility
{
    public Rajah_E_Ability(SO_Ability abilityObject, Mb_GuardianBase user) : base(abilityObject, user)
    {
    }


    // 1. Called when the Character spawns (Setup Passive)
    public override void OnEquip(Mb_GuardianBase user)
    {
        // Debug
        Debug.Log($"{user.name} has equipped {this._AbilityData.AbilityName}.");
    }

    // 2. Called when the Player presses the button (Active)
    public override void Activate(Mb_GuardianBase user)
    {
        // Debug
        Debug.Log($"{user.name} has activated {this._AbilityData.AbilityName}.");
    }

    // 3. Called when Character dies or ability is swapped (Cleanup)
    public override void OnUnequip(Mb_GuardianBase user)
    {
        Debug.Log($"{user.name} has unequipped {this._AbilityData.AbilityName}.");
    }
}
