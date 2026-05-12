// Augment_CycleOfLife.cs
// Every 8 takedowns: heal for 5% Max HP, then permanently gain 2% more Max HP.
// Both the heal and the Max HP gain compound — each milestone is slightly
// more powerful than the last because Max HP keeps growing.
//
// HOW IT WORKS:
//   - We subscribe to MB_CuBotBase.OnCuBotKill on equip.
//   - OnCuBotKill passes the attacker — we only count kills by our owner.
//   - Every 8 kills triggers a milestone: a new permanent flat Max HP modifier
//     is added, then the player is healed for 5% of the new (grown) Max HP.
//   - We store every milestone modifier in a list so OnUnequip can surgically
//     remove exactly our modifiers without touching anyone else's.
//
// Inspector setup: none required.
// TODO: Tune KILLS_PER_MILESTONE and HP_GROWTH_PERCENT if balance needs adjusting.

using System.Collections.Generic;
using UnityEngine;

public class Mb_CycleOfLife : Sc_AugmentBase
{
    // How many kills are needed to trigger one milestone
    private const int KILLS_PER_MILESTONE = 8;

    // How much Max HP to permanently add per milestone, as a fraction of current Max HP
    private const float HP_GROWTH_PERCENT = 0.02f;

    // How much to heal on each milestone, as a fraction of current Max HP
    // (applied AFTER the Max HP growth modifier, so it benefits from the new max)
    private const float HEAL_PERCENT = 0.05f;

    // Counts kills since the last milestone — resets every 8 kills
    private int _killsThisMilestone = 0;

    // Every milestone adds one modifier to this list.
    // We keep our own references so OnUnequip removes exactly these —
    // not all augment modifiers, just ours.
    private List<Sc_Modifier> _milestoneModifiers = new List<Sc_Modifier>();


    public Mb_CycleOfLife(SO_Augment data, Mb_CharacterBase owner)
        : base(data, owner) { }


    public override void OnEquip(Mb_CharacterBase owner)
    {
        // This augment has no static SO effects — skip base.OnEquip().
        // All effects are applied dynamically on kill milestones.

        // Start listening for enemy deaths so we can count player kills
        MB_CuBotBase.OnCuBotKill += HandleKill;

        Debug.Log("[Cycle of Life] Equipped. Counting takedowns.");
    }


    public override void OnUnequip(Mb_CharacterBase owner)
    {
        // Stop listening — we don't want to count kills after the stage ends
        MB_CuBotBase.OnCuBotKill -= HandleKill;

        // Remove every Max HP modifier we accumulated during the stage
        foreach (var modifier in _milestoneModifiers)
            owner.Stats.RemoveModifier(modifier);

        _milestoneModifiers.Clear();
        _killsThisMilestone = 0;

        Debug.Log("[Cycle of Life] Unequipped. All milestone modifiers removed.");
    }


    private void HandleKill(Mb_CharacterBase attacker)
    {
        // Only count kills made by the player who owns this augment.
        // attacker can be null if SetLastAttacker() was never called on the CuBot —
        // we guard here so a null attacker is silently ignored.
        if (attacker != _Owner) return;

        _killsThisMilestone++;

        Debug.Log($"[Cycle of Life] Kill {_killsThisMilestone}/{KILLS_PER_MILESTONE}.");

        if (_killsThisMilestone >= KILLS_PER_MILESTONE)
            TriggerMilestone();
    }


    private void TriggerMilestone()
    {
        _killsThisMilestone = 0;

        // Step 1: Calculate how much flat HP to add.
        // We use GetValue() (not BaseValue) so the growth compounds —
        // each milestone grows on top of all previous ones.
        float currentMaxHP = _Owner.Stats.MaxHealth.GetValue();
        float hpGain = currentMaxHP * HP_GROWTH_PERCENT;

        // Step 2: Build and apply the permanent Max HP modifier.
        // Each milestone gets its own modifier instance so we can track
        // and remove them individually on unequip.
        var milestoneModifier = new Sc_Modifier(
            $"Cycle of Life — Milestone {_milestoneModifiers.Count + 1}",
            ModifierSource.Augment,
            new List<Sc_StatEffect>
            {
                new Sc_StatEffect(StatType.MaxHealth, hpGain, StatModType.Flat)
            }
        );

        _milestoneModifiers.Add(milestoneModifier);
        _Owner.Stats.AddModifier(milestoneModifier);

        // Step 3: Heal the player AFTER applying the Max HP boost,
        // so the heal scales off the newly grown max.
        float healAmount = _Owner.Stats.MaxHealth.GetValue() * HEAL_PERCENT;
        _Owner.Health.Heal(healAmount);

        Debug.Log($"[Cycle of Life] Milestone! +{hpGain} Max HP, healed {healAmount}. " +
                  $"Total milestones: {_milestoneModifiers.Count}.");
    }
}