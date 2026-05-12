// Augment_HuntersInstinct.cs
// Gain +25% Crit Chance and +50% Crit Damage.
// Every critical strike heals the player for 5% of the damage dealt.
//
// HOW IT WORKS:
//   - The +25% CritChance and +50% CritDamage are static SO effects — base.OnEquip()
//     applies them automatically from the SO_Augment Inspector fields.
//   - We subscribe to Sc_BaseAbility.OnCriticalHit on equip — a static event fired
//     from inside ApplyCriticalStrike() whenever any ability lands a crit.
//   - On each crit, we check that the attacker is our owner, then heal for 5% of damage.
//   - OnUnequip unsubscribes cleanly so no ghost listeners remain after stage end.
//
// Inspector setup: assign the SO_Augment asset. In its Effects list add:
//   - StatType: CriticalChance | Value: 0.25 | Type: Percent
//   - StatType: CriticalDamage | Value: 0.50 | Type: Percent

using UnityEngine;

public class Mb_HuntersInstinct : Sc_AugmentBase
{
    // Fraction of crit damage returned as healing — 5% of damage dealt per crit
    private const float HEAL_PERCENT = 0.05f;


    public Mb_HuntersInstinct(SO_Augment data, Mb_CharacterBase owner)
        : base(data, owner) { }


    public override void OnEquip(Mb_CharacterBase owner)
    {
        // Apply the +25% CritChance and +50% CritDamage from the SO effects list.
        // The base class reads the SO and applies everything automatically.
        base.OnEquip(owner);

        // Subscribe to the static crit event so we catch every crit from every ability slot.
        // Static event means one subscription covers Primary, Secondary, Q, E, R — all of them.
        Sc_BaseAbility.OnCriticalHit += HandleCriticalHit;

        Debug.Log("[Hunter's Instinct] Equipped. Listening for critical strikes.");
    }


    public override void OnUnequip(Mb_CharacterBase owner)
    {
        // Unsubscribe before removing stat effects — order matters here because
        // base.OnUnequip() fires OnStatsChanged, and we don't want to be
        // listening to anything by the time that propagates.
        Sc_BaseAbility.OnCriticalHit -= HandleCriticalHit;

        // Remove the crit stat modifiers applied by base.OnEquip()
        base.OnUnequip(owner);

        Debug.Log("[Hunter's Instinct] Unequipped.");
    }


    private void HandleCriticalHit(float critDamage, Mb_CharacterBase attacker)
    {
        // Only heal when OUR owner lands a crit — the event is static so every
        // Hunter's Instinct instance (if somehow two existed) would receive it.
        // The attacker check ensures we only respond to our owner's crits.
        if (attacker != _Owner) return;

        float healAmount = critDamage * HEAL_PERCENT;
        _Owner.Health.Heal(healAmount);

        Debug.Log($"[Hunter's Instinct] Crit healed {healAmount} HP (5% of {critDamage} crit damage).");
    }
}