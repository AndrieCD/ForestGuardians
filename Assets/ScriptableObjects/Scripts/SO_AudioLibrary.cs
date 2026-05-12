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
    Hit_Generic,
    Hit_Critical,
    Ability_Q,
    Ability_E,
    Ability_R,
    Ability_Primary,
    Ability_Secondary,
    CuBot_Death,
    CuBot_Spawn,
    Guardian_Death,
    Panoharra_Hit
}

public enum UISFX
{
    UI_Click,
    UI_Hover,
    UI_RewardOpen,
    UI_RewardSelect,
    UI_WaveStart,
    UI_WaveComplete
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