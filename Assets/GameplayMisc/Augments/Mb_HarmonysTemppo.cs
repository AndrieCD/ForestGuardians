// Augment_HarmoneysTempo.cs
// Gain flat Haste and Ability Power that scale with the player's current level.
// At level 1: +20 HST, +100 AP. At max level (15): +40 HST, +200 AP.
// Values are interpolated linearly between those two points as the player levels up.
//
// Because the player levels up at wave rewards, OnWaveEnd() rebuilds the modifier
// so the bonus always matches the current level — same pattern as Augment_FeralSurge.

using System.Collections.Generic;
using UnityEngine;

public class Mb_HarmonysTempo : Sc_AugmentBase
{
    // We track this modifier ourselves so we can remove and rebuild it on level-up.
    // (The base class _appliedModifier slot is taken by base.OnEquip for SO effects,
    // but this augment has no SO effects — so we own the full modifier lifecycle here.)
    private Sc_Modifier _scalingModifier;

    // TODO: Tune these in the Inspector if you want to expose them later.
    // For now they're constants matching the design doc range.
    private const float HASTE_MIN = 20f;
    private const float HASTE_MAX = 40f;
    private const float AP_MIN = 100f;
    private const float AP_MAX = 200f;


    public Mb_HarmonysTempo(SO_Augment data, Mb_CharacterBase owner)
        : base(data, owner) { }


    public override void OnEquip(Mb_CharacterBase owner)
    {
        // This augment has no static SO effects, so we skip base.OnEquip()
        // and manage the full modifier ourselves.
        RebuildModifier();
    }


    public override void OnUnequip(Mb_CharacterBase owner)
    {
        // Clean up the scaling modifier we applied
        if (_scalingModifier != null)
        {
            owner.Stats.RemoveModifier(_scalingModifier);
            _scalingModifier = null;
        }

        // No base.OnUnequip() needed — we never called base.OnEquip(),
        // so the base class has no modifier to remove.
    }


    public override void OnWaveEnd()
    {
        // The player may have leveled up after this wave's reward, so
        // rebuild the modifier to reflect the new level's bonuses.
        RebuildModifier();
    }


    private void RebuildModifier()
    {
        // Remove the old modifier before building a new one,
        // otherwise both old and new values would stack.
        if (_scalingModifier != null)
            _Owner.Stats.RemoveModifier(_scalingModifier);

        // t = 0 at level 1, t = 1 at max level.
        // Mathf.Clamp01 keeps t in range if level ever goes out of bounds.
        int level = _Owner.GetLevel();
        int maxLevel = _Owner.GetMaxLevel();
        float t = Mathf.Clamp01((level - 1f) / (maxLevel - 1f));

        float haste = Mathf.Lerp(HASTE_MIN, HASTE_MAX, t);
        float ap = Mathf.Lerp(AP_MIN, AP_MAX, t);

        // Both bonuses travel together as one modifier so they're
        // removed as a single unit on unequip or rebuild.
        _scalingModifier = new Sc_Modifier(
            "Harmony's Tempo — Scaling",
            ModifierSource.Augment,
            new List<Sc_StatEffect>
            {
                new Sc_StatEffect(StatType.Haste,        haste, StatModType.Flat),
                new Sc_StatEffect(StatType.AbilityPower, ap,    StatModType.Flat),
            }
        );

        _Owner.Stats.AddModifier(_scalingModifier);
    }
}