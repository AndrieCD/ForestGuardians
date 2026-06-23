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
//   - Gap between lines: a short configurable delay separates each dialog so
//     back-to-back lines don't feel like they are running together.
//
// INSPECTOR SETUP:
//   - DialogUI: drag the child DialogPanel GameObject (which has Mb_DialogUI on it).
//   - FallbackDismissDuration: seconds to wait before auto-dismiss when VoiceClip is null.
//     TODO: 3f is a reasonable starting point — tune per dialog pacing.
//   - LinePause: seconds of silence between consecutive dialog lines.
//     TODO: 0.6f feels like a natural breath between lines — tune to taste.

using System.Collections;
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

    [Header("Timing")]
    [Tooltip("Seconds before auto-dismiss when a dialog has no VoiceClip assigned. " +
             "Does not apply to tutorial instructions — those always wait for CompleteInstruction().")]
    // TODO: Tune this value — 3f is a safe starting point for average-length dialog lines.
    [SerializeField] private float _FallbackDismissDuration = 3f;

    // *** NEW ***
    [Tooltip("Seconds of silence between consecutive dialog lines. " +
             "The panel hides during this gap so each line feels distinct. " +
             "Set to 0 to disable the gap entirely.")]
    // TODO: 0.6f is a comfortable breath between lines. Raise to 1f for a more
    // cinematic feel, lower to 0.3f if the pacing feels too slow.
    [SerializeField] private float _LinePause = 0.6f;

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

    // *** NEW ***
    // Handle for the gap coroutine so we can cancel it cleanly if ClearAll() is called
    // mid-gap — prevents the next dialog from playing after a forced clear.
    private Coroutine _linePauseCoroutine = null;

    // *** NEW ***
    // True while the gap coroutine is running — blocks TryPlayNext() from firing
    // a second time during the pause window.
    private bool _inLinePause = false;

    #endregion                  //----------------------------------------


    #region Unity Lifecycle     //----------------------------------------

    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();

        if (_audioSource == null)
            Debug.LogError("[Mb_DialogManager] No AudioSource found on this GameObject. " +
                           "Add one in the Inspector.");

        if (_DialogUI == null)
            Debug.LogError("[Mb_DialogManager] DialogUI is not assigned in the Inspector.");

        _DialogUI?.Hide();
    }


    private void OnEnable()
    {
        Mb_PauseManager.OnPaused += HandlePause;
        Mb_PauseManager.OnResumed += HandleResume;
        GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;

        HandleGameStateChanged(GameState.Playing);
    }


    private void OnDisable()
    {
        Mb_PauseManager.OnPaused -= HandlePause;
        Mb_PauseManager.OnResumed -= HandleResume;

        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
    }


    private void Update()
    {
        if (!_dialogActive) return;

        // --- Fallback timer path (no VoiceClip) ---
        if (_usingFallbackTimer)
        {
            if (_activeDialog != null && _activeDialog.IsTutorialInstruction)
            {
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

        if (!_audioSource.isPlaying && _dialogActive && !_waitingForCompletion)
        {
            if (_activeDialog != null && _activeDialog.IsTutorialInstruction)
            {
                EnterWaitForCompletion();
            }
            else
            {
                DismissActive();
            }
        }
    }

    #endregion                  //----------------------------------------


    #region Public API          //----------------------------------------

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


    public void CompleteInstruction(string eventKey)
    {
        if (!_waitingForCompletion) return;
        if (_activeDialog == null) return;

        if (_activeDialog.CompletionEventKey == eventKey)
            DismissActive();
    }


    public void ClearAll()
    {
        _queue.Clear();

        // *** NEW ***
        // Cancel any in-flight gap coroutine so the next dialog does not
        // play after a forced clear — e.g. on scene transition or stage end.
        if (_linePauseCoroutine != null)
        {
            StopCoroutine(_linePauseCoroutine);
            _linePauseCoroutine = null;
        }
        _inLinePause = false;

        if (_dialogActive)
            ForceStopActive();
    }

    #endregion                  //----------------------------------------


    #region Playback Internals  //----------------------------------------

    private void TryPlayNext()
    {
        // Already showing something — let it finish naturally
        if (_dialogActive) return;

        // *** NEW ***
        // A gap coroutine is running between lines — let it finish.
        // The coroutine will call TryPlayNext() itself when the pause expires.
        if (_inLinePause) return;

        if (_queue.Count == 0) return;

        SO_Dialog next = _queue.Dequeue();
        PlayDialog(next);
    }


    private void PlayDialog(SO_Dialog dialog)
    {
        _activeDialog = dialog;
        _dialogActive = true;
        _waitingForCompletion = false;
        _usingFallbackTimer = false;
        _fallbackTimer = 0f;

        if (_panelAllowed)
            _DialogUI?.Show(dialog);

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
                if (!dialog.IsTutorialInstruction)
                {
                    _usingFallbackTimer = true;
                    _fallbackTimer = _FallbackDismissDuration;
                }
                else
                {
                    EnterWaitForCompletion();
                }
            }
        }
    }


    private void EnterWaitForCompletion()
    {
        _waitingForCompletion = true;
        _DialogUI?.SetContinueIndicatorVisible(true);
    }


    private void DismissActive()
    {
        _activeDialog = null;
        _dialogActive = false;
        _waitingForCompletion = false;
        _usingFallbackTimer = false;
        _fallbackTimer = 0f;

        _audioSource?.Stop();
        _DialogUI?.Hide();

        // *** NEW ***
        // Insert a gap before the next line instead of chaining immediately.
        // If _LinePause is zero or negative, skip straight to the next line —
        // this makes it easy to disable the gap without changing code.
        if (_LinePause > 0f && _queue.Count > 0)
        {
            _linePauseCoroutine = StartCoroutine(LinePauseRoutine());
        }
        else
        {
            TryPlayNext();
        }
    }


    // *** NEW ***
    // Waits _LinePause seconds with the panel hidden, then plays the next line.
    // Using WaitForSecondsRealtime so the gap survives Time.timeScale changes —
    // though dialog typically only plays while unpaused.
    // If you want the gap to freeze during pause, swap to WaitForSeconds instead.
    private IEnumerator LinePauseRoutine()
    {
        _inLinePause = true;

        yield return new WaitForSeconds(_LinePause);

        _inLinePause = false;
        _linePauseCoroutine = null;

        TryPlayNext();
    }


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
        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Pause();
    }


    private void HandleResume()
    {
        if (_audioSource != null && _dialogActive && !_waitingForCompletion)
            _audioSource.UnPause();
    }


    private void HandleGameStateChanged(GameState newState)
    {
        _panelAllowed = newState == GameState.Playing
                     || newState == GameState.Paused
                     || newState == GameState.RewardsPanel;

        if (_dialogActive)
        {
            if (_panelAllowed)
                _DialogUI?.Show(_activeDialog);
            else
                _DialogUI?.Hide();
        }
    }

    #endregion                  //----------------------------------------
}