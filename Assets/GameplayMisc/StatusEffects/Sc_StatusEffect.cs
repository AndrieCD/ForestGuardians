// Sc_StatusEffect.cs
// Data class representing one status effect instance — everything the controller
// needs to apply, tick, and clean up a single effect on a character.
//
// IMPORTANT — MODIFIER DURATION RULE:
//   The Sc_Modifier stored here must ALWAYS use float.PositiveInfinity as its duration.
//   Mb_StatBlock.AddModifier() starts its own timed-removal coroutine for non-infinite
//   modifiers — if you pass a timed duration, StatBlock removes the modifier on its own
//   schedule, racing against Mb_StatusEffectController's coroutine.
//   The controller owns the timer. StatBlock just holds the modifier.
//
// USAGE — building a custom effect inline:
//   var slow = new Sc_StatusEffect(
//       StatusType.MoveSlow,
//       duration: 3f,
//       tickInterval: 0f,
//       tickDamage: 0f,
//       statModifier: new Sc_Modifier(
//           "Slow",
//           ModifierSource.StatusEffect,
//           new List<Sc_StatEffect>
//           {
//               new Sc_StatEffect(StatType.MoveSpeed, -0.30f, StatModType.Percent)
//           }
//       )
//   );
//
// USAGE — using a static factory (preferred for common effects):
//   var slow  = Sc_StatusEffect.MoveSlow(duration: 3f, moveSpeedReduction: 0.30f);
//   var burn  = Sc_StatusEffect.Burn(duration: 5f, damagePerTick: 20f);
//   var stun  = Sc_StatusEffect.Stun(duration: 1.5f);

using System.Collections.Generic;
using UnityEngine;

public class Sc_StatusEffect
{
    // -------------------------------------------------------------------------
    // Fields
    // -------------------------------------------------------------------------

    // Which status type this is — used as the dictionary key in the controller.
    // Only one effect of each StatusType can be active at a time on a character.
    public readonly StatusType Type;

    // How long this effect lasts in seconds.
    // The controller's coroutine counts this down and removes the effect when it hits zero.
    public readonly float Duration;

    // How often damage ticks fire, in seconds. Zero means no DoT (e.g. a pure slow).
    // Example: 0.5f = damage fires every half second.
    public readonly float TickInterval;

    // How much damage each tick deals. Zero if this is not a damage-over-time effect.
    // Applied via Mb_HealthComponent.TakeDamage() — never touches health directly.
    public readonly float TickDamage;

    // The stat modifier applied when this effect starts and removed when it ends.
    // Null for pure DoT effects (Poison, Burn) that have no stat changes.
    //
    // MUST use float.PositiveInfinity as its duration — see file header comment.
    public readonly Sc_Modifier StatModifier;


    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a status effect instance. Prefer the static factory methods below
    /// for common effects — they enforce correct default values automatically.
    /// </summary>
    /// <param name="type">Which status this is.</param>
    /// <param name="duration">How long it lasts in seconds.</param>
    /// <param name="tickInterval">Seconds between DoT ticks. Pass 0 for no DoT.</param>
    /// <param name="tickDamage">Damage per tick. Pass 0 for no damage.</param>
    /// <param name="statModifier">Stat modifier to apply on start, remove on end.
    /// Pass null if this effect has no stat change. Must use infinite duration.</param>
    public Sc_StatusEffect(
        StatusType type,
        float duration,
        float tickInterval = 0f,
        float tickDamage = 0f,
        Sc_Modifier statModifier = null)
    {
        Type = type;
        Duration = duration;
        TickInterval = tickInterval;
        TickDamage = tickDamage;
        StatModifier = statModifier;
    }


    // -------------------------------------------------------------------------
    // Static Factories — use these in ability scripts for common effects
    // -------------------------------------------------------------------------

    /// <summary>
    /// A flat Move Speed reduction with no damage component.
    /// The modifier is built here so the caller only needs to pass gameplay values.
    /// </summary>
    /// <param name="duration">How long the slow lasts.</param>
    /// <param name="moveSpeedReduction">Fraction or percentage to reduce MS by (0.30 or 30 = 30% slower).</param>
    public static Sc_StatusEffect MoveSlow(float duration, float moveSpeedReduction)
    {
        float normalizedReduction = NormalizeSlowReduction(moveSpeedReduction);

        // Percent modifier with a negative value reduces the stat.
        // Infinite duration on the modifier — the controller removes it when the effect expires.
        var modifier = new Sc_Modifier(
            "Slow",
            ModifierSource.StatusEffect,
            new List<Sc_StatEffect>
            {
                new Sc_StatEffect(StatType.MoveSpeed, -normalizedReduction, StatModType.Percent)
            }
        );

        return new Sc_StatusEffect(
            StatusType.MoveSlow,
            duration,
            tickInterval: 0f,
            tickDamage: 0f,
            statModifier: modifier
        );
    }

    /// <summary>
    /// A flat Move Speed reduction with no damage component.
    /// The modifier is built here so the caller only needs to pass gameplay values.
    /// </summary>
    /// <param name="duration">How long the slow lasts.</param>
    /// <param name="attackSpeedReduction">Fraction or percentage to reduce AS by (0.30 or 30 = 30% slower).</param>
    public static Sc_StatusEffect AttackSlow(float duration, float attackSpeedReduction)
    {
        float normalizedReduction = NormalizeSlowReduction(attackSpeedReduction);

        // Percent modifier with a negative value reduces the stat.
        // Infinite duration on the modifier — the controller removes it when the effect expires.
        var modifier = new Sc_Modifier(
            "Slow",
            ModifierSource.StatusEffect,
            new List<Sc_StatEffect>
            {
                new Sc_StatEffect(StatType.AttackSpeed, -normalizedReduction, StatModType.Percent)
            }
        );

        return new Sc_StatusEffect(
            StatusType.AttackSlow,
            duration,
            tickInterval: 0f,
            tickDamage: 0f,
            statModifier: modifier
        );
    }


    /// <summary>
    /// Deals damage every tickInterval seconds for the full duration.
    /// No stat modifier — pure damage over time.
    /// </summary>
    /// <param name="duration">Total effect duration.</param>
    /// <param name="damagePerTick">Damage dealt each tick.</param>
    /// <param name="tickInterval">Seconds between ticks. Default 1s.</param>
    public static Sc_StatusEffect Poison(
        float duration,
        float damagePerTick,
        float tickInterval = 1f)
    {
        // Poison has no stat modifier — it only deals damage over time.
        return new Sc_StatusEffect(
            StatusType.Poison,
            duration,
            tickInterval: tickInterval,
            tickDamage: damagePerTick,
            statModifier: null
        );
    }


    /// <summary>
    /// Deals damage every tickInterval seconds. Faster tick rate than Poison by default.
    /// No stat modifier — pure damage over time.
    /// </summary>
    /// <param name="duration">Total effect duration.</param>
    /// <param name="damagePerTick">Damage dealt each tick.</param>
    /// <param name="tickInterval">Seconds between ticks. Default 0.5s (faster than Poison).</param>
    public static Sc_StatusEffect Burn(
        float duration,
        float damagePerTick,
        float tickInterval = 0.5f)
    {
        return new Sc_StatusEffect(
            StatusType.Burn,
            duration,
            tickInterval: tickInterval,
            tickDamage: damagePerTick,
            statModifier: null
        );
    }


    /// <summary>
    /// Mari's Psychic Bloom passive DoT.
    /// Separate status type so it can coexist with Poison and Burn while still
    /// using the generic DoT controller path.
    /// </summary>
    /// <param name="duration">Total effect duration.</param>
    /// <param name="damagePerTick">Damage dealt each tick.</param>
    /// <param name="tickInterval">Seconds between ticks. Default 0.5s.</param>
    public static Sc_StatusEffect PsychicBloom(
        float duration,
        float damagePerTick,
        float tickInterval = 0.5f)
    {
        return new Sc_StatusEffect(
            StatusType.PsychicBloom,
            duration,
            tickInterval: tickInterval,
            tickDamage: damagePerTick,
            statModifier: null
        );
    }


    /// <summary>
    /// Blocks all movement and ability use for the duration.
    /// No DoT — pure crowd control.
    ///
    /// NOTE: On Guardians, the caller must also apply ActionDisableFlags.Stun
    /// via Mb_PlayerController.AddDisable() — this modifier alone only handles
    /// the stat side. CuBot stun behavior requires Mb_CuBotController to
    /// subscribe to Mb_StatusEffectController.OnStatusApplied and stop its NavMeshAgent.
    ///
    /// TODO: Wire ActionDisableFlags.Stun into the application site once a
    ///       stun-applying ability is implemented.
    /// </summary>
    /// <param name="duration">How long the stun lasts.</param>
    public static Sc_StatusEffect Stun(float duration)
    {
        // Stun has no stat modifier in the traditional sense — crowd control is
        // handled via ActionDisableFlags on Guardians and agent flags on CuBots.
        // We pass null here and let the application site handle the disable flags.
        return new Sc_StatusEffect(
            StatusType.Stun,
            duration,
            tickInterval: 0f,
            tickDamage: 0f,
            statModifier: null
        );
    }

    private static float NormalizeSlowReduction(float reduction)
    {
        float positiveReduction = Mathf.Abs(reduction);

        if (positiveReduction > 1f)
            positiveReduction /= 100f;

        return Mathf.Clamp01(positiveReduction);
    }
}
