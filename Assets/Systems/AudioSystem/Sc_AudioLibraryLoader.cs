// Sc_AudioLibraryLoader.cs
// A plain C# helper class that validates SO_AudioLibrary at startup.
// Mb_AudioManager calls Validate() once in Awake() before any audio plays.
//
// WHY THIS EXISTS:
//   SO_AudioLibrary is filled manually in the Inspector. It's easy to forget
//   to assign a clip, or to add a new enum value without adding the matching entry.
//   This validator scans all audio categories and logs a clear warning for
//   every gap — so the team catches missing clips immediately during development,
//   not when a sound fails to play during a playtest.
//
// THIS IS NOT A MONOBEHAVIOUR — it has no Update, no scene lifetime, no GameObject.
// Mb_AudioManager creates one instance of this class and calls Validate() once.

using System;
using UnityEngine;

public class Sc_AudioLibraryLoader
{
    // The library asset we are validating — passed in from Mb_AudioManager
    private readonly SO_AudioLibrary _library;

    // Counts how many clips are missing across all categories.
    // Mb_AudioManager can log a summary after validation completes.
    public int MissingClipCount { get; private set; } = 0;


    public Sc_AudioLibraryLoader(SO_AudioLibrary library)
    {
        _library = library;
    }


    /// <summary>
    /// Scans audio entries and logs a warning for every assigned row with a null clip.
    /// If SO_AudioLibrary.WarnForMissingEntries is true, it also checks every enum key.
    /// that has no matching entry, or has an entry with a null clip.
    ///
    /// Call this once in Mb_AudioManager.Awake() before any audio plays.
    /// Returns true if no clips are missing — false means at least one gap was found.
    /// </summary>
    public bool Validate()
    {
        MissingClipCount = 0;

        ValidateMusicTracks();
        ValidateCombatSFX();
        ValidateEnvironmentSFX();
        ValidateUISFX();

        if (MissingClipCount == 0)
        {
            Debug.Log("[Sc_AudioLibraryLoader] All audio clips validated — no missing entries.");
            return true;
        }

        Debug.LogWarning($"[Sc_AudioLibraryLoader] Validation complete — {MissingClipCount} clip(s) missing. " +
                         "Check SO_AudioLibrary in the Inspector and assign the flagged clips.");
        return false;
    }


    // ─────────────────────────────────────────────────────────────────────────
    // Category Validators
    // Each one iterates over every value in its enum and checks whether
    // SO_AudioLibrary has a matching entry with a non-null clip assigned.
    // ─────────────────────────────────────────────────────────────────────────

    private void ValidateMusicTracks()
    {
        foreach (MusicEntry entry in _library.MusicTracks)
        {
            if (entry.Track == MusicTrack.None)
                continue;

            if (entry.Clip == null)
            {
                Debug.LogWarning($"[Sc_AudioLibraryLoader] NULL CLIP — MusicTrack.{entry.Track} " +
                                 "has an entry in SO_AudioLibrary but its AudioClip is not assigned.");
                MissingClipCount++;
            }
        }

        if (!_library.WarnForMissingEntries)
            return;

        foreach (MusicTrack track in Enum.GetValues(typeof(MusicTrack)))
        {
            if (track == MusicTrack.None) continue;

            if (!_library.TryGetMusic(track, out MusicEntry entry))
            {
                Debug.LogWarning($"[Sc_AudioLibraryLoader] MISSING ENTRY — MusicTrack.{track} " +
                                 "has no entry in SO_AudioLibrary.MusicTracks.");
                MissingClipCount++;
                continue;
            }

            // The entry exists but the clip itself was never assigned in the Inspector
            if (entry.Clip == null)
            {
                Debug.LogWarning($"[Sc_AudioLibraryLoader] NULL CLIP — MusicTrack.{track} " +
                                 "has an entry in SO_AudioLibrary but its AudioClip is not assigned.");
                MissingClipCount++;
            }
        }
    }


    private void ValidateCombatSFX()
    {
        foreach (CombatSFXEntry entry in _library.CombatSounds)
        {
            if (entry.Clip == null)
            {
                Debug.LogWarning($"[Sc_AudioLibraryLoader] NULL CLIP — CombatSFX.{entry.Key} " +
                                 "has an entry in SO_AudioLibrary but its AudioClip is not assigned.");
                MissingClipCount++;
            }
        }

        if (!_library.WarnForMissingEntries)
            return;

        foreach (CombatSFX key in Enum.GetValues(typeof(CombatSFX)))
        {
            if (!_library.TryGetCombatSFX(key, out CombatSFXEntry entry))
            {
                Debug.LogWarning($"[Sc_AudioLibraryLoader] MISSING ENTRY — CombatSFX.{key} " +
                                 "has no entry in SO_AudioLibrary.CombatSounds.");
                MissingClipCount++;
                continue;
            }

            if (entry.Clip == null)
            {
                Debug.LogWarning($"[Sc_AudioLibraryLoader] NULL CLIP — CombatSFX.{key} " +
                                 "has an entry in SO_AudioLibrary but its AudioClip is not assigned.");
                MissingClipCount++;
            }
        }
    }

    private void ValidateEnvironmentSFX()
    {
        foreach (EnvironmentSFXEntry entry in _library.EnvironmentSounds)
        {
            if (entry.Clip == null)
            {
                Debug.LogWarning($"[Sc_AudioLibraryLoader] NULL CLIP — EnvironmentSFX.{entry.Key} " +
                                 "has an entry in SO_AudioLibrary but its AudioClip is not assigned.");
                MissingClipCount++;
            }
        }

        if (!_library.WarnForMissingEntries)
            return;

        foreach (EnvironmentSFX key in Enum.GetValues(typeof(EnvironmentSFX)))
        {
            if (!_library.TryGetEnvironmentSFX(key, out EnvironmentSFXEntry entry))
            {
                Debug.LogWarning($"[Sc_AudioLibraryLoader] MISSING ENTRY — EnvironmentSFX.{key} " +
                                 "has no entry in SO_AudioLibrary.EnvironmentSounds.");
                MissingClipCount++;
                continue;
            }

            if (entry.Clip == null)
            {
                Debug.LogWarning($"[Sc_AudioLibraryLoader] NULL CLIP — EnvironmentSFX.{key} " +
                                 "has an entry in SO_AudioLibrary but its AudioClip is not assigned.");
                MissingClipCount++;
            }
        }
    }

    private void ValidateUISFX()
    {
        foreach (UISFXEntry entry in _library.UISounds)
        {
            if (entry.Clip == null)
            {
                Debug.LogWarning($"[Sc_AudioLibraryLoader] NULL CLIP — UISFX.{entry.Key} " +
                                 "has an entry in SO_AudioLibrary but its AudioClip is not assigned.");
                MissingClipCount++;
            }
        }

        if (!_library.WarnForMissingEntries)
            return;

        foreach (UISFX key in Enum.GetValues(typeof(UISFX)))
        {
            if (!_library.TryGetUISFX(key, out UISFXEntry entry))
            {
                Debug.LogWarning($"[Sc_AudioLibraryLoader] MISSING ENTRY — UISFX.{key} " +
                                 "has no entry in SO_AudioLibrary.UISounds.");
                MissingClipCount++;
                continue;
            }

            if (entry.Clip == null)
            {
                Debug.LogWarning($"[Sc_AudioLibraryLoader] NULL CLIP — UISFX.{key} " +
                                 "has an entry in SO_AudioLibrary but its AudioClip is not assigned.");
                MissingClipCount++;
            }
        }
    }
}
