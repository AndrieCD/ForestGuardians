// SO_ProjectileData.cs
// ScriptableObject that defines all configuration for one projectile type.
// Create one asset per projectile type — ability scripts assign the correct
// asset before firing so Mb_Projectile never needs character-specific code.
//
// HOW TO USE:
//   1. Right-click in the Project window → Create → Forest Guardians → Projectile Data
//   2. Fill in the fields for your projectile type
//   3. Assign the HitEffects list by dragging in Sc_HitEffect assets
//   4. Pass this asset to Mb_ProjectileLauncher.Fire(data, owner)
//
// TODO: Create one SO_ProjectileData asset for each of the following:
//   - Rajah_Feather_Basic       (Rajah Secondary — single feather, no effects)
//   - Rajah_Feather_Spread      (Rajah E — spread shot, same damage, no extra effects)
//   - Mari_PsychicLeaf          (Mari basic — light AP-scaling, no effects)
//   - Mari_PsyshockBolt         (Mari ability — heavy AP-scaling, slow on hit)
//   - Hunter_Bolt               (Hunter CuBot — straight bolt, knockback on hit)
//   - Trapper_TrapProjectile    (Trapper CuBot — slow + stun on hit)

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New ProjectileData", menuName = "ForestGuardians/Projectile Data")]
public class SO_ProjectileData : ScriptableObject
{
    [Header("Movement")]
    [Tooltip("How fast the projectile travels in world units per second.")]
    public float LaunchSpeed = 20f;

    [Tooltip("The projectile deactivates after travelling this far from its spawn point.")]
    public float MaxRange = 40f;

    [Header("Piercing")]
    [Tooltip("If true, the projectile passes through multiple targets instead of stopping on first hit.")]
    public bool IsPiercing = false;

    // Only read if IsPiercing is true — ignored for non-piercing projectiles.
    // TODO: Default of 3 is a safe starting point; tune per ability once damage numbers are final.
    [Tooltip("Maximum number of targets a piercing projectile can hit before deactivating. " +
             "Ignored if IsPiercing is false.")]
    public int MaxPierceTargets = 3;

    [Header("On-Hit Effects")]
    [Tooltip("List of effects applied to every valid target this projectile hits. " +
             "Add Sc_HitEffect assets here — one asset per effect type. " +
             "Order does not matter; all effects are applied on the same hit.")]
    public List<Sc_HitEffect> HitEffects = new List<Sc_HitEffect>();
}