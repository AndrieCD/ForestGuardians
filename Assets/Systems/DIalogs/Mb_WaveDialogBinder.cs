// Mb_WaveDialogBinder.cs
// Companion component that bridges Mb_WaveManager events to Mb_DialogManager.
// Lives on the Stage GameObject alongside Mb_WaveManager.
//
// WHY A SEPARATE COMPONENT:
//   Mb_WaveManager owns wave flow logic — it should not know about dialog.
//   This binder subscribes to wave events and forwards sequences to the dialog
//   system, keeping both components clean and independently testable.
//
// HOW IT WORKS:
//   - Fill the WaveDialogs list in the Inspector with WaveDialogEntry structs.
//   - Each entry specifies a wave index, a trigger type, and a sequence to play.
//   - OnWaveStart fires the OnWaveStart entries for that wave index.
//   - OnFirstEnemySpawned fires once per wave when the first enemy is spawned —
//     subsequent spawns in the same wave are ignored.
//
// INSPECTOR SETUP:
//   - Add this component to the Stage GameObject (same as Mb_WaveManager).
//   - Populate WaveDialogs with entries for each wave that needs dialog.
//   - Leave the list empty for waves that have no dialog — no entries = no dialog.

using System;
using System.Collections.Generic;
using UnityEngine;

public class Mb_WaveDialogBinder : MonoBehaviour
{
    #region Inspector Fields    //----------------------------------------

    [Header("Wave Dialog Entries")]
    [Tooltip("One entry per wave/trigger combination that should play dialog. " +
             "Multiple entries with the same WaveIndex but different Triggers are allowed.")]
    [SerializeField] private List<WaveDialogEntry> WaveDialogs = new List<WaveDialogEntry>();

    #endregion                  //----------------------------------------


    #region Private State       //----------------------------------------

    // Tracks whether the first-enemy-spawned trigger has already fired this wave.
    // Resets at the start of each new wave so the trigger can fire again next wave.
    private bool _firstEnemySpawnedThisWave = false;

    // Cached current wave index — used by the spawn handler to know which
    // wave entries to check without needing a reference to Mb_WaveManager.
    private int _currentWaveIndex = -1;

    #endregion                  //----------------------------------------


    #region Unity Lifecycle     //----------------------------------------

    private void OnEnable()
    {
        Mb_WaveManager.OnWaveStart += HandleWaveStart;
        Mb_WaveManager.OnEnemySpawned += HandleEnemySpawned;
    }


    private void OnDisable()
    {
        Mb_WaveManager.OnWaveStart -= HandleWaveStart;
        Mb_WaveManager.OnEnemySpawned -= HandleEnemySpawned;
    }

    #endregion                  //----------------------------------------


    #region Event Handlers      //----------------------------------------

    private void HandleWaveStart(int waveIndex)
    {
        _currentWaveIndex = waveIndex;

        // Reset the first-spawn flag so OnFirstEnemySpawned can fire again this wave
        _firstEnemySpawnedThisWave = false;

        // Fire all OnWaveStart entries registered for this wave index
        FireEntries(waveIndex, WaveDialogTrigger.OnWaveStart);
    }


    private void HandleEnemySpawned(GameObject enemy)
    {
        // OnFirstEnemySpawned fires exactly once per wave — ignore all subsequent spawns
        if (_firstEnemySpawnedThisWave) return;

        _firstEnemySpawnedThisWave = true;

        // Fire all OnFirstEnemySpawned entries registered for the current wave index
        FireEntries(_currentWaveIndex, WaveDialogTrigger.OnFirstEnemySpawned);
    }

    #endregion                  //----------------------------------------


    #region Internals           //----------------------------------------

    // Enqueues all sequences whose WaveIndex and Trigger match the given arguments.
    // Multiple entries can match — they are enqueued in list order.
    private void FireEntries(int waveIndex, WaveDialogTrigger trigger)
    {
        if (Mb_DialogManager.Instance == null)
        {
            Debug.LogWarning("[Mb_WaveDialogBinder] No Mb_DialogManager instance found in scene.");
            return;
        }

        foreach (WaveDialogEntry entry in WaveDialogs)
        {
            if (entry.WaveIndex == waveIndex && entry.Trigger == trigger)
            {
                if (entry.Sequence == null)
                {
                    Debug.LogWarning($"[Mb_WaveDialogBinder] Entry for wave {waveIndex} / " +
                                     $"{trigger} has no sequence assigned — skipping.");
                    continue;
                }

                Mb_DialogManager.Instance.EnqueueSequence(entry.Sequence);
            }
        }
    }

    #endregion                  //----------------------------------------
}


// -------------------------------------------------------------------------
// Supporting Types
// -------------------------------------------------------------------------

/// <summary>
/// Pairs a wave index and trigger type with the dialog sequence to play.
/// Add one entry per wave/trigger combination in the Inspector.
/// </summary>
[Serializable]
public struct WaveDialogEntry
{
    [Tooltip("The wave index this entry responds to. " +
             "Matches Mb_WaveManager.CurrentWaveIndex (0-based).")]
    public int WaveIndex;

    [Tooltip("When within the wave this sequence should play.")]
    public WaveDialogTrigger Trigger;

    [Tooltip("The dialog sequence to enqueue when this entry fires.")]
    public SO_DialogSequence Sequence;
}


/// <summary>
/// Determines at what point during a wave the dialog sequence fires.
/// </summary>
public enum WaveDialogTrigger
{
    // Fires when Mb_WaveManager.OnWaveStart is raised for this wave index.
    OnWaveStart,

    // Fires once when the first enemy is spawned this wave.
    // Subsequent spawns in the same wave do not re-trigger this.
    OnFirstEnemySpawned
}