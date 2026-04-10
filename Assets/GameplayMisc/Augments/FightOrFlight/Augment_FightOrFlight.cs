// Augment_FightOrFlight.cs
// Below 40% HP: +AP and +ATK (amounts scale with how close to death the player is).
// Below 20% HP: additional +HST, +AS, and +MS on top of the above.
//
// HOW IT WORKS:
//   - We subscribe to Mb_HealthComponent.OnHealthChanged when equipped.
//   - Every time HP changes, we check which tier the player is in.
//   - If the tier changed, we remove the old modifier and apply a new one.
//   - This means the modifier in StatBlock always reflects the current tier.
//
// NOTE: HST (Haste) does not currently exist as a StatType.
// The HST buff in the below-20% tier is marked TODO until the stat is added.

using System.Collections.Generic;

public class Augment_FightOrFlight : Sc_AugmentBase
{
    // Which tier is currently active: 0 = none, 1 = below 40%, 2 = below 20%
    private int _currentTier = 0;

    // We track the modifier we applied so we can swap it when the tier changes
    private Sc_Modifier _tierModifier;

    public Augment_FightOrFlight(SO_Augment data, Mb_CharacterBase owner)
        : base(data, owner) { }


    public override void OnEquip(Mb_CharacterBase owner)
    {
        // This augment has no static SO effects — don't call base.OnEquip().
        // All effects are applied dynamically based on HP thresholds.

        // Subscribe to HP changes so we react every time the player takes damage or heals
        owner.Health.OnHealthChanged += HandleHealthChanged;
    }


    public override void OnUnequip(Mb_CharacterBase owner)
    {
        // Unsubscribe first — we don't want ghost listeners after stage end
        owner.Health.OnHealthChanged -= HandleHealthChanged;

        // Remove whichever tier modifier is currently active (if any)
        if (_tierModifier != null)
        {
            owner.Stats.RemoveModifier(_tierModifier);
            _tierModifier = null;
        }

        _currentTier = 0;
    }


    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        float hpPercent = currentHealth / maxHealth;

        // Determine which tier the player is currently in
        int newTier = 0;
        if (hpPercent < 0.20f) newTier = 2;
        else if (hpPercent < 0.40f) newTier = 1;

        // Only swap modifiers if the tier actually changed — avoids unnecessary StatBlock churn
        if (newTier == _currentTier) return;

        // Remove the old tier's modifier before applying the new one
        if (_tierModifier != null)
        {
            _Owner.Stats.RemoveModifier(_tierModifier);
            _tierModifier = null;
        }

        _currentTier = newTier;

        // Apply the correct tier's modifier
        if (_currentTier == 1)
            _tierModifier = BuildTier1Modifier();
        else if (_currentTier == 2)
            _tierModifier = BuildTier2Modifier();

        if (_tierModifier != null)
            _Owner.Stats.AddModifier(_tierModifier);
    }


    // Below 40% HP: +50 AP (flat), +50 ATK (flat)
    // TODO: Scale these values with difficulty or guardian level when that system exists
    private Sc_Modifier BuildTier1Modifier()
    {
        return new Sc_Modifier(
            "Fight or Flight — Tier 1",
            ModifierSource.Augment,
            new List<Sc_StatEffect>
            {
                new Sc_StatEffect(StatType.AbilityPower,  50f, StatModType.Flat),
                new Sc_StatEffect(StatType.AttackPower,   50f, StatModType.Flat),
            }
        );
    }


    // Below 20% HP: +200 AP (flat), +200 ATK (flat), +50% AS, +50% MS
    // TODO: Add HST (+50 flat) once StatType.Haste is added to the enum
    private Sc_Modifier BuildTier2Modifier()
    {
        return new Sc_Modifier(
            "Fight or Flight — Tier 2",
            ModifierSource.Augment,
            new List<Sc_StatEffect>
            {
                new Sc_StatEffect(StatType.AbilityPower,  200f, StatModType.Flat),
                new Sc_StatEffect(StatType.AttackPower,   200f, StatModType.Flat),
                new Sc_StatEffect(StatType.AttackSpeed,   0.50f, StatModType.Percent),
                new Sc_StatEffect(StatType.MoveSpeed,     0.50f, StatModType.Percent),
                // TODO: new Sc_StatEffect(StatType.Haste, 50f, StatModType.Flat),
            }
        );
    }
}