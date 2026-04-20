// Rajah_R_Branch2.cs
// [R] Eagle Eye — Branch 2 of Rajah Bagwis's ultimate ability.
// Multi-feather ranged attack.
//
// TODO: Implement full ability logic when R Branch 2 is designed.

using UnityEngine;

public class Rajah_R_Branch2 : Sc_BaseAbility
{
    public Rajah_R_Branch2(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user) { }


    public override void OnEquip(Mb_CharacterBase user)
    {
        Debug.Log($"[{user.name}] Eagle Eye (Branch 2) equipped.");
    }

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        // TODO: Implement Eagle Eye — multi-feather ranged barrage
        Debug.Log($"[{user.name}] Eagle Eye activated. (Not yet implemented)");

        StartCooldown(user, GetAbilityCooldown(user));
    }

    public override void OnUnequip(Mb_CharacterBase user)
    {
        Debug.Log($"[{user.name}] Eagle Eye unequipped.");
    }

    protected override void TriggerAbilityAnimation(Mb_CharacterBase user)
    {
        // TODO: Wire animator trigger when animation is ready
    }
}