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
//   2. Fill the category-specific lists instead of one long global dropdown.
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
    [Header("Validation")]
    [Tooltip("When true, Validate() logs every VFXType that has no library entry. Keep false while planned enum keys are still empty.")]
    public bool WarnForMissingEntries = false;

    [Header("Generic Combat VFX")]
    public List<GenericCombatVFXEntry> GenericCombatVFX = new List<GenericCombatVFXEntry>();

    [Header("Rajah VFX")]
    public List<RajahVFXEntry> RajahVFX = new List<RajahVFXEntry>();

    [Header("Mari VFX")]
    public List<MariVFXEntry> MariVFX = new List<MariVFXEntry>();

    [Header("CuBot Combat VFX")]
    public List<CuBotVFXEntry> CuBotCombatVFX = new List<CuBotVFXEntry>();

    [Header("Status Effect VFX")]
    public List<StatusVFXEntry> StatusEffectVFX = new List<StatusVFXEntry>();

    [Header("Environment VFX")]
    public List<EnvironmentVFXEntry> EnvironmentVFX = new List<EnvironmentVFXEntry>();

    [Header("Footstep VFX")]
    public List<FootstepVFXEntry> FootstepVFX = new List<FootstepVFXEntry>();

    [Header("UI VFX")]
    public List<UIVFXEntry> UIVFX = new List<UIVFXEntry>();

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

        foreach (VFXEntry entry in _lookup.Values)
        {
            if (entry.Prefab == null)
            {
                Debug.LogWarning($"[SO_VFXLibrary] NULL PREFAB — VFXType.{entry.Type} " +
                                 "has an entry but its Prefab field is not assigned.");
                missingCount++;
            }
        }

        if (WarnForMissingEntries)
        {
            foreach (VFXType type in Enum.GetValues(typeof(VFXType)))
            {
                if (_lookup.ContainsKey(type))
                    continue;

                Debug.LogWarning($"[SO_VFXLibrary] MISSING ENTRY — VFXType.{type} " +
                                 "has no entry in the VFX library. Add it in the Inspector.");
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

        AddEntries(GenericCombatVFX, entry => entry.Type, entry => entry.Prefab, entry => entry.Lifetime, entry => entry.PoolSize, entry => entry.MaxInstances, entry => entry.Priority, entry => entry.ExhaustionPolicy);
        AddEntries(RajahVFX, entry => entry.Type, entry => entry.Prefab, entry => entry.Lifetime, entry => entry.PoolSize, entry => entry.MaxInstances, entry => entry.Priority, entry => entry.ExhaustionPolicy);
        AddEntries(MariVFX, entry => entry.Type, entry => entry.Prefab, entry => entry.Lifetime, entry => entry.PoolSize, entry => entry.MaxInstances, entry => entry.Priority, entry => entry.ExhaustionPolicy);
        AddEntries(CuBotCombatVFX, entry => entry.Type, entry => entry.Prefab, entry => entry.Lifetime, entry => entry.PoolSize, entry => entry.MaxInstances, entry => entry.Priority, entry => entry.ExhaustionPolicy);
        AddEntries(StatusEffectVFX, entry => entry.Type, entry => entry.Prefab, entry => entry.Lifetime, entry => entry.PoolSize, entry => entry.MaxInstances, entry => entry.Priority, entry => entry.ExhaustionPolicy);
        AddEntries(EnvironmentVFX, entry => entry.Type, entry => entry.Prefab, entry => entry.Lifetime, entry => entry.PoolSize, entry => entry.MaxInstances, entry => entry.Priority, entry => entry.ExhaustionPolicy);
        AddEntries(FootstepVFX, entry => entry.Type, entry => entry.Prefab, entry => entry.Lifetime, entry => entry.PoolSize, entry => entry.MaxInstances, entry => entry.Priority, entry => entry.ExhaustionPolicy);
        AddEntries(UIVFX, entry => entry.Type, entry => entry.Prefab, entry => entry.Lifetime, entry => entry.PoolSize, entry => entry.MaxInstances, entry => entry.Priority, entry => entry.ExhaustionPolicy);
    }

    private void AddEntries<TEntry, TEnum>(
        List<TEntry> entries,
        Func<TEntry, TEnum> getType,
        Func<TEntry, GameObject> getPrefab,
        Func<TEntry, float> getLifetime,
        Func<TEntry, int> getPoolSize,
        Func<TEntry, int> getMaxInstances,
        Func<TEntry, VFXPriority> getPriority,
        Func<TEntry, VFXPoolExhaustionPolicy> getExhaustionPolicy) where TEnum : Enum
    {
        foreach (TEntry sourceEntry in entries)
        {
            VFXType runtimeType = (VFXType)Convert.ToInt32(getType(sourceEntry));
            var runtimeEntry = new VFXEntry
            {
                Type = runtimeType,
                Prefab = getPrefab(sourceEntry),
                Lifetime = getLifetime(sourceEntry),
                PoolSize = getPoolSize(sourceEntry),
                MaxInstances = Mathf.Max(getMaxInstances(sourceEntry), getPoolSize(sourceEntry)),
                Priority = getPriority(sourceEntry),
                ExhaustionPolicy = getExhaustionPolicy(sourceEntry)
            };

            if (_lookup.ContainsKey(runtimeType))
            {
                Debug.LogWarning($"[SO_VFXLibrary] Duplicate entry for VFXType.{runtimeType}. " +
                                 "Only the first entry will be used. Remove the duplicate.");
                continue;
            }

            _lookup[runtimeType] = runtimeEntry;
        }
    }
}


// ─────────────────────────────────────────────────────────────────────────────
// Supporting Data Type
// ─────────────────────────────────────────────────────────────────────────────

public enum VFXPriority
{
    Low,
    Normal,
    High,
    Critical
}

public enum VFXPoolExhaustionPolicy
{
    ExpandUntilCap,
    Drop
}

/// <summary>
/// Runtime VFX row used by Mb_VFXManager after category entries are converted.
/// </summary>
[Serializable]
public class VFXEntry
{
    [Tooltip("The VFXType this entry corresponds to. Must be unique in the library.")]
    public VFXType Type;

    [Tooltip("The particle system prefab to spawn for this VFX. " +
             "Must have an Mb_VFXInstance component attached.")]
    public GameObject Prefab;

    [Tooltip("Legacy lifetime metadata retained for asset compatibility. " +
             "One-shot effects now return when their particle systems complete.")]
    public float Lifetime = 2f;

    [Tooltip("How many instances of this VFX to pre-warm at scene start.")]
    [Min(1)]
    public int PoolSize = 3;

    [Tooltip("Maximum number of instances this VFX may create, including pre-warmed instances.")]
    [Min(1)]
    public int MaxInstances = 20;

    [Tooltip("Importance used when the global VFX active-instance budget is full.")]
    public VFXPriority Priority = VFXPriority.Normal;

    [Tooltip("Behavior when this VFX reaches MaxInstances and no pooled instance is available.")]
    public VFXPoolExhaustionPolicy ExhaustionPolicy = VFXPoolExhaustionPolicy.ExpandUntilCap;
}

[Serializable]
public class GenericCombatVFXEntry
{
    public GenericCombatVFXType Type;
    public GameObject Prefab;
    public float Lifetime = 2f;
    [Min(1)] public int PoolSize = 3;
    [Min(1)] public int MaxInstances = 20;
    public VFXPriority Priority = VFXPriority.Normal;
    public VFXPoolExhaustionPolicy ExhaustionPolicy = VFXPoolExhaustionPolicy.ExpandUntilCap;
}

[Serializable]
public class RajahVFXEntry
{
    public RajahVFXType Type;
    public GameObject Prefab;
    public float Lifetime = 2f;
    [Min(1)] public int PoolSize = 3;
    [Min(1)] public int MaxInstances = 20;
    public VFXPriority Priority = VFXPriority.Normal;
    public VFXPoolExhaustionPolicy ExhaustionPolicy = VFXPoolExhaustionPolicy.ExpandUntilCap;
}

[Serializable]
public class MariVFXEntry
{
    public MariVFXType Type;
    public GameObject Prefab;
    public float Lifetime = 2f;
    [Min(1)] public int PoolSize = 3;
    [Min(1)] public int MaxInstances = 20;
    public VFXPriority Priority = VFXPriority.Normal;
    public VFXPoolExhaustionPolicy ExhaustionPolicy = VFXPoolExhaustionPolicy.ExpandUntilCap;
}

[Serializable]
public class CuBotVFXEntry
{
    public CuBotVFXType Type;
    public GameObject Prefab;
    public float Lifetime = 2f;
    [Min(1)] public int PoolSize = 3;
    [Min(1)] public int MaxInstances = 20;
    public VFXPriority Priority = VFXPriority.Normal;
    public VFXPoolExhaustionPolicy ExhaustionPolicy = VFXPoolExhaustionPolicy.ExpandUntilCap;
}

[Serializable]
public class StatusVFXEntry
{
    public StatusVFXType Type;
    public GameObject Prefab;
    public float Lifetime = 2f;
    [Min(1)] public int PoolSize = 3;
    [Min(1)] public int MaxInstances = 20;
    public VFXPriority Priority = VFXPriority.Normal;
    public VFXPoolExhaustionPolicy ExhaustionPolicy = VFXPoolExhaustionPolicy.ExpandUntilCap;
}

[Serializable]
public class EnvironmentVFXEntry
{
    public EnvironmentVFXType Type;
    public GameObject Prefab;
    public float Lifetime = 2f;
    [Min(1)] public int PoolSize = 3;
    [Min(1)] public int MaxInstances = 20;
    public VFXPriority Priority = VFXPriority.Normal;
    public VFXPoolExhaustionPolicy ExhaustionPolicy = VFXPoolExhaustionPolicy.ExpandUntilCap;
}

[Serializable]
public class FootstepVFXEntry
{
    public FootstepVFXType Type;
    public GameObject Prefab;
    public float Lifetime = 2f;
    [Min(1)] public int PoolSize = 3;
    [Min(1)] public int MaxInstances = 20;
    public VFXPriority Priority = VFXPriority.Normal;
    public VFXPoolExhaustionPolicy ExhaustionPolicy = VFXPoolExhaustionPolicy.ExpandUntilCap;
}

[Serializable]
public class UIVFXEntry
{
    public UIVFXType Type;
    public GameObject Prefab;
    public float Lifetime = 2f;
    [Min(1)] public int PoolSize = 3;
    [Min(1)] public int MaxInstances = 20;
    public VFXPriority Priority = VFXPriority.Normal;
    public VFXPoolExhaustionPolicy ExhaustionPolicy = VFXPoolExhaustionPolicy.ExpandUntilCap;
}
