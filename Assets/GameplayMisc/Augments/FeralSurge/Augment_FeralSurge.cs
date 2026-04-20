// Augment_FeralSurge.cs
// Gain 50%–200% Attack Power scaling with level, but lose 35% Max HP.

using System.Collections.Generic;

public class Augment_FeralSurge : Sc_AugmentBase
{
    private Sc_Modifier _apModifier;

    public Augment_FeralSurge(SO_Augment data, Mb_CharacterBase owner)
        : base(data, owner) { }


    public override void OnEquip(Mb_CharacterBase owner)
    {
        // Apply -35% Max HP from SO
        base.OnEquip(owner);

        RebuildModifier();
    }


    public override void OnUnequip(Mb_CharacterBase owner)
    {
        if (_apModifier != null)
        {
            owner.Stats.RemoveModifier(_apModifier);
            _apModifier = null;
        }

        base.OnUnequip(owner);
    }


    public override void OnWaveEnd()
    {
        // If level increases per wave, update scaling
        RebuildModifier();
    }


    private void RebuildModifier()
    {
        if (_apModifier != null)
        {
            _Owner.Stats.RemoveModifier(_apModifier);
        }

        int level = _Owner.GetLevel();

        int maxLevel = _Owner.GetMaxLevel(); 
        float t = UnityEngine.Mathf.Clamp01((level - 1f) / (maxLevel - 1f));

        float scaledBonus = UnityEngine.Mathf.Lerp(0.5f, 2.0f, t);

        _apModifier = new Sc_Modifier(
            "Feral Surge — Scaling AP",
            ModifierSource.Augment,
            new List<Sc_StatEffect>
            {
                new Sc_StatEffect(StatType.AttackPower, scaledBonus, StatModType.Percent)
            }
        );

        _Owner.Stats.AddModifier(_apModifier);
    }
}