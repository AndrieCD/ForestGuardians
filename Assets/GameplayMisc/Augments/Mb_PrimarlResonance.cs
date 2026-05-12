// Augment_PrimalResonance.cs
// Each ability cast grants stacking +5% AP and ATK (percent, additive).
// All stacks are wiped at the end of every wave — start fresh each wave.
//
// HOW IT WORKS:
//   - On equip, subscribe to Mb_AbilityController.OnAbilityActivated.
//   - Each cast increments _stacks and mutates the Value fields on the two
//     Sc_StatEffect entries inside our single modifier, then calls
//     NotifyStatsChanged() — no modifier add/remove per cast, just a value update.
//   - On wave end, remove the modifier entirely and reset _stacks to 0.
//   - On equip of the next wave's first cast, the modifier is rebuilt from scratch.
//
// This is the same mutating-effect pattern used by Augment_HeartOfTheForest.
//
// Inspector setup: none required.
// TODO: Tune STACK_BONUS_PERCENT and MAX_STACKS for balance if needed.

using System.Collections.Generic;
using UnityEngine;

public class Mb_PrimalResonance : Sc_AugmentBase
{
    // How much AP and ATK each stack adds, as a percent (0.05 = 5%)
    private const float STACK_BONUS_PERCENT = 0.05f;

    // Optional cap — set to a high number to effectively uncap.
    // TODO: Tune this. Uncapped stacks could become very strong by wave 15.
    private const int MAX_STACKS = 20;

    // How many stacks are currently active this wave
    private int _stacks = 0;

    // The single modifier we keep alive while stacks > 0.
    // Null when there are no stacks (start of wave or after reset).
    private Sc_Modifier _stackModifier;

    // Direct references to the two effect entries inside _stackModifier.
    // We mutate their Value fields directly instead of rebuilding the modifier —
    // same pattern as HeartOfTheForest's regen effect.
    private Sc_StatEffect _apEffect;
    private Sc_StatEffect _atkEffect;


    public Mb_PrimalResonance(SO_Augment data, Mb_CharacterBase owner)
        : base(data, owner) { }


    public override void OnEquip(Mb_CharacterBase owner)
    {
        // No static SO effects — skip base.OnEquip().

        // Listen for any ability cast on this character's ability controller
        owner.Abilities.OnAbilityActivated += HandleAbilityActivated;

        // Listen for wave end so we can wipe stacks between waves
        Mb_WaveManager.OnWaveEnd += HandleWaveEnd;

        Debug.Log("[Primal Resonance] Equipped. Listening for ability casts.");
    }


    public override void OnUnequip(Mb_CharacterBase owner)
    {
        owner.Abilities.OnAbilityActivated -= HandleAbilityActivated;
        Mb_WaveManager.OnWaveEnd -= HandleWaveEnd;

        // Clean up whatever stacks remain
        ClearStacks();

        Debug.Log("[Primal Resonance] Unequipped. Stacks cleared.");
    }


    private void HandleAbilityActivated(string slotName)
    {
        // Cap check — don't add stacks beyond the maximum
        if (_stacks >= MAX_STACKS) return;

        _stacks++;

        if (_stackModifier == null)
        {
            // First cast this wave — build the modifier and its two effect entries.
            // Values start at one stack's worth of bonus.
            _apEffect = new Sc_StatEffect(StatType.AbilityPower, STACK_BONUS_PERCENT, StatModType.Percent);
            _atkEffect = new Sc_StatEffect(StatType.AttackPower, STACK_BONUS_PERCENT, StatModType.Percent);

            _stackModifier = new Sc_Modifier(
                "Primal Resonance — Stacks",
                ModifierSource.Augment,
                new List<Sc_StatEffect> { _apEffect, _atkEffect }
            );

            _Owner.Stats.AddModifier(_stackModifier);
        }
        else
        {
            // Subsequent casts — just update the effect values in place.
            // No need to remove and re-add the modifier; mutating the effect
            // values and notifying is cheaper and avoids a StatBlock list churn.
            _apEffect.Value = STACK_BONUS_PERCENT * _stacks;
            _atkEffect.Value = STACK_BONUS_PERCENT * _stacks;

            // Tell StatBlock something changed so UI and anything reading
            // stats this frame gets the updated values.
            _Owner.Stats.NotifyStatsChanged();
        }

        Debug.Log($"[Primal Resonance] Stack {_stacks}/{MAX_STACKS} — " +
                  $"+{_apEffect.Value * 100f:F0}% AP and ATK.");
    }


    private void HandleWaveEnd(int completedWaveIndex)
    {
        // Wipe all stacks at the end of every wave, regardless of count.
        // The player starts the next wave at zero stacks and builds again.
        if (_stacks > 0)
        {
            Debug.Log($"[Primal Resonance] Wave {completedWaveIndex} ended. " +
                      $"Resetting {_stacks} stacks.");
            ClearStacks();
        }
    }


    private void ClearStacks()
    {
        if (_stackModifier != null)
        {
            _Owner.Stats.RemoveModifier(_stackModifier);
            _stackModifier = null;
        }

        // Null the effect refs too — next cast will rebuild them fresh
        _apEffect = null;
        _atkEffect = null;
        _stacks = 0;
    }
}