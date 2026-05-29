// E_VFXType.cs
// Defines every VFX type in Forest Guardians as a single enum.
// All external code references VFX by this enum — never by string or direct prefab reference.
//
// HOW TO ADD A NEW VFX:
//   1. Add a new value here in the appropriate category.
//   2. Add a matching entry in your SO_VFXLibrary asset in the Inspector.
//   3. That's it. Nothing else needs to change.
//
// CATEGORIES (for readability — no runtime significance):
//   Ability_*    — cast or swing effects on the Guardian
//   Hit_*        — impact effects on characters or the environment
//   Status_*     — looping particles while a status effect is active
//   Footstep_*   — ground contact effects triggered by animation events
//   Env_*        — environment-driven one-shot events

public enum VFXType
{
    // ── Ability VFX ───────────────────────────────────────────────
    // Spawned at the ability's origin point when the ability fires.
    Ability_Q,          // Q cast effect (e.g. Sky Rend dash trail)
    Ability_E,          // E cast effect (e.g. Feather Barrage muzzle burst)
    Ability_R,          // R cast effect (e.g. Sovereign's Wrath or Eagle Eye activation)
    Ability_Primary,    // Primary attack swing VFX (e.g. Feathery Slash arc)
    Ability_Secondary,  // Secondary attack projectile trail or muzzle flash

    // ── Hit VFX ───────────────────────────────────────────────────
    // Spawned at the point of impact or at the character's position.
    Hit_Generic,        // Small spark or flash — plays on any character taking damage
    Hit_Critical,       // Larger, more dramatic burst — plays on a critical strike
    Hit_Guardian,       // Hit flash specific to the Guardian taking damage
    Hit_Panoharra,      // Impact effect on the Panoharra Tree
    Hit_CuBot_Death,    // Death explosion / dissolve on a CuBot being destroyed

    // ── Status VFX ────────────────────────────────────────────────
    // Looping effects attached to the affected character.
    // Spawned when the status starts; stopped when it ends.
    Status_Burn,        // Fire / ember particles (Burn DoT)
    Status_Poison,      // Toxic green particles (Poison DoT)
    Status_Slow,        // Frost or slowing field particles
    Status_Stun,        // Stars or daze particles orbiting the head

    // ── Footstep VFX ──────────────────────────────────────────────
    // Spawned at foot contact point via animation events.
    Footstep_Grass,     // Leaf / dust puff on grass terrain
    Footstep_Water,     // Water ripple splash
    Footstep_Mud,       // Mud splatter
    Footstep_Stone,     // Dust kick on stone or hard surfaces

    // ── Environment VFX ───────────────────────────────────────────
    // Gameplay-event-driven one-shots in the world.
    Env_Portal_Enter,       // Rafflesia teleport entry burst
    Env_Portal_Exit,        // Rafflesia teleport exit burst
    Env_Wave_Start,         // Ambient energy pulse at wave start
    Env_CuBot_Spawn,        // Materialise effect on CuBot activation
    Env_Guardian_Death,     // Large burst on Guardian death
    Env_Panoharra_Death,    // Catastrophic burst on Panoharra destruction
    Env_Levelup,            // Level-up flourish on the Guardian
}