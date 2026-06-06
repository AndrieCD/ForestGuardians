using UnityEngine;
using System;

public enum GameState
{
    MainMenu,
    LoadingStage,
    Playing,
    RewardsPanel,
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

        HandleMouseVisibilityAndLock(newState);
    }

    private void HandleMouseVisibilityAndLock(GameState state)
    {
        switch (state)
        {
            case GameState.MainMenu:
            case GameState.RewardsPanel:
            case GameState.Victory:
            case GameState.Defeat:
            case GameState.Paused:
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                break;
            case GameState.Playing:
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
                break;
        }
    }
}