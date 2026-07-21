// SO_AudioLibrary.cs
// A ScriptableObject that acts as the single source of truth for all audio assets
// in Forest Guardians. Every clip is stored here, indexed by a named enum key.
//
// WHY ENUMS INSTEAD OF STRINGS:
//   Using enum keys means typos are caught at compile time, not at runtime.
//   If you rename a clip's enum entry, every reference breaks visibly — no silent failures.
//
// CREATE IN PROJECT WINDOW:
//   Right-click > Create > ForestGuardians > AudioLibrary
//   Assign one instance to Mb_AudioManager in the Inspector.

using System.Collections.Generic;
using UnityEngine;


// ─────────────────────────────────────────────────────────────────────────────
// AUDIO ENUMS
// Defined here so every script in the project can reference them without
// needing a separate file to hunt down.
// ─────────────────────────────────────────────────────────────────────────────

public enum MusicTrack
{
    None = 0,           // Used as a "no music" / stop signal — not a real clip
    MainMenu = 100,
    Combat_Stage1 = 200,
    Combat_Stage2 = 210,
    Combat_Stage3 = 220,
    Tutorial = 230,
    Victory = 300,
    Defeat = 310
}

public enum CombatSFX
{
    // ── SHARED / GENERIC ──────────────────────────────────────────────────
    // Used as fallbacks or for characters without a unique sound yet

    Hit_Guardian = 1000,        // any guardian takes damage (fallback)
    Hit_CuBot = 1010,           // any cubot takes damage (fallback)

    // ── RAJAH BAGWIS ──────────────────────────────────────────────────────
    Rajah_Feather_Launch = 2000, // Feather Shot fired
    Rajah_Primary = 2010,        // Feathery Slash swing
    Rajah_Primary_Hit = 2020,    // Feathery Slash lands on enemy
    Rajah_Secondary_Swing = 2030,// Feather Shot wind-up
    Rajah_Secondary_Hit = 2040,  // Feather Shot hits enemy
    Rajah_Q_Cast = 2050,         // Sky Rend dash starts
    Rajah_Q_Hit = 2060,          // Sky Rend hits enemies during dash
    Rajah_E_Launch = 2070,       // Feather Barrage leap + fire
    Rajah_E_Hit = 2080,          // Feather Barrage feather hits enemy
    Rajah_R_Branch1_Cast = 2090, // Sovereign's Wrath activation
    Rajah_R_Branch1_Hit = 2100,
    Rajah_R_Branch2_Cast = 2110, // Eagle Eye activation
    Rajah_R_Branch2_Hit = 2120,

    // ── MARI (TARSIER) ────────────────────────────────────────────────────
    Mari_Primary = 3000,
    Mari_Primary_Hit = 3010,
    Mari_Secondary_Launch = 3020,
    Mari_Secondary_Hit = 3030,
    Mari_Q_Cast = 3040,
    Mari_Q_Hit = 3050,
    Mari_E_Cast = 3060,
    Mari_E_Hit = 3070,
    Mari_R_Branch1_Cast = 3080,
    Mari_R_Branch1_Hit = 3090,
    Mari_R_Branch2_Cast = 3100,
    Mari_R_Branch2_Hit = 3110,

    // ── CUBOTS — SHARED ───────────────────────────────────────────────────
    CuBot_Death = 4000,
    CuBot_Spawn = 4010,
    CuBot_Hit_Generic = 4020,       // fallback for any cubot hit
    CuBot_Attack_Generic = 4030,    // fallback for any cubot attack (swing or projectile)
    CuBot_Aggro_Generic = 4040,
    CuBot_Chopper_Attack = 4100,
    CuBot_Chopper_Hit = 4110,
    CuBot_Hunter_Attack = 4120,
    CuBot_Hunter_Hit = 4130,
    CuBot_Minny_Attack = 4140,
    CuBot_Minny_Hit = 4150,
    CuBot_Bernie_Attack = 4160,
    CuBot_Bernie_Hit = 4170,
    CuBot_Sawyer_Attack = 4180,
    CuBot_Sawyer_Hit = 4190,
    CuBot_Trapper_Attack = 4200,
    CuBot_Trapper_Hit = 4210,
    CuBot_Drilly_Attack = 4220,
    CuBot_Drilly_Hit = 4230,
    CuBot_Shovy_Attack = 4240,
    CuBot_Shovy_Hit = 4250,
    CuBot_Toxion_Attack = 4260,
    CuBot_Toxion_Hit = 4270,
    CuBot_Luxion_Attack = 4280,
    CuBot_Luxion_Hit = 4290,

    // ── ENVIRONMENT ───────────────────────────────────────────────────────
    Panoharra_Hit = 6000,
    Panoharra_Death = 6010,
}

public enum EnvironmentSFX
{
    Guardian_Footstep_Generic = 7000,
    Guardian_Footstep_Water = 7010,
    Guardian_Footstep_Grass = 7020,
    Guardian_Footstep_Mud = 7030,
    Guardian_Footstep_Stone = 7040,
    Guardian_Footstep_Metal = 7050,
    Guardian_Jump_Generic = 7100,
    Guardian_Land_Generic = 7110,

    Almanac_Collected = 7200,
    River_Splash = 7210,
    Portal_Enter = 7300,
    Portal_Exit = 7310,
    Wave_Start = 7400,
    Wave_Complete = 7410,
}

public enum UISFX
{
    // Navigation
    UI_Click_Generic = 8000,       // fallback for any unspecified button
    UI_Click_Confirm = 8010,       // confirm / proceed (e.g. "Play", "Select")
    UI_Click_Back = 8020,          // back / cancel buttons
    UI_Hover = 8030,               // mouse-over any interactable

    // Main Menu specific
    UI_MainMenu_Start = 8100,      // Play button specifically
    UI_MainMenu_Open = 8110,       // opening the menu (scene load lands)

    // Rewards Panel
    UI_RewardPanel_Open = 8200,    // panel slides in
    UI_RewardSelect = 8210,        // player picks a card
    UI_RewardHover = 8220,         // hovering a reward card (optional, distinct feel)

    // Pause Menu
    UI_Pause_Open = 8300,
    UI_Pause_Resume = 8310,

    // Wave Announcements
    UI_Countdown_3 = 8400,         // "3" voice or beep
    UI_Countdown_2 = 8410,         // "2"
    UI_Countdown_1 = 8420,         // "1"
    UI_WaveStart = 8430,
    UI_WaveComplete = 8440,
    UI_StageClear = 8450,          // all waves done
    UI_StageDefeat = 8460,         // Panoharra destroyed

    UX_Heartbeat = 8500,           // low thump that plays when HP is low, or as a warning cue

    UI_PanoharraUnderAttack = 8510,
}


// ─────────────────────────────────────────────────────────────────────────────
// ENTRY STRUCTS
// Each entry pairs a key (enum) with a clip and a default volume.
// Volume is a 0–1 float — think of it as a per-clip mixing offset on top of
// the AudioMixer group volume, useful when one clip is inherently louder.
// ─────────────────────────────────────────────────────────────────────────────

[System.Serializable]
public struct MusicEntry
{
    public MusicTrack Track;
    public AudioClip Clip;
    [Range(0f, 1f)] public float DefaultVolume;
}

[System.Serializable]
public struct CombatSFXEntry
{
    public CombatSFX Key;
    public AudioClip Clip;
    [Range(0f, 1f)] public float DefaultVolume;
}

[System.Serializable]
public struct EnvironmentSFXEntry
{
    public EnvironmentSFX Key;
    public AudioClip Clip;
    [Range(0f, 1f)] public float DefaultVolume;
}

[System.Serializable]
public struct UISFXEntry
{
    public UISFX Key;
    public AudioClip Clip;
    [Range(0f, 1f)] public float DefaultVolume;
}


// ─────────────────────────────────────────────────────────────────────────────
// SCRIPTABLEOBJECT
// ─────────────────────────────────────────────────────────────────────────────

[CreateAssetMenu(fileName = "AudioLibrary", menuName = "ForestGuardians/AudioLibrary")]
public class SO_AudioLibrary : ScriptableObject
{
    [Header("Validation")]
    [Tooltip("When true, startup validation logs every enum key that has no library entry. Keep false while planned enum keys are still empty.")]
    public bool WarnForMissingEntries = false;

    [Header("Music Tracks")]
    public List<MusicEntry> MusicTracks = new List<MusicEntry>();

    [Header("Combat SFX")]
    public List<CombatSFXEntry> CombatSounds = new List<CombatSFXEntry>();

    [Header("Environment SFX")]
    public List<EnvironmentSFXEntry> EnvironmentSounds = new List<EnvironmentSFXEntry>();

    [Header("UI SFX")]
    public List<UISFXEntry> UISounds = new List<UISFXEntry>();


    // ─────────────────────────────────────────────────────────────────────────
    // LOOKUP METHODS
    // Called by Mb_AudioManager at runtime. Returns true if the clip was found.
    // The out parameters carry both the clip and its default volume so the
    // caller doesn't need to do a second lookup.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the music entry for the given track enum key.
    /// Returns false if no matching entry exists — caller should log a warning.
    /// </summary>
    public bool TryGetMusic(MusicTrack track, out MusicEntry result)
    {
        foreach (var entry in MusicTracks)
        {
            if (entry.Track == track)
            {
                result = entry;
                return true;
            }
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Finds the combat SFX entry for the given key.
    /// Returns false if no matching entry exists.
    /// </summary>
    public bool TryGetCombatSFX(CombatSFX key, out CombatSFXEntry result)
    {
        foreach (var entry in CombatSounds)
        {
            if (entry.Key == key)
            {
                result = entry;
                return true;
            }
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Finds the combat SFX entry for the given key.
    /// Returns false if no matching entry exists.
    /// </summary>
    public bool TryGetEnvironmentSFX(EnvironmentSFX key, out EnvironmentSFXEntry result)
    {
        foreach (var entry in EnvironmentSounds )
        {
            if (entry.Key == key)
            {
                result = entry;
                return true;
            }
        }

        result = default;
        return false;
    }


    /// <summary>
    /// Finds the UI SFX entry for the given key.
    /// Returns false if no matching entry exists.
    /// </summary>
    public bool TryGetUISFX(UISFX key, out UISFXEntry result)
    {
        foreach (var entry in UISounds)
        {
            if (entry.Key == key)
            {
                result = entry;
                return true;
            }
        }

        result = default;
        return false;
    }
}
