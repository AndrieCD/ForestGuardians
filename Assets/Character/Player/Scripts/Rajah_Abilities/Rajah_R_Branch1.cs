// Rajah_R_Branch1.cs
// [R] Sovereign's Wrath — Branch 1 of Rajah Bagwis's ultimate ability.
// Lifesteal-focused melee strike.
//
// TODO: Implement full ability logic when R Branch 1 is designed.

using UnityEngine;

public class Rajah_R_Branch1 : Sc_BaseAbility
{
    public Rajah_R_Branch1(SO_Ability abilityData, Mb_CharacterBase user)
        : base(abilityData, user) { }


    public override void OnEquip(Mb_CharacterBase user)
    {
        Debug.Log($"[{user.name}] Sovereign's Wrath (Branch 1) equipped.");
    }

    public override void Activate(Mb_CharacterBase user)
    {
        if (!CheckCooldown()) return;

        // TODO: Implement Sovereign's Wrath — lifesteal melee strike
        Debug.Log($"[{user.name}] Sovereign's Wrath activated. (Not yet implemented)");

        StartCooldown(user, GetAbilityCooldown(user));
    }

    public override void OnUnequip(Mb_CharacterBase user)
    {
        Debug.Log($"[{user.name}] Sovereign's Wrath unequipped.");
    }

    protected override void TriggerAbilityAnimation(Mb_CharacterBase user)
    {
        // TODO: Wire animator trigger when animation is ready
    }
}