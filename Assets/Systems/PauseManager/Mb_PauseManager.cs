using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the pause state of the game.
/// Only allows pausing during active gameplay (GameState.Playing).
/// Other components should subscribe to OnPaused/OnResumed to react to pause state changes,
/// rather than polling IsPaused every Update.
/// </summary>
public class Mb_PauseManager : MonoBehaviour
{
    public static Mb_PauseManager Instance { get; private set; }

    public static bool IsPaused { get; private set; }


    // Subscribe to these events to react to pause/resume (e.g. freeze AI, block input)
    public static event Action OnPaused;
    public static event Action OnResumed;


    private InputActionMap playerMap;
    private InputAction pauseAction;



    private void Awake()
    {
        // Singleton setup — only one PauseManager should exist at a time
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        IsPaused = false; // Ensure game starts unpaused
    }


    private void OnEnable()
    {
        playerMap = InputSystem.actions.FindActionMap("Player");
        pauseAction = playerMap.FindAction("Pause");

        pauseAction.performed += ctx => SetPause(!IsPaused); // Toggle pause on button press
    }

    private void OnDisable()
    {
        pauseAction.performed -= ctx => SetPause(!IsPaused);
    }


    /// <summary>
    /// Pauses or resumes the game. Only works during GameState.Playing.
    /// Can also be called directly from a UI button.
    /// </summary>
    public void SetPause(bool pause)
    {
        Debug.Log(pause ? "Pausing game..." : "Resuming game...");
        Debug.Log($"GameState: {GameManager.Instance.CurrentState}");

        // Guard: don't allow pausing outside of active gameplay and don't allow resuming if not currently paused
        if (pause == true && GameManager.Instance.CurrentState != GameState.Playing ) return;
        if (pause == false && GameManager.Instance.CurrentState != GameState.Paused) return;

        // Prevent firing the same state twice (e.g. pausing when already paused)
        if (IsPaused == pause) return;

        IsPaused = pause;

        // timeScale = 0 freezes Update, FixedUpdate, and coroutines automatically.
        // This means ability cooldown coroutines in Sc_BaseAbility freeze for free.
        Time.timeScale = pause ? 0f : 1f;

        // Pause audio globally
        AudioListener.pause = pause;

        // Notify all subscribers (Movement, AbilityController, CuBot AI, etc.)
        if (pause)
        {
            OnPaused?.Invoke();
            GameManager.Instance.ChangeState(GameState.Paused);
        }
        else
        {
            OnResumed?.Invoke();
            GameManager.Instance.ChangeState(GameState.Playing);
        }
        Debug.Log($"GameState: {GameManager.Instance.CurrentState}");
    }
}