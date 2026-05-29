// Mb_WaveManager.cs
// Owns all wave flow logic for a stage — preparation, combat, and resolution.
//
// PHASE FLOW (loops until all waves are cleared):
//
//   Preparation → Combat → Resolution → Preparation → ...
//
//   PREPARATION:
//     Fires OnRewardsPanelClosed is awaited if a rewards panel was opened by
//     Mb_RewardsManager. Once rewards are done (or if there were none), a countdown
//     begins. Fires OnPreparationTick every second so TopBar can display the timer.
//     When countdown hits zero, transitions to Combat.
//
//   COMBAT:
//     Increments wave index, fires OnWaveStart, spawns all CuBots with configurable
//     delays. Waits until all active enemies are dead, then transitions to Resolution.
//
//   RESOLUTION:
//     Fires OnWaveResolution so TopBar can display "WAVE X COMPLETE".
//     Holds for ResolutionDuration seconds. If this was the final wave, ends the stage.
//     Otherwise loops back to Preparation.
//
// TIMING FIELDS (all configurable in the Inspector):
//   PreparationDuration      — countdown seconds before combat starts
//   ResolutionDuration       — seconds "WAVE X COMPLETE" is shown
//   SpawnInterval            — delay between individual CuBot spawns
//   SpawnTypeSeparatorInterval — extra delay between different CuBot types

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum WavePhase
{
    Preparation,    // Rewards done, counting down to next wave
    Combat,         // Enemies spawning and alive
    Resolution      // All enemies dead, showing wave complete
}

public class Mb_WaveManager : MonoBehaviour
{
    #region Inspector Fields        //----------------------------------------

    [Header("References")]
    [SerializeField] private Mb_StageManager _stageManager;

    [Tooltip("All possible spawn points. One is chosen at random per enemy.")]
    [SerializeField] private List<Transform> _spawnPoints;

    [Tooltip("The parent GameObject that holds all pooled CuBot instances.")]
    [SerializeField] private GameObject CuBotPool;

    [Header("Phase Timing")]
    [Tooltip("Seconds the player has to prepare before the next wave begins. " +
             "Timer starts after the rewards panel closes (or immediately if no reward).")]
    [SerializeField] private float PreparationDuration = 10f;

    [Tooltip("Seconds the 'WAVE X COMPLETE' label is shown before looping back to preparation.")]
    [SerializeField] private float ResolutionDuration = 3f;

    [Header("Spawn Timing")]
    [Tooltip("Delay in seconds between spawning individual CuBots of the same type.")]
    [SerializeField] private float SpawnInterval = 0.5f;

    [Tooltip("Extra delay in seconds between finishing one CuBot type and starting the next.")]
    [SerializeField] private float SpawnTypeSeparatorInterval = 0.5f;

    #endregion                      //----------------------------------------


    #region Events                  //----------------------------------------

    // Fired when the active phase changes — TopBar listens to update its label
    public static event Action<WavePhase> OnPhaseChanged;

    // Fired once per second during preparation with the remaining countdown value.
    // TopBar uses this to update the "NEXT WAVE IN X" timer label.
    public static event Action<float> OnPreparationTick;

    // Fired when a new wave's combat begins (0-based index)
    public static event Action<int> OnWaveStart;

    // Fired when all enemies in a wave are dead (0-based index)
    public static event Action<int> OnWaveEnd;

    // Fired each time a single CuBot is activated from the pool
    public static event Action<GameObject> OnEnemySpawned;

    // Fired when all enemies for this wave have been placed into the scene
    public static event Action<int> OnWaveSpawnComplete;

    // Fires when all waves have been cleared and the stage is ending
    public static event Action OnAllWavesCleared;


    #endregion                      //----------------------------------------


    #region Runtime State           //----------------------------------------

    // Fetched from StageManager in Awake — same asset Mb_RewardsManager references
    private SO_StageData _stageData;

    // 0-based. Starts at -1 so the first increment brings it to 0.
    public int CurrentWaveIndex { get; private set; } = -1;

    // All CuBots currently alive this wave — removed on death
    private List<GameObject> _activeEnemies = new List<GameObject>();

    // Set true while enemies are alive — guards EndWave from firing twice
    private bool _isWaveActive = false;

    // Set true when Mb_RewardsManager opens a panel — WaveManager waits for
    // OnRewardsPanelClosed before starting the preparation countdown
    private bool _rewardsPanelOpen = false;

    #endregion                      //----------------------------------------


    #region Unity Lifecycle         //----------------------------------------

    private void Awake()
    {
        _stageData = _stageManager.GetStageData();
    }

    private void OnEnable()
    {
        MB_CuBotBase.OnCuBotDeath += HandleCuBotDeath;
        Mb_StageManager.OnStageStart += HandleStageStart;
        Mb_RewardsManager.OnRewardsPanelOpened += HandleRewardsPanelOpened;
        Mb_RewardsManager.OnRewardsPanelClosed += HandleRewardsPanelClosed;
    }

    private void OnDisable()
    {
        MB_CuBotBase.OnCuBotDeath -= HandleCuBotDeath;
        Mb_StageManager.OnStageStart -= HandleStageStart;
        Mb_RewardsManager.OnRewardsPanelOpened -= HandleRewardsPanelOpened;
        Mb_RewardsManager.OnRewardsPanelClosed -= HandleRewardsPanelClosed;
    }

    #endregion                      //----------------------------------------


    #region Phase: Entry            //----------------------------------------

    private void HandleStageStart()
    {
        Debug.Log("[Mb_WaveManager] Stage started — beginning first preparation phase.");
        StartCoroutine(PreparationRoutine());
    }

    #endregion                      //----------------------------------------


    #region Phase: Preparation      //----------------------------------------

    private IEnumerator PreparationRoutine()
    {
        OnPhaseChanged?.Invoke(WavePhase.Preparation);

        // If a rewards panel is open, wait here until it closes.
        // Mb_RewardsManager sets _rewardsPanelOpen via events.
        while (_rewardsPanelOpen)
            yield return null;

        // Countdown — fire a tick event each second for the TopBar timer label
        float remaining = PreparationDuration;

        while (remaining > 0f)
        {
            OnPreparationTick?.Invoke(remaining);
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        // Clamp and fire one final tick at zero so the UI reads "0" cleanly
        OnPreparationTick?.Invoke(0f);

        StartCoroutine(CombatRoutine());
    }

    #endregion                      //----------------------------------------


    #region Phase: Combat           //----------------------------------------

    private IEnumerator CombatRoutine()
    {
        OnPhaseChanged?.Invoke(WavePhase.Combat);

        // Advance to the next wave
        CurrentWaveIndex++;
        _isWaveActive = true;
        _activeEnemies.Clear();

        OnWaveStart?.Invoke(CurrentWaveIndex);

        yield return StartCoroutine(SpawnWaveRoutine());

        // Spawning is done — now just wait for all enemies to die.
        // HandleCuBotDeath calls EndWave() which transitions to Resolution.
        // If enemies were already all dead before spawning finished (edge case
        // on very small waves), EndWave checks _isWaveActive so it's safe.
    }


    private IEnumerator SpawnWaveRoutine()
    {
        var currentWaveData = _stageData.WaveDataList[CurrentWaveIndex];

        foreach (var entry in currentWaveData.enemyDataList)
        {
            for (int i = 0; i < entry.count; i++)
            {
                SpawnSingleEnemy(entry.enemyType);
                yield return new WaitForSeconds(SpawnInterval);
            }

            yield return new WaitForSeconds(SpawnTypeSeparatorInterval);
        }

        OnWaveSpawnComplete?.Invoke(CurrentWaveIndex);
        Debug.Log($"[Mb_WaveManager] Wave {CurrentWaveIndex} spawn complete. " +
                  $"Active enemies: {_activeEnemies.Count}");
    }


    private void SpawnSingleEnemy(GameObject enemyType)
    {
        Transform spawnPoint = _spawnPoints[UnityEngine.Random.Range(0, _spawnPoints.Count)];

        foreach (Transform child in CuBotPool.transform)
        {
            if (!child.gameObject.activeInHierarchy &&
                child.gameObject.name == enemyType.name)
            {
                child.position = spawnPoint.position;
                child.rotation = spawnPoint.rotation;
                child.gameObject.SetActive(true);

                OnEnemySpawned?.Invoke(child.gameObject);
                _activeEnemies.Add(child.gameObject);
                break;
            }
        }
    }


    private void HandleCuBotDeath(GameObject deadEnemy)
    {
        _activeEnemies.Remove(deadEnemy);
        Debug.Log($"[Mb_WaveManager] CuBot died. Remaining: {_activeEnemies.Count}");

        if (_activeEnemies.Count == 0 && _isWaveActive)
            EndWave();
    }


    private void EndWave()
    {
        _isWaveActive = false;
        OnWaveEnd?.Invoke(CurrentWaveIndex);
        Debug.Log($"[Mb_WaveManager] Wave {CurrentWaveIndex} cleared.");

        StartCoroutine(ResolutionRoutine());
    }

    #endregion                      //----------------------------------------


    #region Phase: Resolution       //----------------------------------------

    private IEnumerator ResolutionRoutine()
    {
        OnPhaseChanged?.Invoke(WavePhase.Resolution);

        // Hold so TopBar can display "WAVE X COMPLETE" for the full duration
        yield return new WaitForSeconds(ResolutionDuration);

        // Final wave — end the stage instead of looping back
        if (CurrentWaveIndex >= _stageData.WaveDataList.Count - 1)
        {
            Debug.Log("[Mb_WaveManager] All waves cleared. Ending stage.");
            OnAllWavesCleared?.Invoke();
            _stageManager.EndStage();
            yield break;
        }

        // Loop back — Mb_RewardsManager will have already opened the panel
        // via its OnWaveEnd listener by the time we get here, so
        // PreparationRoutine's _rewardsPanelOpen check will gate the countdown
        // until the player chooses (or the rewards timer expires).
        StartCoroutine(PreparationRoutine());
    }

    #endregion                      //----------------------------------------


    #region Rewards Panel Sync      //----------------------------------------

    // These two handlers let PreparationRoutine know whether it needs to
    // wait before starting the countdown. RewardsManager fires these events
    // independently — WaveManager doesn't call RewardsManager directly.

    private void HandleRewardsPanelOpened(RewardType type)
    {
        _rewardsPanelOpen = true;
    }

    private void HandleRewardsPanelClosed()
    {
        _rewardsPanelOpen = false;
    }

    #endregion                      //----------------------------------------
}