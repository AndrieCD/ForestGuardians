// Augment_HeartOfTheForest.cs
// -15% ATK/AP, +1000 Max HP
// Gain HP Regen equal to 5% of current Max HP (dynamic).

using System.Collections.Generic;

public class Augment_HeartOfTheForest : Sc_AugmentBase
{
    private Sc_Modifier _regenModifier;

    public Augment_HeartOfTheForest(SO_Augment data, Mb_CharacterBase owner)
        : base(data, owner) { }


    public override void OnEquip(Mb_CharacterBase owner)
    {
        // Apply static penalties + Max HP
        base.OnEquip(owner);

        // Build initial regen
        RebuildRegenModifier();

        // Subscribe to stat changes if your system supports it
        owner.Stats.OnStatsChanged += HandleStatsChanged;
    }


    public override void OnUnequip(Mb_CharacterBase owner)
    {
        owner.Stats.OnStatsChanged -= HandleStatsChanged;

        if (_regenModifier != null)
        {
            owner.Stats.RemoveModifier(_regenModifier);
            _regenModifier = null;
        }

        base.OnUnequip(owner);
    }


    private void HandleStatsChanged()
    {
        // If Max HP changes, recalculate regen
        RebuildRegenModifier();
    }


    private void RebuildRegenModifier()
    {
        // Remove old regen
        if (_regenModifier != null)
        {
            _Owner.Stats.RemoveModifier(_regenModifier);
        }

        float maxHP = _Owner.Stats.MaxHealth.GetValue();
        float regenAmount = maxHP * 0.05f;

        _regenModifier = new Sc_Modifier(
            "Heart of the Forest — Regen",
            ModifierSource.Augment,
            new List<Sc_StatEffect>
            {
                new Sc_StatEffect(StatType.HealthRegen, regenAmount, StatModType.Flat)
            }
        );

        _Owner.Stats.AddModifier(_regenModifier);
    }
}