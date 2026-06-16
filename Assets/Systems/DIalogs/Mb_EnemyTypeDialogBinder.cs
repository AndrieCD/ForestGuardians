// Mb_EnemyTypeDialogBinder.cs
// Plays a dialog sequence the FIRST TIME a specific CuBot type spawns —
// across the entire stage run, not just the current wave.
// Lives on the Stage GameObject.
//
// HOW IT WORKS:
//   - Subscribes to Mb_WaveManager.OnEnemySpawned (fires per individual spawn).
//   - Matches the spawned GameObject's name against each entry's EnemyPrefabName.
//   - Fires the sequence once per entry — uses a HashSet to track which names
//     have already triggered so repeats are ignored.
//
// INSPECTOR SETUP:
//   - Fill EnemyDialogs list with one entry per enemy type you want to narrate.
//   - EnemyPrefabName: must match the GameObject name as it appears in the scene
//     (i.e. the prefab name — e.g. "Chopper", "Minny", "Hunter").
//     Unity appends "(Clone)" to instantiated objects but CuBots use pooling,
//     so the name should match the pool child name exactly.
//     Check the pool child names in the Hierarchy and match them here.
//   - Sequence: the SO_DialogSequence to enqueue on first spawn of that type.

using System.Collections.Generic;
using UnityEngine;

public class Mb_EnemyTypeDialogBinder : MonoBehaviour
{
    [Header("Enemy Type Dialog Entries")]
    [SerializeField]
    private List<EnemyTypeDialogEntry> EnemyDialogs
        = new List<EnemyTypeDialogEntry>();

    // Tracks which enemy type names have already fired their dialog this stage
    private HashSet<string> _triggered = new HashSet<string>();


    private void OnEnable()
    {
        Mb_WaveManager.OnEnemySpawned += HandleEnemySpawned;
    }

    private void OnDisable()
    {
        Mb_WaveManager.OnEnemySpawned -= HandleEnemySpawned;
    }


    private void HandleEnemySpawned(GameObject enemy)
    {
        if (enemy == null) return;
        if (Mb_DialogManager.Instance == null) return;

        string enemyName = enemy.name;

        foreach (EnemyTypeDialogEntry entry in EnemyDialogs)
        {
            // Skip if this type has already triggered, name is empty, or sequence missing
            if (string.IsNullOrEmpty(entry.EnemyPrefabName)) continue;
            if (entry.Sequence == null) continue;

            // Match by checking if the spawned name contains the entry name —
            // handles cases where pool children have slight suffix differences
            if (enemyName.Contains(entry.EnemyPrefabName) && !_triggered.Contains(entry.EnemyPrefabName))
            {
                _triggered.Add(entry.EnemyPrefabName);
                Mb_DialogManager.Instance.EnqueueSequence(entry.Sequence);
            }
        }
    }
}


// -------------------------------------------------------------------------
// Supporting Types
// -------------------------------------------------------------------------

[System.Serializable]
public struct EnemyTypeDialogEntry
{
    [Tooltip("Must match the GameObject name of the CuBot as it appears in the " +
             "Hierarchy under the pool. Check your pool children and copy the name exactly.")]
    public string EnemyPrefabName;

    [Tooltip("Sequence to play the first time this enemy type appears in the stage.")]
    public SO_DialogSequence Sequence;
}