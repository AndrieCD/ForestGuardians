// Sc_HitEffect_Slow.cs
// Applies a timed movement slow to the hit target via Mb_StatusEffectController.
//
// Inspector setup:
//   SlowDuration - how long the slow lasts in seconds.
//   SlowPercent  - how much movement speed is reduced (0-100). Example: 30 = 30% slow.

using UnityEngine;

[CreateAssetMenu(fileName = "New HitEffect_Slow",
    menuName = "ForestGuardians/Hit Effects/Slow")]
public class Sc_HitEffect_Slow : Sc_HitEffect
{
    [Header("Slow Settings")]
    [Tooltip("How long the slow lasts in seconds.")]
    public float SlowDuration = 2f;

    [Tooltip("Percentage of movement speed removed. 30 = 30% slower. Clamped 0-100.")]
    [Range(0f, 100f)]
    public float SlowPercent = 30f;

    public override void ApplyOnHit(Mb_CharacterBase target, Mb_CharacterBase attacker,
                                    Vector3 hitPoint, Vector3 hitNormal)
    {
        if (target == null) return;

        Mb_StatusEffectController statusController =
            target.GetComponent<Mb_StatusEffectController>();

        if (statusController == null) return;

        float slowFraction = Mathf.Clamp(SlowPercent, 0f, 100f) / 100f;
        statusController.Apply(Sc_StatusEffect.MoveSlow(SlowDuration, slowFraction));
    }
}
