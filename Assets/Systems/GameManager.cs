using UnityEngine;
using System;

public enum GameState
{
    MainMenu,
    LoadingStage,
    Playing,
    Paused,
    Victory,
    Defeat
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState CurrentState { get; private set; }

    public event Action<GameState> OnGameStateChanged;

    public void Initialize( )
    {
        CurrentState = GameState.MainMenu;

        Debug.Log("GameManager initialized. Current State: " + CurrentState);
    }

    private void Awake( )
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ChangeState(GameState newState)
    {
        CurrentState = newState;
        OnGameStateChanged?.Invoke(newState);
    }
}