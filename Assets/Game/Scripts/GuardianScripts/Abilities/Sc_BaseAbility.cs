using UnityEngine;

public class Sc_BaseAbility
{
    [Header("Base Ability Object")]
    [SerializeField] protected SO_Ability _AbilityData;

    public Sc_BaseAbility(SO_Ability abilityObject)
    {
        _AbilityData = abilityObject;
    }

    // 1. Called when the Character spawns (Setup Passive)
    public virtual void OnEquip(Mb_GuardianBase user) { }

    // 2. Called when the Player presses the button (Active)
    public virtual void Activate(Mb_GuardianBase user) { }

    // 3. Called when Character dies or ability is swapped (Cleanup)
    public virtual void OnUnequip(Mb_GuardianBase user) { }
}
