using UnityEngine;

public class Q_Ability : Sc_BaseAbility
{
    public Q_Ability(SO_Ability abilityObject) : base(abilityObject)
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

        // Example: Increase user's Max Health by 20HP (flat) when activated
        user.MaxHealth.AddModifier(new Sc_StatModifier(20f, StatModType.Flat, 2f), user);
    }

    // 3. Called when Character dies or ability is swapped (Cleanup)
    public override void OnUnequip(Mb_GuardianBase user)
    {
        Debug.Log($"{user.name} has unequipped {this._AbilityData.AbilityName}.");
    }
}
