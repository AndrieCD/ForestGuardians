// E_VFXType.cs


public enum VFXType
{
    // ── SHARED / GENERIC ──────────────────────────────────────────────────
    // Fallbacks used when no character-specific VFX is assigned yet.
    // Also used for effects that are genuinely character-agnostic.

    Hit_Guardian_Generic,       // Any guardian takes damage (fallback)
    Hit_CuBot_Generic,          // Any CuBot takes damage (fallback)
    Hit_Projectile_Generic,     // Generic projectile impact
    CuBot_Death_Generic,        // CuBot death burst (fallback)
    Hit_Panoharra,              // Panoharra Tree takes damage
    Hit_Critical,               // Critical strike flash — fires on any crit from any character



    // ── RAJAH BAGWIS ──────────────────────────────────────────────────────
    // Cast effects spawn at the ability origin when the ability fires.
    // Hit effects spawn at the point of impact (use PlayAtImpact where possible).

    Rajah_Primary_Cast,         // Feathery Slash swing arc
    Rajah_Primary_Hit,          // Feathery Slash lands on an enemy
    Rajah_Secondary_Cast,       // Feather Shot muzzle flash / launch trail
    Rajah_Secondary_Hit,        // Feather Shot projectile impact
    Rajah_Q_Cast,               // Sky Rend dash trail (plays at cast origin)
    Rajah_Q_Hit,                // Sky Rend hits enemies during dash
    Rajah_Q_Shield,             // Shield granted after Sky Rend (plays on Rajah)
    Rajah_E_Cast,               // Feather Barrage leap burst
    Rajah_E_Hit,                // Individual feather projectile impact
    Rajah_R_Branch1_Cast,       // Sovereign's Wrath activation burst
    Rajah_R_Branch1_Hit,        // Sovereign's Wrath AoE tick impact
    Rajah_R_Branch1_Final,      // Sovereign's Wrath final strike impact (larger)
    Rajah_R_Branch2_Cast,       // Eagle Eye activation burst
    Rajah_R_Branch2_Hit,        // Eagle Eye continuous feather impacts


    // ── MARI (PHILIPPINE TARSIER) ─────────────────────────────────────────
    // Placeholder entries — fill in when Mari's ability set is finalised.

    Mari_Primary_Cast,
    Mari_Primary_Hit,
    Mari_Secondary_Cast,
    Mari_Secondary_Hit,
    Mari_Q_Cast,                // Mindspikes AoE field
    Mari_Q_Hit,                 // Mindspike DoT tick on enemy
    Mari_E_Cast,                // Psychic Slam launch
    Mari_E_Hit,                 // Psychic Slam knockback impact
    Mari_R_Branch1_Cast,        // Psychic Bloom field activation
    Mari_R_Branch1_Hit,         // Psychic Bloom DoT tick
    Mari_R_Branch2_Cast,        // Mind Unbound laser activation
    Mari_R_Branch2_Hit,         // Mind Unbound laser impact per frame


    // ── CUBOTS — GENERIC ──────────────────────────────────────────────────
    // Shared fallback effects for any CuBot without a type-specific entry.

    CuBot_Attack_Generic,       // Any CuBot attack (fallback swing / projectile)
    CuBot_Aggro,    // Plays when a CuBot switches aggro to the player
    CuBot_Boss_Death_Generic,

    // ── CUBOTS — TYPE-SPECIFIC ────────────────────────────────────────────
    // Add entries as each CuBot's visual effects are designed.
    // Pattern: CuBot_[Name]_[Action]

    CuBot_Chopper_Attack,       // Axe swing arc
    CuBot_Chopper_Hit,          // Chopper takes damage

    CuBot_Hunter_Attack,        // Ranged shot launch
    CuBot_Hunter_Hit,           // Hunter takes damage

    CuBot_Minny_Attack,         // Mining slam
    CuBot_Minny_Hit,            // Minny takes damage

    CuBot_Bernie_Attack,

    // TODO: Add when Stage 2 / Stage 3 CuBot designs are finalised
    // CuBot_Sawyer_Attack,
    // CuBot_Sawyer_Hit,
    // CuBot_Trapper_Attack,
    // CuBot_Trapper_Hit,
    // CuBot_Drilly_Attack,
    // CuBot_Drilly_Hit,
    // CuBot_Shovy_Attack,
    // CuBot_Shovy_Hit,
    // CuBot_Bernie_Attack,
    // CuBot_Bernie_Hit,
    // CuBot_Toxion_Attack,
    // CuBot_Toxion_Hit,
    // CuBot_Luxion_Attack,
    // CuBot_Luxion_Hit,


    // ── STATUS EFFECTS ────────────────────────────────────────────────────
    // Looping effects parented to the affected character.
    // Managed by Mb_StatusVFXHandler — do not set a finite Lifetime for these
    // in SO_VFXLibrary. Use 999 and let the handler call Stop() explicitly.

    Status_Burn,                // Fire / ember loop (Burn DoT)
    Status_Poison,              // Toxic particle loop (Poison DoT)
    Status_Slow,                // Frost / slow-field loop (MoveSlow + AttackSlow share this)
    Status_Stun,                // Stars / daze particles orbiting the head
    Status_Shield,              // Generic shield aura — plays on ANY character (Guardian or CuBot) while shielded
                                // TODO: Status_Root and Status_Silence when visual design is confirmed


    // ─────────────────────────────────────────────────────────────────────────────
    // ENVIRONMENT VFX
    // World-space one-shot effects driven by gameplay events.
    // None of these are parented to a character — they play at a world position.
    // ─────────────────────────────────────────────────────────────────────────────

    // ── WAVE EVENTS ───────────────────────────────────────────────────────
    Wave_Start,                 // Ambient energy pulse at the Panoharra Tree — auto-fired by Mb_VFXManager on OnWaveStart
    CuBot_Spawn,                // Materialise effect when a CuBot activates from the pool
    CuBot_SpawnPillar,      // Large visible beacon at spawn point — announces CuBot location

    // ── GUARDIAN EVENTS ───────────────────────────────────────────────────
    Guardian_Death,             // Large burst on Guardian death — call from Mb_GuardianBase.HandleDeath()
    Guardian_Levelup,           // Level-up flourish on the Guardian — call from Mb_RewardsManager after LevelUp()

    // ── PANOHARRA EVENTS ──────────────────────────────────────────────────
    Panoharra_Death,            // Catastrophic burst when Panoharra is destroyed

    // ── RAFFLESIA TELEPORTER ──────────────────────────────────────────────
    Portal_Enter,               // Entry burst when a character enters the Rafflesia portal
    Portal_Exit,                // Exit burst when a character exits the Rafflesia portal



// ─────────────────────────────────────────────────────────────────────────────
// FOOTSTEP VFX
// Ground-contact effects triggered by Animation Events on walk / run clips.
// Managed by Mb_FootstepVFXEmitter — never called from Update().
// Each value maps to a physics layer via SurfaceMappings in the Inspector.
// ─────────────────────────────────────────────────────────────────────────────


    Footstep_Grass,      // Leaf / dust puff on grass terrain
    Footstep_Water,      // Water ripple splash
    Footstep_Generic,    // Generic dust puff — fallback when no layer match is found

    Guardian_Jump,      // Dust/feather burst when Guardian leaves the ground
    Guardian_Land,      // Impact burst when Guardian lands — scale with fall height later
                        // TODO: Add Sand, Wood, Metal if additional surface types are added to the stage


    // ─────────────────────────────────────────────────────────────────────────────
    // UI VFX
    // Screen-space or near-camera effects tied to UI events.
    // Reserved for future use — none currently implemented.
    // Examples: reward card shimmer, level-up screen flash, defeat vignette.
    // ─────────────────────────────────────────────────────────────────────────────

    // TODO: Define once UI visual direction is confirmed
    RewardCard_Shimmer,
    LevelUp_ScreenFlash,
    Defeat_Vignette,
    Victory_Confetti,
    Burn_Vignette,
    Poison_Vignette,
    Guardian_LowHP_Vignette,    // Looping red pulse on the camera — started/stopped by threshold
}