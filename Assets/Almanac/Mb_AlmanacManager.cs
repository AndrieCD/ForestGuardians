// Mb_AlmanacManager.cs
// Persistent singleton — survives scene loads via DontDestroyOnLoad.
// Single source of truth for almanac unlock state at runtime.
//
// RESPONSIBILITIES:
//   - Load almanac save data on Awake()
//   - Track which entries are unlocked and how many times completed
//   - Apply and reapply stat bonuses to the Guardian via Mb_StatBlock
//   - Fire events so UI and other systems stay in sync without polling
//   - Save to disk whenever state changes
//
// STAT BONUS REAPPLICATION PATTERN:
//   On every scene load where a Guardian is present, call
//   ReapplyAllBonuses(Mb_StatBlock guardianStats) to:
//     1. Strip all ModifierSource.Almanac modifiers (RemoveAllFromSource)
//     2. Rebuild fresh from save data
//   This prevents double-stacking across scene loads while keeping
//   augment, ability, and wave-scaling modifiers untouched.
//
// Inspector setup:
//   - Drag all 13 SO_WildlifeEntry assets into AllEntries
//   - This component lives on the Bootstrap GameObject (same as GameManager)
//   - Call ReapplyAllBonuses() from the stage's initialization sequence
//     once the Guardian's Mb_StatBlock is ready
//     // TODO: Wire ReapplyAllBonuses() call from Mb_StageManager or
//     //       GameInitializer once Guardian is confirmed present in scene

using System;
using System.Collections.Generic;
using UnityEngine;

public class Mb_AlmanacManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Singleton
    // -------------------------------------------------------------------------

    public static Mb_AlmanacManager Instance { get; private set; }


    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    [Header("Wildlife Entries")]
    [Tooltip("Drag all 13 SO_WildlifeEntry ScriptableObjects here. " +
             "Order does not matter — lookups are done by CommonName.")]
    [SerializeField] private List<SO_WildlifeEntry> AllEntries = new List<SO_WildlifeEntry>();


    // -------------------------------------------------------------------------
    // Runtime State
    // -------------------------------------------------------------------------

    // Loaded from disk on Awake — updated in memory and re-saved on every change
    private Sc_AlmanacSaveData _saveData;

    // Cached reference to the Guardian's stat block for applying bonuses.
    // Set by calling ReapplyAllBonuses() from the stage initialization sequence.
    // Null outside of a stage — bonus application is silently skipped when null.
    private Mb_StatBlock _guardianStats;


    // -------------------------------------------------------------------------
    // Events
    // -------------------------------------------------------------------------

    // Fired when an entry transitions from locked to unlocked for the first time.
    // Mb_AlmanacUI subscribes to refresh the compendium card.
    // Mb_WildlifeHotbarUI subscribes to show a completion indicator.
    public static event Action<SO_WildlifeEntry> OnEntryUnlocked;

    // Fired when an already-unlocked entry is completed again in a later run.
    // The 5% repeat bonus has already been applied when this fires.
    public static event Action<SO_WildlifeEntry> OnRepeatCompleted;

    // Fired when admin/debug commands change many entries at once.
    public static event Action OnAlmanacProgressChanged;


    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Singleton setup — one AlmanacManager persists for the entire session
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Load save data immediately — all other systems that call IsUnlocked()
        // or GetCompletionCount() are guaranteed to get accurate data from Start()
        // onward because Awake() always runs before Start() on other objects
        _saveData = Sc_AlmanacSaveData.Load();

        Debug.Log("[Mb_AlmanacManager] Initialized. " +
                  $"Unlocked entries: {_saveData.UnlockedEntries.Count}/{AllEntries.Count}");
    }


    // -------------------------------------------------------------------------
    // Public Read API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true if this entry has been unlocked at least once.
    /// Safe to call before a Guardian is present in the scene.
    /// </summary>
    public bool IsUnlocked(SO_WildlifeEntry entry)
    {
        return _saveData.IsUnlocked(entry.CommonName);
    }


    /// <summary>
    /// Returns how many times this entry has been fully completed.
    /// 0 = never unlocked, 1 = unlocked once, 2+ = repeated completions.
    /// </summary>
    public int GetCompletionCount(SO_WildlifeEntry entry)
    {
        return _saveData.GetCount(entry.CommonName);
    }


    /// <summary>
    /// Returns a read-only view of all 13 wildlife entries.
    /// Used by Mb_AlmanacUI to populate the compendium grid.
    /// </summary>
    public IReadOnlyList<SO_WildlifeEntry> GetAllEntries()
    {
        return AllEntries.AsReadOnly();
    }


    // -------------------------------------------------------------------------
    // Public Write API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unlocks an entry for the first time:
    ///   1. Marks it unlocked in save data
    ///   2. Records completion count = 1
    ///   3. Applies the full stat bonus to the Guardian
    ///   4. Saves to disk
    ///   5. Fires OnEntryUnlocked
    ///
    /// If the entry is already unlocked, this does nothing — call
    /// RecordRepeatCompletion() instead for subsequent completions.
    /// </summary>
    public void UnlockEntry(SO_WildlifeEntry entry)
    {
        if (_saveData.IsUnlocked(entry.CommonName))
        {
            Debug.LogWarning($"[Mb_AlmanacManager] UnlockEntry called on already-unlocked " +
                             $"entry '{entry.CommonName}'. Use RecordRepeatCompletion() instead.");
            return;
        }

        // Mark as unlocked and set completion count to 1
        _saveData.UnlockedEntries.Add(entry.CommonName);
        _saveData.SetCount(entry.CommonName, 1);

        // Apply the full stat bonus to the Guardian if one is in the scene
        ApplyBonus(entry.StatBonus, entry.CommonName, isRepeat: false);

        // Persist immediately — we never want to lose an unlock
        Sc_AlmanacSaveData.Save(_saveData);

        OnEntryUnlocked?.Invoke(entry);

        Debug.Log($"[Mb_AlmanacManager] '{entry.CommonName}' unlocked! " +
                  $"Bonus applied: {entry.StatBonus.TargetStat} +{entry.StatBonus.Value}");
    }


    /// <summary>
    /// Records a repeat completion for an already-unlocked entry:
    ///   1. Increments the completion count
    ///   2. Applies 5% of the original stat bonus value (additive, permanent)
    ///   3. Saves to disk
    ///   4. Fires OnRepeatCompleted
    ///
    /// The repeat bonus is always 5% of StatBonus.Value — not compounding off
    /// the running total — so each repeat adds the same fixed increment.
    /// </summary>
    public void RecordRepeatCompletion(SO_WildlifeEntry entry)
    {
        if (!_saveData.IsUnlocked(entry.CommonName))
        {
            Debug.LogWarning($"[Mb_AlmanacManager] RecordRepeatCompletion called on locked " +
                             $"entry '{entry.CommonName}'. Use UnlockEntry() for first unlock.");
            return;
        }

        int newCount = _saveData.GetCount(entry.CommonName) + 1;
        _saveData.SetCount(entry.CommonName, newCount);

        // Repeat bonus = fixed 5% of the original SO stat value
        // We always read from entry.StatBonus.Value (the SO) — not from any
        // running total — so every repeat grants the same increment regardless
        // of how many times the entry has been completed before
        ApplyBonus(entry.StatBonus, entry.CommonName, isRepeat: true);

        Sc_AlmanacSaveData.Save(_saveData);

        OnRepeatCompleted?.Invoke(entry);

        float repeatBonusValue = entry.StatBonus.Value * 0.05f;
        Debug.Log($"[Mb_AlmanacManager] '{entry.CommonName}' completed again " +
                  $"(x{newCount}). Repeat bonus: {entry.StatBonus.TargetStat} " +
                  $"+{repeatBonusValue} (5% of {entry.StatBonus.Value})");
    }


    /// <summary>
    /// Unlocks every configured almanac entry once. Admin/demo use only.
    /// </summary>
    public void FillAllEntriesForDebug()
    {
        _saveData = new Sc_AlmanacSaveData();

        foreach (SO_WildlifeEntry entry in AllEntries)
        {
            if (entry == null) continue;
            if (string.IsNullOrWhiteSpace(entry.CommonName)) continue;

            _saveData.UnlockedEntries.Add(entry.CommonName);
            _saveData.SetCount(entry.CommonName, 1);
        }

        Sc_AlmanacSaveData.Save(_saveData);

        if (_guardianStats != null)
            ReapplyAllBonuses(_guardianStats);

        OnAlmanacProgressChanged?.Invoke();

        Debug.Log($"[Mb_AlmanacManager] Debug filled almanac. " +
                  $"Unlocked entries: {_saveData.UnlockedEntries.Count}/{AllEntries.Count}");
    }


    /// <summary>
    /// Clears every almanac unlock and completion count. Admin/demo use only.
    /// </summary>
    public void ClearAllEntriesForDebug()
    {
        _saveData = Sc_AlmanacSaveData.Reset();

        _guardianStats?.RemoveAllFromSource(ModifierSource.Almanac);

        OnAlmanacProgressChanged?.Invoke();

        Debug.Log("[Mb_AlmanacManager] Debug cleared almanac. Unlocked entries: 0.");
    }


    /// <summary>
    /// Strips all existing Almanac modifiers from the Guardian's StatBlock,
    /// then rebuilds every bonus from save data fresh.
    ///
    /// Call this once per stage load after the Guardian's Mb_StatBlock is ready:
    ///   Mb_AlmanacManager.Instance.ReapplyAllBonuses(guardianStats);
    ///
    /// This is the only safe way to restore almanac bonuses across scene loads
    /// without double-stacking them.
    /// </summary>
    public void ReapplyAllBonuses(Mb_StatBlock guardianStats)
    {
        _guardianStats = guardianStats;

        // Step 1: Strip all almanac modifiers cleanly — leaves augment,
        // ability, and wave-scaling modifiers completely untouched
        _guardianStats.RemoveAllFromSource(ModifierSource.Almanac);

        // Step 2: Rebuild from save data — one modifier per unlocked entry,
        // plus one modifier per repeat completion on top of that
        int totalApplied = 0;

        foreach (SO_WildlifeEntry entry in AllEntries)
        {
            if (!_saveData.IsUnlocked(entry.CommonName)) continue;

            int completionCount = _saveData.GetCount(entry.CommonName);

            // First unlock — full stat bonus
            BuildAndApplyModifier(
                entry.StatBonus.TargetStat,
                entry.StatBonus.Value,
                entry.StatBonus.Type,
                $"Almanac — {entry.CommonName}"
            );

            // Repeat completions — each adds 5% of the original bonus
            // completionCount of 1 means unlocked once, no repeats yet
            int repeatCount = completionCount - 1;
            if (repeatCount > 0)
            {
                float repeatBonus = entry.StatBonus.Value * 0.05f * repeatCount;
                BuildAndApplyModifier(
                    entry.StatBonus.TargetStat,
                    repeatBonus,
                    entry.StatBonus.Type,
                    $"Almanac — {entry.CommonName} (Repeat x{repeatCount})"
                );
            }

            totalApplied++;
        }

        Debug.Log($"[Mb_AlmanacManager] Reapplied bonuses for {totalApplied} unlocked entries.");
    }


    // -------------------------------------------------------------------------
    // Internal Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Applies a bonus modifier to the Guardian's StatBlock.
    /// For first unlocks, applies the full StatBonus.Value.
    /// For repeats, applies 5% of the original StatBonus.Value.
    /// Silently skips if no Guardian is registered yet.
    /// </summary>
    private void ApplyBonus(Sc_StatEffect bonusStat, string entryName, bool isRepeat)
    {
        if (_guardianStats == null)
        {
            // This is expected outside of a stage — bonuses will be
            // reapplied in full when ReapplyAllBonuses() is called at stage load
            Debug.Log($"[Mb_AlmanacManager] No Guardian registered — " +
                      $"bonus for '{entryName}' will be applied at next stage load.");
            return;
        }

        float value = isRepeat
            ? bonusStat.Value * 0.05f   // 5% of original for repeats
            : bonusStat.Value;          // Full value for first unlock

        string modifierName = isRepeat
            ? $"Almanac — {entryName} (Repeat)"
            : $"Almanac — {entryName}";

        BuildAndApplyModifier(bonusStat.TargetStat, value, bonusStat.Type, modifierName);
    }


    /// <summary>
    /// Builds a permanent Sc_Modifier tagged ModifierSource.Almanac
    /// and applies it to the cached Guardian StatBlock.
    /// All almanac bonuses are flat permanent modifiers — they never expire.
    /// </summary>
    private void BuildAndApplyModifier(
        StatType statType,
        float value,
        StatModType modType,
        string modifierName)
    {
        if (_guardianStats == null) return;

        var effect = new Sc_StatEffect(statType, value, modType);

        var modifier = new Sc_Modifier(
            modifierName,
            ModifierSource.Almanac,
            new System.Collections.Generic.List<Sc_StatEffect> { effect }
            // Duration omitted — defaults to float.PositiveInfinity (permanent)
        );

        _guardianStats.AddModifier(modifier);
    }


    // -------------------------------------------------------------------------
    // Debug Utility
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resets all almanac progress. Editor/debug use only — never expose to players.
    /// </summary>
    [ContextMenu("DEBUG — Reset Almanac Save")]
    private void DebugResetSave()
    {
        _saveData = Sc_AlmanacSaveData.Reset();

        // Strip all almanac bonuses from the current Guardian if one is present
        _guardianStats?.RemoveAllFromSource(ModifierSource.Almanac);

        Debug.Log("[Mb_AlmanacManager] Almanac save reset.");
    }
}
