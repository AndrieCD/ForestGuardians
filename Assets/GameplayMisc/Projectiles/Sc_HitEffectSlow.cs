// Sc_HitEffect_Slow.cs
// Applies a movement slow to the hit target via Mb_StatusEffectController.
//
// HOW IT WORKS:
//   Calls Mb_StatusEffectController.Apply() with a MoveSlow status effect.
//   The controller owns the duration timer and removal — this class just triggers it.
//   If the target has no Mb_StatusEffectController (e.g. the Guardian is hit by a
//   CuBot slow projectile and status effects aren't implemented on Guardians yet),
//   the effect is silently skipped.
//
// TODO: Used by:
//   - Mari_PsyshockBolt      (moderate slow — SlowPercent ~30, SlowDuration ~2.0)
//   - Trapper_TrapProjectile (heavy slow — SlowPercent ~60, SlowDuration ~3.0)
//   Tune percentages once CuBot movement speeds are finalized.
//
// TODO: Mb_StatusEffectController is currently commented out (stub only).
//   When it is implemented, replace the placeholder Apply() call below with
//   the real API. The field names here (SlowDuration, SlowPercent) are chosen
//   to match what Sc_StatusEffect.MoveSlow() is expected to receive.
//
// Inspector setup:
//   SlowDuration — how long the slow lasts in seconds
//   SlowPercent  — how much movement speed is reduced (0–100). Example: 30 = 30% slow.

using UnityEngine;

[CreateAssetMenu(fileName = "New HitEffect_Slow",
    menuName = "ForestGuardians/Hit Effects/Slow")]
public class Sc_HitEffect_Slow : Sc_HitEffect
{
    [Header("Slow Settings")]
    [Tooltip("How long the slow lasts in seconds.")]
    public float SlowDuration = 2f;

    [Tooltip("Percentage of movement speed removed. 30 = 30% slower. Clamped 0–100.")]
    [Range(0f, 100f)]
    public float SlowPercent = 30f;


    public override void ApplyOnHit(Mb_CharacterBase target, Mb_CharacterBase attacker,
                                    Vector3 hitPoint, Vector3 hitNormal)
    {
        if (target == null) return;

        Mb_StatusEffectController statusController =
            target.GetComponent<Mb_StatusEffectController>();

        // Silently skip if the target doesn't support status effects.
        // This is intentional — not every character type needs a status controller.
        if (statusController == null) return;

        // TODO: Mb_StatusEffectController.Apply() signature is not yet finalized.
        //   Replace this call with the real implementation once the controller is built.
        //   Expected call pattern:
        //   statusController.Apply(Sc_StatusEffect.MoveSlow(SlowPercent, SlowDuration));
        //
        //   For now, apply the slow directly as a timed stat modifier so the system
        //   is functional even before Mb_StatusEffectController is fully implemented.
        ApplySlowModifierFallback(target);
    }


    // Fallback slow implementation using the established Mb_StatBlock modifier system.
    // Applies a percentage reduction to MoveSpeed as a timed Sc_Modifier.
    // Remove this method and replace the ApplyOnHit call above once
    // Mb_StatusEffectController is implemented.
    private void ApplySlowModifierFallback(Mb_CharacterBase target)
    {
        if (target.Stats == null) return;

        // A negative percent modifier reduces the stat by SlowPercent%.
        // StatModType.PercentAdd means: finalValue = baseValue * (1 + sum of percent modifiers)
        // So -30 reduces movement speed by 30% of base.
        Sc_StatEffect slowEffect = new Sc_StatEffect(
            StatType.MoveSpeed,
            -SlowPercent,
            StatModType.Percent
        );

        Sc_Modifier slowModifier = new Sc_Modifier(
            "Slow",
            ModifierSource.StatusEffect,
            new System.Collections.Generic.List<Sc_StatEffect> { slowEffect },
            SlowDuration
        );

        target.Stats.AddModifier(slowModifier);
    }
}