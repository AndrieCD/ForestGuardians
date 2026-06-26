// Mb_CoreDebugMonitor.cs
// Centralized developer diagnostics for core game flow.
//
// Purpose:
//   - Track major runtime events from one place.
//   - Keep gameplay scripts free from scattered Debug.Log calls.
//   - Provide live Inspector state while testing in Play Mode.
//
// Inspector setup:
//   - Add this to a Bootstrap or Stage scene GameObject.
//   - Enable only the categories you are actively debugging.
//   - Disable EnableConsoleLogging to use the Inspector-only event history.

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Mb_CoreDebugMonitor : MonoBehaviour
{
    public static Mb_CoreDebugMonitor Instance { get; private set; }

    [Header("Logging")]
    [SerializeField] private bool EnableConsoleLogging = true;
    [SerializeField] private bool IncludeFrameCount = true;
    [SerializeField] private int MaxStoredEvents = 20;

    [Header("Categories")]
    [SerializeField] private bool TrackSceneFlow = true;
    [SerializeField] private bool TrackGameState = true;
    [SerializeField] private bool TrackStageFlow = true;
    [SerializeField] private bool TrackWaveFlow = true;
    [SerializeField] private bool TrackRewards = true;
    [SerializeField] private bool TrackPause = true;
    [SerializeField] private bool TrackEnemies = true;
    [SerializeField] private bool TrackObjective = true;

    [Header("Live State")]
    [SerializeField] private string _CurrentScene;
    [SerializeField] private GameState _CurrentGameState;
    [SerializeField] private WavePhase _CurrentWavePhase;
    [SerializeField] private int _CurrentWaveIndex = -1;
    [SerializeField] private int _ActiveEnemyCount;
    [SerializeField] private int _SpawnedEnemyCount;
    [SerializeField] private int _DefeatedEnemyCount;
    [SerializeField] private bool _IsPaused;

    [Header("Recent Events")]
    [SerializeField, TextArea(8, 16)] private string _RecentEvents;

    private readonly Queue<string> _eventHistory = new Queue<string>();
    private bool _subscribedToGameManager;
    private bool _isDuplicate;

    public bool IsPaused => _IsPaused;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            _isDuplicate = true;
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _CurrentScene = SceneManager.GetActiveScene().name;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (_isDuplicate) return;

        SceneManager.sceneLoaded += HandleSceneLoaded;

        Mb_StageManager.OnStageStart += HandleStageStart;
        Mb_StageManager.OnStageEnd += HandleStageEnd;

        Mb_WaveManager.OnPhaseChanged += HandleWavePhaseChanged;
        Mb_WaveManager.OnPreparationTick += HandlePreparationTick;
        Mb_WaveManager.OnWaveStart += HandleWaveStart;
        Mb_WaveManager.OnWaveEnd += HandleWaveEnd;
        Mb_WaveManager.OnEnemySpawned += HandleEnemySpawned;
        Mb_WaveManager.OnWaveSpawnComplete += HandleWaveSpawnComplete;
        Mb_WaveManager.OnAllWavesCleared += HandleAllWavesCleared;

        Mb_RewardsManager.OnRewardsPanelOpened += HandleRewardsPanelOpened;
        Mb_RewardsManager.OnRewardChosen += HandleRewardChosen;
        Mb_RewardsManager.OnRewardsPanelClosed += HandleRewardsPanelClosed;

        Mb_PauseManager.OnPaused += HandlePaused;
        Mb_PauseManager.OnResumed += HandleResumed;

        MB_CuBotBase.OnCuBotDeath += HandleCuBotDeath;
        MB_CuBotBase.OnCuBotKill += HandleCuBotKill;

        Mb_PanoharraManager.OnPanoharraDestroyed += HandlePanoharraDestroyed;
        Mb_PanoharraManager.OnPanoharraUnderAttack += HandlePanoharraUnderAttack;

        TrySubscribeToGameManager();
    }

    private void Start()
    {
        if (_isDuplicate) return;

        TrySubscribeToGameManager();
        LogSceneFlow($"Monitor ready in scene '{_CurrentScene}'.");
    }

    private void OnDisable()
    {
        if (_isDuplicate) return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;

        Mb_StageManager.OnStageStart -= HandleStageStart;
        Mb_StageManager.OnStageEnd -= HandleStageEnd;

        Mb_WaveManager.OnPhaseChanged -= HandleWavePhaseChanged;
        Mb_WaveManager.OnPreparationTick -= HandlePreparationTick;
        Mb_WaveManager.OnWaveStart -= HandleWaveStart;
        Mb_WaveManager.OnWaveEnd -= HandleWaveEnd;
        Mb_WaveManager.OnEnemySpawned -= HandleEnemySpawned;
        Mb_WaveManager.OnWaveSpawnComplete -= HandleWaveSpawnComplete;
        Mb_WaveManager.OnAllWavesCleared -= HandleAllWavesCleared;

        Mb_RewardsManager.OnRewardsPanelOpened -= HandleRewardsPanelOpened;
        Mb_RewardsManager.OnRewardChosen -= HandleRewardChosen;
        Mb_RewardsManager.OnRewardsPanelClosed -= HandleRewardsPanelClosed;

        Mb_PauseManager.OnPaused -= HandlePaused;
        Mb_PauseManager.OnResumed -= HandleResumed;

        MB_CuBotBase.OnCuBotDeath -= HandleCuBotDeath;
        MB_CuBotBase.OnCuBotKill -= HandleCuBotKill;

        Mb_PanoharraManager.OnPanoharraDestroyed -= HandlePanoharraDestroyed;
        Mb_PanoharraManager.OnPanoharraUnderAttack -= HandlePanoharraUnderAttack;

        UnsubscribeFromGameManager();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        MaxStoredEvents = Mathf.Max(1, MaxStoredEvents);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _CurrentScene = scene.name;
        _ActiveEnemyCount = 0;
        _SpawnedEnemyCount = 0;
        _DefeatedEnemyCount = 0;
        _CurrentWaveIndex = -1;

        LogSceneFlow($"Scene loaded: {scene.name} ({mode}).");
        TrySubscribeToGameManager();
    }

    private void TrySubscribeToGameManager()
    {
        if (_subscribedToGameManager) return;
        if (GameManager.Instance == null) return;

        _CurrentGameState = GameManager.Instance.CurrentState;
        GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        _subscribedToGameManager = true;
    }

    private void UnsubscribeFromGameManager()
    {
        if (!_subscribedToGameManager) return;
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;

        _subscribedToGameManager = false;
    }

    private void HandleGameStateChanged(GameState newState)
    {
        _CurrentGameState = newState;
        LogGameState($"GameState changed to {newState}.");
    }

    private void HandleStageStart()
    {
        LogStageFlow("Stage started.");
    }

    private void HandleStageEnd()
    {
        LogStageFlow("Stage ended.");
    }

    private void HandleWavePhaseChanged(WavePhase phase)
    {
        _CurrentWavePhase = phase;
        LogWaveFlow($"Wave phase changed to {phase}.");
    }

    private void HandlePreparationTick(float remainingSeconds)
    {
        if (remainingSeconds <= 0f)
            LogWaveFlow("Preparation countdown complete.");
    }

    private void HandleWaveStart(int waveIndex)
    {
        _CurrentWaveIndex = waveIndex;
        _ActiveEnemyCount = 0;
        _SpawnedEnemyCount = 0;
        _DefeatedEnemyCount = 0;

        LogWaveFlow($"Wave {waveIndex + 1} started.");
    }

    private void HandleWaveEnd(int waveIndex)
    {
        LogWaveFlow($"Wave {waveIndex + 1} ended. Spawned: {_SpawnedEnemyCount}, defeated: {_DefeatedEnemyCount}.");
    }

    private void HandleEnemySpawned(GameObject enemy)
    {
        _SpawnedEnemyCount++;
        _ActiveEnemyCount++;

        string enemyName = enemy != null ? enemy.name : "Unknown";
        LogEnemies($"Enemy spawned: {enemyName}. Active enemies: {_ActiveEnemyCount}.");
    }

    private void HandleWaveSpawnComplete(int waveIndex)
    {
        LogWaveFlow($"Wave {waveIndex + 1} spawn complete. Total spawned: {_SpawnedEnemyCount}.");
    }

    private void HandleAllWavesCleared()
    {
        LogWaveFlow("All waves cleared.");
    }

    private void HandleRewardsPanelOpened(RewardType rewardType)
    {
        LogRewards($"Rewards panel opened: {rewardType}.");
    }

    private void HandleRewardChosen(RewardOption reward)
    {
        string rewardName = string.IsNullOrWhiteSpace(reward.Name) ? "Unnamed Reward" : reward.Name;
        LogRewards($"Reward chosen: {rewardName}.");
    }

    private void HandleRewardsPanelClosed()
    {
        LogRewards("Rewards panel closed.");
    }

    private void HandlePaused()
    {
        _IsPaused = true;
        LogPause("Game paused.");
    }

    private void HandleResumed()
    {
        _IsPaused = false;
        LogPause("Game resumed.");
    }

    private void HandleCuBotDeath(GameObject deadEnemy)
    {
        _DefeatedEnemyCount++;
        _ActiveEnemyCount = Mathf.Max(0, _ActiveEnemyCount - 1);

        string enemyName = deadEnemy != null ? deadEnemy.name : "Unknown";
        LogEnemies($"Enemy defeated: {enemyName}. Active enemies: {_ActiveEnemyCount}.");
    }

    private void HandleCuBotKill(Mb_CharacterBase attacker)
    {
        if (attacker == null)
        {
            LogEnemies("Enemy kill credited to no attacker.");
            return;
        }

        LogEnemies($"Enemy kill credited to {attacker.CharacterName}.");
    }

    private void HandlePanoharraDestroyed()
    {
        LogObjective("Panoharra destroyed.");
    }

    private void HandlePanoharraUnderAttack()
    {
        LogObjective("Panoharra under attack.");
    }

    private void LogSceneFlow(string message) => Log("Scene", message, TrackSceneFlow);
    private void LogGameState(string message) => Log("GameState", message, TrackGameState);
    private void LogStageFlow(string message) => Log("Stage", message, TrackStageFlow);
    private void LogWaveFlow(string message) => Log("Wave", message, TrackWaveFlow);
    private void LogRewards(string message) => Log("Rewards", message, TrackRewards);
    private void LogPause(string message) => Log("Pause", message, TrackPause);
    private void LogEnemies(string message) => Log("Enemies", message, TrackEnemies);
    private void LogObjective(string message) => Log("Objective", message, TrackObjective);

    private void Log(string category, string message, bool categoryEnabled)
    {
        if (!categoryEnabled) return;

        string prefix = IncludeFrameCount
            ? $"[CoreDebug][{category}][Frame {Time.frameCount}]"
            : $"[CoreDebug][{category}]";

        string entry = $"{prefix} {message}";
        AddToHistory(entry);

        if (EnableConsoleLogging)
            Debug.Log(entry);
    }

    private void AddToHistory(string entry)
    {
        _eventHistory.Enqueue(entry);

        while (_eventHistory.Count > MaxStoredEvents)
            _eventHistory.Dequeue();

        var builder = new StringBuilder();

        foreach (string eventEntry in _eventHistory)
            builder.AppendLine(eventEntry);

        _RecentEvents = builder.ToString();
    }
}
