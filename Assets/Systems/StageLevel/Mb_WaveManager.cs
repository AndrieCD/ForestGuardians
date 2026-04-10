using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mb_WaveManager : MonoBehaviour
{
    // StageManager reference
    [SerializeField] Mb_StageManager _stageManager;
    [SerializeField] List<Transform> _spawnPoints; // List of spawn points for enemies, should be assigned in the Inspector
    [SerializeField] GameObject CuBotPool; // Reference to the CuBot object pool, should be assigned in the Inspector

    // StageData contains a list of waves, and each wave contains a list of enemy spawn data. 
    SO_StageData TestStageData;

    // Wave Tracking
    public int CurrentWaveIndex = -1; // Tracks the current wave index
    List<GameObject> ActiveEnemies = new List<GameObject>( ); // List to track active enemies in the current wave
    bool IsWaveActive = false; // Flag to indicate if a wave is currently active

    // EVENTS
    public static event Action<int> OnWaveStart; // Event triggered at the start of a wave
    public static event Action<int> OnWaveEnd;   // Event triggered at the end of a wave


    private void Awake( )
    {
        TestStageData = _stageManager.GetStageData( );
    }


    #region  OnEnable/OnDisable
    private void OnEnable( )
    {
        MB_CuBotBase.OnCuBotDeath += HandleCuBotDeath; // Subscribe to the CuBot death event to track active enemies
        Mb_StageManager.OnStageStart += StarWaveManager; // Subscribe to the stage start event to begin the wave management process
    }
    private void OnDisable( )
    {
        MB_CuBotBase.OnCuBotDeath -= HandleCuBotDeath; // Unsubscribe from the CuBot death event
        Mb_StageManager.OnStageStart -= StarWaveManager; // Unsubscribe from the stage start event
    }
    #endregion


    private void HandleCuBotDeath()
    {
        // When a CuBot dies, remove it from the active enemies list
        foreach (GameObject enemy in ActiveEnemies)
        {
            if (!enemy.activeInHierarchy)
            {
                ActiveEnemies.Remove(enemy);
                break;
            }
        }

        // If there are no more active enemies, end the wave
        if (ActiveEnemies.Count == 0 && IsWaveActive)
        {
            EndWave( );
        }
    }


    public void StarWaveManager( )
    {
        Debug.Log($"Start Wave Manager");
        StartPreparationPhase( );
    }


    #region PreparationPhase
    private void StartPreparationPhase( )
    {
        // During this phase, the player selects available upgrades
        // Display UI for upgrade selection.
        // Wait for 15s max (or until player confirms selection) when selecting upgrades.
        // Wait only 10s if there are no upgrades available for selection, or after player confirms selection.

        StartCoroutine(PreparationPhaseRoutine(1));
    }

    IEnumerator PreparationPhaseRoutine(float duration)
    {
        // [Display Upgrade Selection UI]
        Debug.Log("UpgradeRoutine +  started. Duration: " + duration + " seconds.");
        yield return new WaitForSeconds(duration);

        // [Hide Upgrade Selection UI]
        Debug.Log($"UpgradeRoutine +  ended after {duration} seconds.");


        // Start another timer to end the preparation phase
        StartCoroutine(LastPreparationRoutine(1));
    }

    IEnumerator LastPreparationRoutine(float duration)
    {
        Debug.Log("LastPreparationRoutine started. Duration: " + duration + " seconds.");
        yield return new WaitForSeconds(duration);
        // Transition to the wave initiation phase
        StartWaveInitiationPhase( );
    }
    #endregion


    #region WaveInitiationPhase
    private void StartWaveInitiationPhase()
    {
        // Start the wave
        CurrentWaveIndex++; // Increment the wave index to move to the next wave
        IsWaveActive = true; // Set the wave active flag to true
        OnWaveStart?.Invoke(CurrentWaveIndex); // Trigger the wave start event

        // Spawn wave
        StartCoroutine(SpawnWaveRoutine( ));
    }

    private IEnumerator SpawnWaveRoutine( )
    {
        var currentWaveData = TestStageData.WaveDataList[CurrentWaveIndex];
        var enemySpawnDataList = currentWaveData.enemyDataList;

        foreach (var CuBotEntry in enemySpawnDataList)
        {
            var enemyType = CuBotEntry.enemyType;
            var count = CuBotEntry.count;

            for (int i = 0; i < count; i++)
            {
                SpawnSingleEnemy(enemyType);

                yield return new WaitForSeconds(0.5f); // REAL delay
            }

            yield return new WaitForSeconds(0.5f); // Delay between types
        }
        Debug.Log($"Finished spawning wave {CurrentWaveIndex}. Total enemies spawned: {ActiveEnemies.Count}");
    }

    private void SpawnSingleEnemy(GameObject enemyType)
    {
        var spawnPoint = _spawnPoints[UnityEngine.Random.Range(0, _spawnPoints.Count)];

        foreach (Transform child in CuBotPool.transform)
        {
            if (!child.gameObject.activeInHierarchy &&
                child.gameObject.name == enemyType.name)
            {
                child.position = spawnPoint.position;
                child.rotation = spawnPoint.rotation;
                child.gameObject.SetActive(true);
                ActiveEnemies.Add(child.gameObject);
                break;
            }
        }
    }
#endregion
   
    private void EndWave()
    {
        Debug.Log($"Wave {CurrentWaveIndex} cleared! Ending wave.");
        // End the wave
        IsWaveActive = false; // Set the wave active flag to false
        OnWaveEnd?.Invoke(CurrentWaveIndex); // Trigger the wave end event

        // Transition to the wave resolution phase
        StartWaveResolutionPhase( );
    }


    private void StartWaveResolutionPhase( )
    {
        // Check if the player has cleared the final wave
        if (CurrentWaveIndex >= TestStageData.WaveDataList.Count - 1)
        {
            Debug.Log("All waves cleared! Stage complete!");
            _stageManager.EndStage( );
            return;
        }

        // Initialize wave rewards based on finished wave number (ability upgrades / augments)
        // ...

        // Start the preparation phase for the next wave after a short delay
        StartCoroutine(NextWavePreparationRoutine(3f));
    }

    private IEnumerator NextWavePreparationRoutine(float v)
    {
        Debug.Log("NextWavePreparationRoutine + started. Duration: " + v + " seconds.");
        yield return new WaitForSeconds(v);
        StartPreparationPhase( );
    }
}
