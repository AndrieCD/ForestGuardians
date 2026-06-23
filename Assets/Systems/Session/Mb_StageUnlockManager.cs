// Mb_StageUnlockManager.cs
// Persistent singleton — survives scene loads via DontDestroyOnLoad.
// Single source of truth for stage unlock state at runtime.
//
// RESPONSIBILITIES:
//   - Load Sc_StageSaveData on Awake()
//   - Expose IsUnlocked(int stageNumber) for UI to query
//   - Expose UnlockStage(int stageNumber) to set and persist unlock state
//   - Detect victory (via OnAllWavesCleared) and unlock the next stage on StageEnd
//
// VICTORY DETECTION PATTERN:
//   Mb_WaveManager.OnAllWavesCleared fires ONLY on a clean victory —
//   not on defeat. We set _victoryOccurred = true when it fires.
//   On Mb_StageManager.OnStageEnd, if _victoryOccurred is true, we unlock
//   the next stage and clear the flag. This avoids needing a new event or
//   modifying any existing script.
//
// STAGE NUMBER SOURCE:
//   The manager reads Sc_RunSession.SelectedStageNumber to know which stage
//   just completed. This is set by Mb_GuardianSelectionUI before loading
//   the stage scene and cleared after the stage ends.
//
// Inspector setup:
//   - This component lives on the Bootstrap GameObject alongside GameManager.
//   - No fields need to be assigned — everything is resolved at runtime.

using System;
using UnityEngine;

public class Mb_StageUnlockManager : MonoBehaviour
{

    [Header("Build Limits")]
    [Tooltip("Highest stage number currently playable in this build. " +
             "Set to 1 while only Stage 1 exists — prevents Stage 2/3 from " +
             "ever being unlocked even if HandleStageEnd fires. " +
             "TODO: Raise to 2, then 3 as those stages are completed.")]
    [SerializeField] private int MaxAvailableStage = 1;


    // -------------------------------------------------------------------------
    // Singleton
    // -------------------------------------------------------------------------

    public static Mb_StageUnlockManager Instance { get; private set; }


    // -------------------------------------------------------------------------
    // Events
    // -------------------------------------------------------------------------

    // Fired when any stage transitions from locked to unlocked.
    // Mb_StageSelectionUI subscribes to refresh gem visuals if the player
    // returns to the menu after completing a stage in the same session.
    public static event Action<int> OnStageUnlocked;


    // -------------------------------------------------------------------------
    // Runtime State
    // -------------------------------------------------------------------------

    // Loaded from disk on Awake — updated in memory and re-saved on every change
    private Sc_StageSaveData _saveData;

    // Set true by HandleAllWavesCleared — tells HandleStageEnd that this
    // stage ended in victory and the next stage should be unlocked.
    // Cleared immediately after use so it never bleeds into the next session.
    private bool _victoryOccurred = false;


    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Singleton setup — one StageUnlockManager persists for the entire session
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _saveData = Sc_StageSaveData.Load();

        Debug.Log($"[Mb_StageUnlockManager] Initialized. " +
                  $"S1:{_saveData.Stage1Unlocked} " +
                  $"S2:{_saveData.Stage2Unlocked} " +
                  $"S3:{_saveData.Stage3Unlocked}");
    }


    private void OnEnable()
    {
        // Subscribe to victory and stage-end events so we can detect a completed stage.
        // Both events are static — safe to subscribe from a DontDestroyOnLoad object
        // because the events themselves live on static classes, not scene objects.
        Mb_WaveManager.OnAllWavesCleared += HandleAllWavesCleared;
        Mb_StageManager.OnStageEnd += HandleStageEnd;
    }


    private void OnDisable()
    {
        Mb_WaveManager.OnAllWavesCleared -= HandleAllWavesCleared;
        Mb_StageManager.OnStageEnd -= HandleStageEnd;
    }


    // -------------------------------------------------------------------------
    // Public Read API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true if the given stage number (1, 2, or 3) is unlocked.
    /// Safe to call from any UI before a stage scene is loaded.
    /// Always returns true for Stage 1.
    /// </summary>
    public bool IsUnlocked(int stageNumber)
    {
        // Tutorial (stage 4) is not part of the sequential unlock chain —
        // it's always available regardless of MaxAvailableStage.
        if (stageNumber == 4) return _saveData.IsUnlocked(stageNumber);

        if (stageNumber > MaxAvailableStage) return false;
        return _saveData.IsUnlocked(stageNumber);
    }


    // -------------------------------------------------------------------------
    // Public Write API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Unlocks the given stage, saves to disk, and fires OnStageUnlocked.
    /// Safe to call if the stage is already unlocked — does nothing in that case.
    /// </summary>
    public void UnlockStage(int stageNumber)
    {
        // Guard: don't re-unlock or re-save if already unlocked
        if (_saveData.IsUnlocked(stageNumber))
        {
            Debug.Log($"[Mb_StageUnlockManager] Stage {stageNumber} is already unlocked.");
            return;
        }

        _saveData.Unlock(stageNumber);
        Sc_StageSaveData.Save(_saveData);

        OnStageUnlocked?.Invoke(stageNumber);

        Debug.Log($"[Mb_StageUnlockManager] Stage {stageNumber} unlocked and saved.");
    }


    // -------------------------------------------------------------------------
    // Event Handlers
    // -------------------------------------------------------------------------

    // Fired by Mb_WaveManager when all waves are cleared — victory only.
    // We just set the flag here; the actual unlock happens in HandleStageEnd
    // so it runs after any end-sequence cleanup.
    private void HandleAllWavesCleared()
    {
        _victoryOccurred = true;

        Debug.Log("[Mb_StageUnlockManager] Victory detected — " +
                  "will unlock next stage on StageEnd.");
    }


    // Fired by Mb_StageManager.EndStage() for both victory and defeat.
    // Only acts if _victoryOccurred was set by HandleAllWavesCleared.
    private void HandleStageEnd()
    {
        if (!_victoryOccurred)
        {
            // Defeat path — nothing to unlock, just clear state
            Debug.Log("[Mb_StageUnlockManager] Stage ended without victory — no unlock.");
            return;
        }

        _victoryOccurred = false;

        // Determine which stage just completed from the run session.
        // Sc_RunSession.SelectedStageNumber is set by Mb_GuardianSelectionUI
        // before the stage scene loads and is still valid here.
        int completedStage = Sc_RunSession.SelectedStageNumber;

        if (completedStage <= 0)
        {
            // This can happen if the stage was launched directly in the Editor
            // without going through the main menu flow — safe to skip.
            Debug.LogWarning("[Mb_StageUnlockManager] SelectedStageNumber is 0 — " +
                             "stage may have been launched directly. Skipping unlock.");
            return;
        }

        int nextStage = completedStage + 1;

        // Only stages 1 and 2 can unlock a next stage — Stage 3 is the final stage
        if (nextStage > 3)
        {
            Debug.Log($"[Mb_StageUnlockManager] Stage {completedStage} is the final stage. " +
                      "No further stages to unlock.");
            return;
        }

        // TEMP BUILD GUARD: don't unlock stages that don't exist in this build yet
        if (nextStage > MaxAvailableStage)
        {
            Debug.Log($"[Mb_StageUnlockManager] Stage {nextStage} is beyond MaxAvailableStage " +
                      $"({MaxAvailableStage}) — skipping unlock for this build.");
            return;
        }

        UnlockStage(nextStage);

        // Clear run session after the unlock is saved so nothing downstream
        // accidentally reads a stale stage number
        Sc_RunSession.Clear();
    }


    // -------------------------------------------------------------------------
    // Debug Utility
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resets all stage unlock progress. Editor/debug use only.
    /// </summary>
    [ContextMenu("DEBUG — Reset Stage Save")]
    private void DebugResetSave()
    {
        _saveData = Sc_StageSaveData.Reset();
        Debug.Log("[Mb_StageUnlockManager] Stage save reset.");
    }
}