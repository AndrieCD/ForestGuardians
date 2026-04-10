using UnityEngine;

public class Passive_Ability : Sc_BaseAbility
{
    public Passive_Ability(SO_Ability abilityObject, Mb_CharacterBase user) : base(abilityObject, user)
    {
    }

    // 1. Called when the Character spawns (Setup Passive)
    public override void OnEquip(Mb_CharacterBase user) 
    {

        // Debug log to confirm activation
    }

    // 3. Called when Character dies or ability is swapped (Cleanup)
    public override void OnUnequip(Mb_CharacterBase user)
    {
        
        // Debug log to confirm deactivation
    }
}
