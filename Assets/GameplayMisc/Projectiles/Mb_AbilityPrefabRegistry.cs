// Mb_AbilityPrefabRegistry.cs
// A MonoBehaviour on the Guardian GameObject that holds all prefab references
// needed by ability scripts at runtime.
//
// WHY THIS EXISTS:
//   Ability scripts (Sc_BaseAbility subclasses) are plain C# classes — they
//   cannot have [SerializeField] fields and cannot be assigned prefabs in the
//   Inspector. This component is the bridge: it lives on the Guardian GameObject
//   as a MonoBehaviour, holds all prefab references via the Inspector, and
//   exposes them via a simple lookup API that abilities call in OnEquip().
//
// HOW ABILITIES USE IT:
//   In OnEquip():
//     Mb_AbilityPrefabRegistry registry = user.GetComponent<Mb_AbilityPrefabRegistry>();
//     _myPrefab = registry?.GetPrefab(AbilityPrefabID.Mari_MindspikesZone);
//
// HOW TO ADD A NEW PREFAB:
//   1. Add a value to the AbilityPrefabID enum below.
//   2. Add a matching [SerializeField] entry field to the Inspector section.
//   3. Assign the prefab in the Inspector on the Guardian prefab.
//   4. Add a case to GetPrefab() returning that field.
//
// INSPECTOR SETUP:
//   Add this component to every Guardian prefab that uses prefab-spawning abilities.
//   Assign all prefab fields. Leave unused entries null — abilities null-check on fetch.

using UnityEngine;

/// <summary>
/// Identifies each prefab slot in the registry.
/// Add a new value here for every new spawnable prefab a Guardian ability needs.
/// </summary>
public enum AbilityPrefabID
{
    // ── SHARED ────────────────────────────────────────────────────────────────
    // Generic projectile prefab — used by any Guardian whose abilities
    // fire standard projectiles via Mb_ProjectileLauncher.
    // For Guardians with multiple projectile shapes, add specific IDs below.
    Generic_Projectile,

    // ── MARI ──────────────────────────────────────────────────────────────────
    Mari_PsychicLeafProjectile,     // Primary burst projectile (small fast leaf)
    Mari_PsyshockBoltProjectile,    // Secondary single bolt (heavier psychic bolt)
    Mari_MindspikesZone,            // Q rectangular spike field zone GO
    Mari_PsychicSlamShockwave,      // E wide trigger shockwave projectile GO
    Mari_BloomZone,                 // R1 spherical bloom field zone GO
    Mari_LaserBeam,                 // R2 laser beam GO (LineRenderer + hit VFX)
    Mari_DotHitVFX,                 // R1 passive DoT tick VFX prefab

    // ── RAJAH ─────────────────────────────────────────────────────────────────
    // Rajah uses a single generic projectile prefab — no additional entries needed
    Rajah_FeatherProjectile,        // Rajah's feather shot projectile
    Rajah_SkyRendShield,

    // ── FUTURE GUARDIANS ──────────────────────────────────────────────────────
    // Add entries here as new guardians and abilities are designed.


    // CuBots (eg. Hunter, Trapper, Toxion, Luxion) may also need new entries for their unique projectiles and effects.
    Hunter_BulletProjectile,           // Hunter's basic bolt projectile
    Trapper_TrapProjectile,           // Trapper's trap projectile
    Trapper_ProximityTrap,            // Trapper's ground proximity trap
    Toxion_SludgeProjectile,     // Toxion's poison cloud projectile
    Luxion_BulletProjectile            // Luxion's laser projectile
}


public class Mb_AbilityPrefabRegistry : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    [Header("Shared")]
    [Tooltip("Generic projectile prefab. Must have Mb_Projectile, Rigidbody, Collider.")]
    [SerializeField] private GameObject _GenericProjectile;

    [Header("Mari — Projectiles")]
    [Tooltip("Mari's primary burst projectile — small psychic leaf. " +
             "Must have Mb_Projectile, Rigidbody, trigger Collider.")]
    [SerializeField] private GameObject _MariPsychicLeafProjectile;

    [Tooltip("Mari's secondary bolt — heavier psychic spark. " +
             "Must have Mb_Projectile, Rigidbody, trigger Collider.")]
    [SerializeField] private GameObject _MariPsyshockBoltProjectile;

    [Header("Mari — Zone / World GOs")]
    [Tooltip("Mari Q spike field zone. Must have BoxCollider (trigger), " +
             "Mb_MindspikesZone, child 'MindspikesFieldVFX' ParticleSystem, " +
             "child 'SpikeVisualRoot' mesh GO.")]
    [SerializeField] private GameObject _MariMindspikesZone;

    [Tooltip("Mari E shockwave. Must have BoxCollider (trigger), Rigidbody (kinematic), " +
             "Mb_PsychicSlamProjectile, child 'ShockwaveVFX' ParticleSystem, " +
             "child 'ShockwaveVisual' mesh GO.")]
    [SerializeField] private GameObject _MariPsychicSlamShockwave;

    [Tooltip("Mari R1 bloom field zone. Must have SphereCollider (trigger), " +
             "Rigidbody (kinematic), Mb_BloomZone, child 'BloomFieldVFX' ParticleSystem, " +
             "child 'BloomVisualRoot' mesh GO.")]
    [SerializeField] private GameObject _MariBloomZone;

    [Tooltip("Mari R2 laser beam GO. Must have LineRenderer, Mb_LaserBeam, " +
             "child 'LaserHitVFX' ParticleSystem.")]
    [SerializeField] private GameObject _MariLaserBeam;

    [Tooltip("Mari R1 passive DoT tick VFX — short burst spawned on each DoT tick. " +
             "Simple particle system prefab, auto-destroyed after 2 seconds.")]
    [SerializeField] private GameObject _MariDotHitVFX;


    // Rajah
    [Header("Rajah")]
    [Tooltip("Rajah's feather shot projectile. Must have Mb_Projectile, Rigidbody, Collider.")]
    [SerializeField] private GameObject _RajahFeatherProjectile;
    [Tooltip("Rajah's Sky Rend shield.")]
    [SerializeField] private GameObject _RajahSkyRendShield;

    // CuBots
    [Header("CuBots")]
    [Tooltip("Hunter's basic bolt projectile. Must have Mb_Projectile, Rigidbody, Collider.")]
    [SerializeField] private GameObject _HunterBulletProjectile;

    [Tooltip("Trapper's trap projectile. Must have Mb_Projectile, Rigidbody, Collider.")]
    [SerializeField] private GameObject _TrapperTrapProjectile;

    [Tooltip("Trapper's ground proximity trap. Must have Mb_TrapperProximityTrap and a trigger Collider.")]
    [SerializeField] private GameObject _TrapperProximityTrap;

    [Tooltip("Toxion's poison cloud projectile. Must have Mb_Projectile, Rigidbody, Collider.")]
    [SerializeField] private GameObject _ToxionSludgeProjectile;

    [Tooltip("Luxion's laser projectile. Must have Mb_Projectile, Rigidbody, Collider.")]
    [SerializeField] private GameObject _LuxionBulletProjectile;


    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the prefab for the given ability prefab ID.
    /// Returns null if the field is unassigned — callers should null-check
    /// and log a meaningful error if the prefab is required.
    /// </summary>
    public GameObject GetPrefab(AbilityPrefabID id)
    {
        return id switch
        {
            AbilityPrefabID.Generic_Projectile => _GenericProjectile,

            AbilityPrefabID.Mari_PsychicLeafProjectile => _MariPsychicLeafProjectile,
            AbilityPrefabID.Mari_PsyshockBoltProjectile => _MariPsyshockBoltProjectile,
            AbilityPrefabID.Mari_MindspikesZone => _MariMindspikesZone,
            AbilityPrefabID.Mari_PsychicSlamShockwave => _MariPsychicSlamShockwave,
            AbilityPrefabID.Mari_BloomZone => _MariBloomZone,
            AbilityPrefabID.Mari_LaserBeam => _MariLaserBeam,
            AbilityPrefabID.Mari_DotHitVFX => _MariDotHitVFX,

            AbilityPrefabID.Rajah_FeatherProjectile => _RajahFeatherProjectile, 
            AbilityPrefabID.Rajah_SkyRendShield => _RajahSkyRendShield,

            AbilityPrefabID.Hunter_BulletProjectile => _HunterBulletProjectile,
            AbilityPrefabID.Trapper_TrapProjectile => _TrapperTrapProjectile,
            AbilityPrefabID.Trapper_ProximityTrap => _TrapperProximityTrap,
            AbilityPrefabID.Toxion_SludgeProjectile => _ToxionSludgeProjectile,
            AbilityPrefabID.Luxion_BulletProjectile => _LuxionBulletProjectile,


            _ => null
        };
    }
}
