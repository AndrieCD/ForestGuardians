// Sc_HitEffect_Damage.cs
// Applies bonus damage to the hit target on top of the projectile's base damage.
//
// WHY THIS EXISTS:
//   The base damage on a projectile is set at fire time by the ability script
//   (e.g. ATK * 0.8). But some abilities — particularly Mari's AP-scaling shots —
//   need the projectile itself to carry a damage formula so the correct stats are
//   read at the moment of impact, not at fire time. This effect handles that case.
//
//   It also lets ability designers layer additional flat or scaled damage onto any
//   projectile type without writing a new projectile class.
//
// TODO: Used by:
//   - Mari_PsyshockBolt  (heavy AP scaling — set APScaling to 0.9, ATKScaling to 0.0)
//   - Mari_PsychicLeaf   (light AP scaling — set APScaling to 0.4, ATKScaling to 0.2)
//   Keep BonusDamage at 0 unless you want a flat damage addition on top of scaling.
//
// Inspector setup:
//   BonusDamage  — flat damage added regardless of stats (default 0)
//   ATKScaling   — fraction of attacker's AttackPower added as damage (default 0)
//   APScaling    — fraction of attacker's AbilityPower added as damage (default 0)
//   Example: ATKScaling = 0.5 means +50% of ATK as bonus damage

using UnityEngine;

[CreateAssetMenu(fileName = "New HitEffect_Damage",
    menuName = "ForestGuardians/Hit Effects/Damage")]
public class Sc_HitEffect_Damage : Sc_HitEffect
{
    [Header("Damage Formula")]
    [Tooltip("Flat bonus damage applied on hit, independent of any stat scaling.")]
    public float BonusDamage = 0f;

    [Tooltip("Fraction of the attacker's AttackPower added as bonus damage. " +
             "Example: 0.5 = +50% of ATK.")]
    public float ATKScaling = 0f;

    [Tooltip("Fraction of the attacker's AbilityPower added as bonus damage. " +
             "Example: 0.9 = +90% of AP. Use this for AP-scaling abilities like Mari's shots.")]
    public float APScaling = 0f;


    public override void ApplyOnHit(Mb_CharacterBase target, Mb_CharacterBase attacker,
                                    Vector3 hitPoint, Vector3 hitNormal)
    {
        // Guard: target must be damageable and alive
        I_Damageable damageable = target.GetComponent<I_Damageable>();
        if (damageable == null) return;

        float damage = BonusDamage;

        // Add stat-scaled portions only if the attacker has a valid StatBlock.
        // Attacker can be null (e.g. environmental hazard) — guard prevents a crash.
        if (attacker != null && attacker.Stats != null)
        {
            damage += attacker.Stats.AttackPower.GetValue() * ATKScaling;
            damage += attacker.Stats.AbilityPower.GetValue() * APScaling;
        }

        if (damage <= 0f) return; // Nothing to apply — skip the TakeDamage call entirely

        damageable.TakeDamage(damage);
    }
}