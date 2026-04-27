// Augment_HeartOfTheForest.cs
// -15% ATK/AP, +1000 Max HP
// Gain HP Regen equal to 5% of current Max HP (dynamic, mutation-based)

using System.Collections.Generic;

public class Augment_HeartOfTheForest : Sc_AugmentBase
{
    private Sc_Modifier _regenModifier;
    private Sc_StatEffect _regenEffect;

    public Augment_HeartOfTheForest(SO_Augment data, Mb_CharacterBase owner)
        : base(data, owner) { }


    public override void OnEquip(Mb_CharacterBase owner)
    {
        // Apply static effects from SO:
        // -15% ATK, -15% AP, +1000 Max HP
        base.OnEquip(owner);

        // Create ONE persistent regen effect (starts at 0, will update immediately)
        _regenEffect = new Sc_StatEffect(
            StatType.HealthRegen,
            0f,
            StatModType.Flat
        );

        _regenModifier = new Sc_Modifier(
            "Heart of the Forest — Regen",
            ModifierSource.Augment,
            new List<Sc_StatEffect> { _regenEffect }
        );

        owner.Stats.AddModifier(_regenModifier);

        // Initial calculation
        UpdateRegen();

        // Listen for stat changes (Max HP changes will affect regen)
        owner.Stats.OnStatsChanged += HandleStatsChanged;
    }


    public override void OnUnequip(Mb_CharacterBase owner)
    {
        owner.Stats.OnStatsChanged -= HandleStatsChanged;

        if (_regenModifier != null)
        {
            owner.Stats.RemoveModifier(_regenModifier);
            _regenModifier = null;
            _regenEffect = null;
        }

        base.OnUnequip(owner);
    }


    private void HandleStatsChanged()
    {
        // No recursion risk now — we are NOT adding/removing modifiers
        UpdateRegen();
    }


    private void UpdateRegen()
    {
        float maxHP = _Owner.Stats.MaxHealth.GetValue();
        float regenAmount = maxHP * 0.05f;

        _regenEffect.Value = regenAmount;

        // Notify the stat block that the effect value has changed so it can recalculate the final regen stat
        _Owner.Stats.NotifyStatsChanged();
    }
}