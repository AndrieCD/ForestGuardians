// SO_VFXLibrary.cs
// The asset that stores every VFX prefab in one place.
// Mb_VFXManager reads this on Awake to build its object pools.
//
// WHY A SCRIPTABLEOBJECT:
//   All asset references stay in one Inspector-editable file.
//   Mb_VFXManager never hard-references prefabs — it asks the library for them
//   by enum key, so swapping a prefab only requires updating this asset.
//
// INSPECTOR SETUP:
//   1. Right-click in the Project window → Create → ForestGuardians → VFXLibrary
//   2. You will see a list of VFXEntry rows — one per VFXType value.
//   3. For each row:
//        - Prefab      → drag in the particle system prefab
//        - Lifetime    → how long (seconds) before the instance returns to pool
//                        (override only if the particle system duration alone isn't reliable)
//        - Pool Size   → how many instances to pre-warm at scene start
//   4. Mb_VFXManager.Awake() calls Validate() which logs warnings for any null prefabs.
//      Fix all warnings before a playtest.
//
// ABOUT LIFETIME:
//   Unity's ParticleSystem has its own duration + stop action settings.
//   Lifetime here is the wall-clock override used by Mb_VFXInstance's return-to-pool
//   coroutine. Set it slightly longer than the particle system's visual duration
//   so the effect finishes playing before disappearing. e.g. if the particle
//   system plays for 1.5s, set Lifetime to 2.0s.
//   For looping status effects, set Lifetime to float.PositiveInfinity (or a large
//   number) — Mb_StatusVFXHandler calls Stop() explicitly when the status ends.

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New VFXLibrary", menuName = "ForestGuardians/VFXLibrary")]
public class SO_VFXLibrary : ScriptableObject
{
    [Header("VFX Entries")]
    [Tooltip("Add one entry per VFXType value. " +
             "Mb_VFXManager logs a warning at startup for any VFXType with no matching entry.")]
    public List<VFXEntry> Entries = new List<VFXEntry>();

    // Built at runtime by BuildLookup() so TryGetEntry() is O(1), not O(n).
    // Dictionary is rebuilt every time the library is loaded — no stale cache risk.
    private Dictionary<VFXType, VFXEntry> _lookup;


    /// <summary>
    /// Looks up the entry for the given VFXType.
    /// Returns true and populates 'entry' if found; returns false if not.
    /// Mb_VFXManager calls this to get prefab and pool size per type.
    /// </summary>
    public bool TryGetEntry(VFXType type, out VFXEntry entry)
    {
        // Build the lookup on first use — lazy init so this works even if
        // Awake() order means the library hasn't been touched yet.
        if (_lookup == null)
            BuildLookup();

        return _lookup.TryGetValue(type, out entry);
    }


    /// <summary>
    /// Scans all VFXType enum values and logs a warning for every value
    /// that has no matching entry or has a null prefab.
    /// Call this once from Mb_VFXManager.Awake() before pools are built.
    /// Returns true if no issues were found.
    /// </summary>
    public bool Validate()
    {
        if (_lookup == null)
            BuildLookup();

        int missingCount = 0;

        foreach (VFXType type in Enum.GetValues(typeof(VFXType)))
        {
            if (!_lookup.TryGetValue(type, out VFXEntry entry))
            {
                Debug.LogWarning($"[SO_VFXLibrary] MISSING ENTRY — VFXType.{type} " +
                                 "has no entry in the VFX library. Add it in the Inspector.");
                missingCount++;
                continue;
            }

            if (entry.Prefab == null)
            {
                Debug.LogWarning($"[SO_VFXLibrary] NULL PREFAB — VFXType.{type} " +
                                 "has an entry but its Prefab field is not assigned.");
                missingCount++;
            }
        }

        if (missingCount == 0)
        {
            Debug.Log("[SO_VFXLibrary] All VFX entries validated — no missing prefabs.");
            return true;
        }

        Debug.LogWarning($"[SO_VFXLibrary] Validation complete — {missingCount} issue(s) found. " +
                         "Fix null or missing prefabs before playtesting.");
        return false;
    }


    // Converts the flat list into a dictionary for fast lookups.
    // Called lazily — safe to call multiple times (idempotent).
    private void BuildLookup()
    {
        _lookup = new Dictionary<VFXType, VFXEntry>();

        foreach (var entry in Entries)
        {
            if (_lookup.ContainsKey(entry.Type))
            {
                // Duplicate entries would silently overwrite each other — warn instead.
                Debug.LogWarning($"[SO_VFXLibrary] Duplicate entry for VFXType.{entry.Type}. " +
                                 "Only the first entry will be used. Remove the duplicate.");
                continue;
            }

            _lookup[entry.Type] = entry;
        }
    }
}


// ─────────────────────────────────────────────────────────────────────────────
// Supporting Data Type
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One row in SO_VFXLibrary — pairs a VFXType with its prefab and pool settings.
/// </summary>
[Serializable]
public class VFXEntry
{
    [Tooltip("The VFXType this entry corresponds to. Must be unique in the library.")]
    public VFXType Type;

    [Tooltip("The particle system prefab to spawn for this VFX. " +
             "Must have an Mb_VFXInstance component attached.")]
    public GameObject Prefab;

    [Tooltip("How long (seconds) before the instance returns to the pool after playing. " +
             "Set slightly longer than the particle system's visual duration. " +
             "For looping status VFX, set this to a very large value — " +
             "Mb_StatusVFXHandler calls Stop() explicitly when the status ends.")]
    public float Lifetime = 2f;

    [Tooltip("How many instances of this VFX to pre-warm at scene start. " +
             "Increase this if the VFX fires frequently (e.g. Hit_Generic should be ~10). " +
             "Mb_VFXManager expands the pool with a warning if this is too small.")]
    [Min(1)]
    public int PoolSize = 3;
}