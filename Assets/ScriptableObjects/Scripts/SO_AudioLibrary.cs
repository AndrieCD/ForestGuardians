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
    None,           // Used as a "no music" / stop signal — not a real clip
    MainMenu,
    Combat_Stage1,
    Combat_Stage2,
    Combat_Stage3,
    Victory,
    Defeat
}

public enum CombatSFX
{
    // ── SHARED / GENERIC ──────────────────────────────────────────────────
    // Used as fallbacks or for characters without a unique sound yet

    Hit_Guardian,           // any guardian takes damage (fallback)
    Hit_CuBot,              // any cubot takes damage (fallback)
    CuBot_Death,
    CuBot_Spawn,

    // ── RAJAH BAGWIS ──────────────────────────────────────────────────────
    Rajah_Feather_Launch, // Feather Shot fired
    Rajah_Primary,          // Feathery Slash swing
    Rajah_Primary_Hit,      // Feathery Slash lands on enemy
    Rajah_Secondary_Swing,  // Feather Shot wind-up
    Rajah_Secondary_Hit,    // Feather Shot hits enemy
    Rajah_Q_Cast,           // Sky Rend dash starts
    Rajah_Q_Hit,            // Sky Rend hits enemies during dash
    Rajah_E_Launch,         // Feather Barrage leap + fire
    Rajah_E_Hit,            // Feather Barrage feather hits enemy
    Rajah_R_Branch1_Cast,   // Sovereign's Wrath activation
    Rajah_R_Branch1_Hit,
    Rajah_R_Branch2_Cast,   // Eagle Eye activation
    Rajah_R_Branch2_Hit,

    // ── MARI (TARSIER) ────────────────────────────────────────────────────
    Mari_Primary,
    Mari_Primary_Hit,
    Mari_Secondary_Launch,
    Mari_Secondary_Hit,
    Mari_Q_Cast,
    Mari_Q_Hit,
    Mari_E_Cast,
    Mari_E_Hit,
    Mari_R_Branch1_Cast,
    Mari_R_Branch1_Hit,
    Mari_R_Branch2_Cast,
    Mari_R_Branch2_Hit,

    // ── CUBOTS — SHARED ───────────────────────────────────────────────────
    CuBot_Hit_Generic,      // fallback for any cubot hit
    CuBot_Attack_Generic,    // fallback for any cubot attack (swing or projectile)
    CuBot_Aggro_Generic,
    CuBot_Chopper_Attack,
    CuBot_Chopper_Hit,
    CuBot_Hunter_Attack,
    CuBot_Hunter_Hit,
    CuBot_Minny_Attack,
    CuBot_Minny_Hit,
    // Add Sawyer, Trapper, Drilly, Shovy, Bernie, Toxion, Luxion as needed

    // ── ENVIRONMENT ───────────────────────────────────────────────────────
    Panoharra_Hit,
    Panoharra_Death,
}

public enum EnvironmentSFX
{
    Guardian_Footstep_Generic,
    Guardian_Footstep_Water,
    Guardian_Jump_Generic,
    Guardian_Land_Generic,
}

public enum UISFX
{
    // Navigation
    UI_Click_Generic,       // fallback for any unspecified button
    UI_Click_Confirm,       // confirm / proceed (e.g. "Play", "Select")
    UI_Click_Back,          // back / cancel buttons
    UI_Hover,               // mouse-over any interactable

    // Main Menu specific
    UI_MainMenu_Start,      // Play button specifically
    UI_MainMenu_Open,       // opening the menu (scene load lands)

    // Rewards Panel
    UI_RewardPanel_Open,    // panel slides in
    UI_RewardSelect,        // player picks a card
    UI_RewardHover,         // hovering a reward card (optional, distinct feel)

    // Pause Menu
    UI_Pause_Open,
    UI_Pause_Resume,

    // Wave Announcements
    UI_Countdown_3,         // "3" voice or beep
    UI_Countdown_2,         // "2"
    UI_Countdown_1,         // "1"
    UI_WaveStart,
    UI_WaveComplete,
    UI_StageClear,          // all waves done
    UI_StageDefeat,          // Panoharra destroyed

    UX_Heartbeat,             // low thump that plays when HP is low, or as a warning cue

    UI_PanoharraUnderAttack,
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