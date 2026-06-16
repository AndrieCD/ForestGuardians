// Mb_DialogManager.cs
// Singleton manager that owns the dialog queue and drives playback.
// Lives on the HUD Canvas — scene-specific, no DontDestroyOnLoad.
//
// HOW IT WORKS:
//   - Other systems call EnqueueDialog() or EnqueueSequence() to push dialogs in.
//   - One dialog is active at a time. If one is already playing, new entries wait.
//   - When a dialog becomes active: Mb_DialogUI.Show() is called, VoiceClip plays.
//   - Auto-dismiss: AudioSource.isPlaying is polled in Update() (guarded by
//     _dialogActive bool so Update() is cheap when nothing is playing).
//   - Tutorial instructions: stay open after audio ends until CompleteInstruction()
//     is called with the matching key. ContinueIndicator shown during this wait.
//   - Pause-safe: AudioSource paused/resumed via Mb_PauseManager events.
//   - Visibility: panel shown only during Playing and Paused game states.
//
// INSPECTOR SETUP:
//   - DialogUI: drag the child DialogPanel GameObject (which has Mb_DialogUI on it).
//   - FallbackDismissDuration: seconds to wait before auto-dismiss when VoiceClip is null.
//     TODO: 3f is a reasonable starting point — tune per dialog pacing.

using System.Collections.Generic;
using UnityEngine;

public class Mb_DialogManager : MonoBehaviour
{
    #region Singleton           //----------------------------------------

    public static Mb_DialogManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    #endregion                  //----------------------------------------


    #region Inspector Fields    //----------------------------------------

    [Header("UI")]
    [Tooltip("The child DialogPanel GameObject that has Mb_DialogUI on it.")]
    [SerializeField] private Mb_DialogUI _DialogUI;

    [Header("Fallback")]
    [Tooltip("Seconds before auto-dismiss when a dialog has no VoiceClip assigned. " +
             "Does not apply to tutorial instructions — those always wait for CompleteInstruction().")]
    // TODO: Tune this value — 3f is a safe starting point for average-length dialog lines.
    [SerializeField] private float _FallbackDismissDuration = 3f;

    #endregion                  //----------------------------------------


    #region Private State       //----------------------------------------

    // The main queue — dialogs wait here until the active one is dismissed
    private Queue<SO_Dialog> _queue = new Queue<SO_Dialog>();

    // The dialog currently being displayed
    private SO_Dialog _activeDialog = null;

    // True while a dialog is on screen — gates the Update() polling check
    private bool _dialogActive = false;

    public bool IsDialogActive => _dialogActive;

    // True when audio has finished but we are waiting for a tutorial completion key
    private bool _waitingForCompletion = false;

    // Tracks elapsed time for the fallback dismiss timer (no VoiceClip case)
    private float _fallbackTimer = 0f;
    private bool _usingFallbackTimer = false;

    // AudioSource lives on this same GameObject — one central audio player for all dialog
    private AudioSource _audioSource;

    // Whether dialog panel should be visible at all in the current game state
    private bool _panelAllowed = false;

    #endregion                  //----------------------------------------


    #region Unity Lifecycle     //----------------------------------------

    private void Start()
    {
        // Fetch AudioSource on this GameObject — must be added in the Inspector
        _audioSource = GetComponent<AudioSource>();

        if (_audioSource == null)
            Debug.LogError("[Mb_DialogManager] No AudioSource found on this GameObject. " +
                           "Add one in the Inspector.");

        if (_DialogUI == null)
            Debug.LogError("[Mb_DialogManager] DialogUI is not assigned in the Inspector.");

        // Start hidden — UpdatePanelVisibility will show it when state allows
        _DialogUI?.Hide();
    }


    private void OnEnable()
    {
        Mb_PauseManager.OnPaused += HandlePause;
        Mb_PauseManager.OnResumed += HandleResume;
        GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;


        // On Scene load (dialog manager is not active yet when state is changed to playing
        // explicit gamestatechaned here
        HandleGameStateChanged(GameState.Playing);
    }


    private void OnDisable()
    {
        Mb_PauseManager.OnPaused -= HandlePause;
        Mb_PauseManager.OnResumed -= HandleResume;

        // Guard: GameManager may already be destroyed on scene teardown
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
    }


    private void Update()
    {
        // Only poll when a dialog is active — keeps Update() free otherwise
        if (!_dialogActive) return;

        // --- Fallback timer path (no VoiceClip) ---
        if (_usingFallbackTimer)
        {
            // Tutorial instructions ignore the fallback timer — they always
            // wait for CompleteInstruction() regardless of clip presence
            if (_activeDialog != null && _activeDialog.IsTutorialInstruction)
            {
                // Switch to waiting-for-completion mode immediately
                _usingFallbackTimer = false;
                EnterWaitForCompletion();
                return;
            }

            _fallbackTimer -= Time.deltaTime;

            if (_fallbackTimer <= 0f)
                DismissActive();

            return;
        }

        // --- AudioSource polling path ---
        if (_audioSource == null) return;

        // Audio has finished playing
        if (!_audioSource.isPlaying && _dialogActive && !_waitingForCompletion)
        {
            if (_activeDialog != null && _activeDialog.IsTutorialInstruction)
            {
                // Audio done but this is a tutorial instruction — wait for the player
                EnterWaitForCompletion();
            }
            else
            {
                // Normal dialog — dismiss as soon as audio ends
                DismissActive();
            }
        }
    }

    #endregion                  //----------------------------------------


    #region Public API          //----------------------------------------

    /// <summary>
    /// Enqueues a single dialog. Plays immediately if nothing is active.
    /// Safe to call while another dialog is playing — it will wait in the queue.
    /// </summary>
    public void EnqueueDialog(SO_Dialog dialog)
    {
        if (dialog == null)
        {
            Debug.LogWarning("[Mb_DialogManager] EnqueueDialog called with null — skipping.");
            return;
        }

        _queue.Enqueue(dialog);
        TryPlayNext();
    }


    /// <summary>
    /// Enqueues all dialogs in a sequence in order.
    /// Safe to call while another dialog is playing.
    /// </summary>
    public void EnqueueSequence(SO_DialogSequence sequence)
    {
        if (sequence == null)
        {
            Debug.LogWarning("[Mb_DialogManager] EnqueueSequence called with null — skipping.");
            return;
        }

        if (sequence.Dialogs == null || sequence.Dialogs.Count == 0)
        {
            Debug.LogWarning($"[Mb_DialogManager] Sequence '{sequence.name}' has no dialogs.");
            return;
        }

        foreach (SO_Dialog dialog in sequence.Dialogs)
            _queue.Enqueue(dialog);

        TryPlayNext();
    }


    /// <summary>
    /// Fires a completion event by key.
    /// If the active dialog is a tutorial instruction with a matching CompletionEventKey,
    /// it is dismissed immediately. Otherwise the call is silently ignored.
    /// </summary>
    public void CompleteInstruction(string eventKey)
    {
        if (!_waitingForCompletion) return;
        if (_activeDialog == null) return;

        if (_activeDialog.CompletionEventKey == eventKey)
        {
            DismissActive();
        }
    }


    /// <summary>
    /// Immediately clears the active dialog and empties the queue.
    /// Use on scene transitions or stage end.
    /// </summary>
    public void ClearAll()
    {
        _queue.Clear();

        if (_dialogActive)
            ForceStopActive();
    }

    #endregion                  //----------------------------------------


    #region Playback Internals  //----------------------------------------

    // Starts the next queued dialog if nothing is currently active.
    private void TryPlayNext()
    {
        // Already showing something — let it finish naturally
        if (_dialogActive) return;

        if (_queue.Count == 0) return;

        SO_Dialog next = _queue.Dequeue();
        PlayDialog(next);
    }


    // Activates a dialog: populates UI, plays audio, starts dismiss logic.
    private void PlayDialog(SO_Dialog dialog)
    {
        _activeDialog = dialog;
        _dialogActive = true;
        _waitingForCompletion = false;
        _usingFallbackTimer = false;
        _fallbackTimer = 0f;

        // Show UI only if the current game state permits it
        if (_panelAllowed)
            _DialogUI?.Show(dialog);

        // Play voice clip if one is provided
        if (_audioSource != null)
        {
            _audioSource.Stop();

            if (dialog.VoiceClip != null)
            {
                _audioSource.clip = dialog.VoiceClip;
                _audioSource.Play();
            }
            else
            {
                // No clip — use fallback timer unless it's a tutorial instruction,
                // which transitions to wait-for-completion in Update()
                if (!dialog.IsTutorialInstruction)
                {
                    _usingFallbackTimer = true;
                    _fallbackTimer = _FallbackDismissDuration;
                }
                else
                {
                    // Tutorial instruction with no clip — go straight to waiting
                    EnterWaitForCompletion();
                }
            }
        }
    }


    // Transitions a tutorial instruction into the "waiting for player action" state.
    // Called when audio ends (or immediately if no clip) on an IsTutorialInstruction dialog.
    private void EnterWaitForCompletion()
    {
        _waitingForCompletion = true;

        // Show the continue indicator so the player knows they need to act
        _DialogUI?.SetContinueIndicatorVisible(true);
    }


    // Dismisses the active dialog and tries to play the next one.
    private void DismissActive()
    {
        _activeDialog = null;
        _dialogActive = false;
        _waitingForCompletion = false;
        _usingFallbackTimer = false;
        _fallbackTimer = 0f;

        _audioSource?.Stop();
        _DialogUI?.Hide();

        // Chain to the next dialog in the queue if one is waiting
        TryPlayNext();
    }


    // Hard stop with no chaining — used by ClearAll().
    private void ForceStopActive()
    {
        _activeDialog = null;
        _dialogActive = false;
        _waitingForCompletion = false;
        _usingFallbackTimer = false;
        _fallbackTimer = 0f;

        _audioSource?.Stop();
        _DialogUI?.Hide();
    }

    #endregion                  //----------------------------------------


    #region Pause & State Handling  //------------------------------------

    private void HandlePause()
    {
        // Pause the voice clip in place — it will resume from the same position
        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Pause();
    }


    private void HandleResume()
    {
        // Resume only if a dialog is active and had a clip playing
        if (_audioSource != null && _dialogActive && !_waitingForCompletion)
            _audioSource.UnPause();
    }


    private void HandleGameStateChanged(GameState newState)
    {
        // Dialog panel is visible during Playing and Paused only
        _panelAllowed = newState == GameState.Playing || newState == GameState.Paused || newState == GameState.RewardsPanel;

        if (_dialogActive)
        {
            // Show or hide the panel based on whether the state allows it,
            // but don't dismiss — the dialog keeps its position in playback
            if (_panelAllowed)
                _DialogUI?.Show(_activeDialog);
            else
                _DialogUI?.Hide();
        }
    }

    #endregion                  //----------------------------------------
}