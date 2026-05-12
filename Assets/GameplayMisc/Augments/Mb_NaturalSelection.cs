// Augment_NaturalSelection.cs
// Reads the current bonus ATK and bonus AP. Whichever is greater gets
// converted into the other at 200% value — the greater stat's bonus is
// zeroed out (negative flat modifier) and the lesser receives 200% of
// that amount as a flat bonus.
//
// Re-evaluates every time any stat changes so Primal Resonance stacks,
// level-ups, and other augments are always accounted for.
//
// HOW IT WORKS:
//   - Subscribe to Mb_StatBlock.OnStatsChanged on equip.
//   - On each change, read BonusValue() on both ATK and AP.
//   - Remove the old conversion modifier pair, build a new one, apply it.
//   - A _isEvaluating flag prevents the evaluation from triggering itself
//     (since AddModifier fires OnStatsChanged, which would re-enter here).
//
// EXAMPLE:
//   ATK bonus = 200, AP bonus = 50. ATK is greater.
//   → AP receives +(200 * 2) = +400 flat.
//   → ATK receives -200 flat (bonus zeroed).
//   Net result: ATK is at base, AP has 400 bonus on top of its existing 50.
//
// Inspector setup: none required.

using System.Collections.Generic;
using UnityEngine;

public class Mb_NaturalSelection : Sc_AugmentBase
{
    // The two modifiers we apply as a matched pair every evaluation.
    // One is always a negative (zeroing the greater stat's bonus),
    // one is always a positive (boosting the lesser at 200%).
    // Both are null when no conversion is active (e.g. both bonuses are zero).
    private Sc_Modifier _negativeModifier;
    private Sc_Modifier _positiveModifier;

    // Guard flag — prevents re-entry when our own AddModifier call
    // fires OnStatsChanged, which would otherwise cause infinite recursion.
    private bool _isEvaluating = false;


    public Mb_NaturalSelection(SO_Augment data, Mb_CharacterBase owner)
        : base(data, owner) { }


    public override void OnEquip(Mb_CharacterBase owner)
    {
        // No static SO effects — skip base.OnEquip().

        // Subscribe to stat changes so we re-evaluate whenever anything
        // that could affect ATK or AP bonus values changes.
        owner.Stats.OnStatsChanged += HandleStatsChanged;

        // Run an immediate evaluation so the conversion is active from the moment
        // the augment is equipped, not just on the next stat change.
        Evaluate();

        Debug.Log("[Natural Selection] Equipped.");
    }


    public override void OnUnequip(Mb_CharacterBase owner)
    {
        owner.Stats.OnStatsChanged -= HandleStatsChanged;

        // Remove both conversion modifiers cleanly
        RemoveConversionPair();

        Debug.Log("[Natural Selection] Unequipped. Conversion modifiers removed.");
    }


    private void HandleStatsChanged()
    {
        // Guard: if we're already inside Evaluate(), do nothing.
        // This breaks the loop: Evaluate → AddModifier → OnStatsChanged → Evaluate...
        if (_isEvaluating) return;

        Evaluate();
    }


    private void Evaluate()
    {
        _isEvaluating = true;

        // Step 1: Read current bonus values BEFORE removing old modifiers.
        // We remove first, then re-read, so the old conversion doesn't
        // pollute the bonus values we're basing the new conversion on.
        RemoveConversionPair();

        float atkBonus = _Owner.Stats.AttackPower.BonusValue();
        float apBonus = _Owner.Stats.AbilityPower.BonusValue();

        // Step 2: If neither stat has any bonus, there's nothing to convert.
        // Skip to avoid applying zeroed or negative modifiers with no purpose.
        if (atkBonus <= 0f && apBonus <= 0f)
        {
            Debug.Log("[Natural Selection] No bonus stats to convert. Waiting.");
            _isEvaluating = false;
            return;
        }

        // Step 3: Determine which stat is greater and build the conversion pair.
        // Equal bonus defaults to treating AP as the greater (arbitrary tie-break).
        if (atkBonus > apBonus)
        {
            // ATK is greater — zero it out, pour 200% of it into AP
            _negativeModifier = BuildModifier(
                "Natural Selection — ATK Zeroed",
                StatType.AttackPower,
                -atkBonus   // exactly cancels the ATK bonus
            );

            _positiveModifier = BuildModifier(
                "Natural Selection — AP Boosted",
                StatType.AbilityPower,
                atkBonus * 2f   // 200% of ATK bonus added to AP
            );

            Debug.Log($"[Natural Selection] ATK ({atkBonus}) > AP ({apBonus}). " +
                      $"Zeroing ATK, adding {atkBonus * 2f} to AP.");
        }
        else
        {
            // AP is greater (or equal) — zero it out, pour 200% of it into ATK
            _negativeModifier = BuildModifier(
                "Natural Selection — AP Zeroed",
                StatType.AbilityPower,
                -apBonus    // exactly cancels the AP bonus
            );

            _positiveModifier = BuildModifier(
                "Natural Selection — ATK Boosted",
                StatType.AttackPower,
                apBonus * 2f    // 200% of AP bonus added to ATK
            );

            Debug.Log($"[Natural Selection] AP ({apBonus}) >= ATK ({atkBonus}). " +
                      $"Zeroing AP, adding {apBonus * 2f} to ATK.");
        }

        // Step 4: Apply both modifiers.
        // Each AddModifier call fires OnStatsChanged — the _isEvaluating flag
        // ensures HandleStatsChanged ignores those calls while we're mid-evaluation.
        _Owner.Stats.AddModifier(_negativeModifier);
        _Owner.Stats.AddModifier(_positiveModifier);

        _isEvaluating = false;
    }


    private void RemoveConversionPair()
    {
        // Remove both modifiers if they exist — order doesn't matter here
        if (_negativeModifier != null)
        {
            _Owner.Stats.RemoveModifier(_negativeModifier);
            _negativeModifier = null;
        }

        if (_positiveModifier != null)
        {
            _Owner.Stats.RemoveModifier(_positiveModifier);
            _positiveModifier = null;
        }
    }


    // Convenience builder — both modifiers are always a single flat effect
    // on one stat, so this avoids repeating the same boilerplate twice.
    private Sc_Modifier BuildModifier(string name, StatType targetStat, float value)
    {
        return new Sc_Modifier(
            name,
            ModifierSource.Augment,
            new List<Sc_StatEffect>
            {
                new Sc_StatEffect(targetStat, value, StatModType.Flat)
            }
        );
    }
}