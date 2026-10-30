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

    // Requests remain intact in this queue. Dialogs are advanced within their
    // owning request instead of being flattened into one unrelated queue.
    private readonly List<DialogPlaybackRequest> _queue
        = new List<DialogPlaybackRequest>();

    private DialogPlaybackRequest _activeRequest;
    private int _nextRequestId = 1;

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

    // AudioSource lives on this same GameObject — one central audio player for all dialog.
    private Sc_DialogAudioController _audioController;

    // Whether dialog panel should be visible at all in the current game state
    private bool _panelAllowed = false;

    #endregion                  //----------------------------------------


    #region Unity Lifecycle     //----------------------------------------

    private void Start()
    {
        // Fetch AudioSource on this GameObject — must be added in the Inspector
        AudioSource audioSource = GetComponent<AudioSource>();
        _audioController = new Sc_DialogAudioController(audioSource);

        if (!_audioController.HasAudioSource)
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

        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;


        // Refresh from the real state so enabling the component does not
        // temporarily force a paused/reward/cutscene scene into Playing UI.
        HandleGameStateChanged(
            GameManager.Instance != null
                ? GameManager.Instance.CurrentState
                : GameState.Playing);
    }


    private void OnDisable()
    {
        Mb_PauseManager.OnPaused -= HandlePause;
        Mb_PauseManager.OnResumed -= HandleResume;

        // Guard: GameManager may already be destroyed on scene teardown
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;

        CancelAllRequests("dialog manager disabled or scene unloaded");
    }


    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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
        // Audio has finished playing
        if (_audioController == null || _audioController.IsPlaying) return;

        if (_dialogActive && !_waitingForCompletion)
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

        RequestDialog(
            dialog,
            new Sc_DialogPlaybackOptions
            {
                Source = "Compatibility.EnqueueDialog"
            });
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

        RequestSequence(
            sequence,
            new Sc_DialogPlaybackOptions
            {
                Source = $"Compatibility.EnqueueSequence:{sequence.name}"
            });
    }


    /// <summary>
    /// Creates a request for one dialog and returns its lifecycle handle.
    /// </summary>
    public Sc_DialogPlaybackHandle RequestDialog(
        SO_Dialog dialog,
        Sc_DialogPlaybackOptions options = null)
    {
        if (dialog == null)
        {
            Debug.LogWarning("[Mb_DialogManager] Rejected dialog request: dialog is null.");
            return null;
        }

        return EnqueueRequest(new List<SO_Dialog> { dialog }, options, null);
    }


    /// <summary>
    /// Creates an owned request for a complete sequence and returns its lifecycle handle.
    /// </summary>
    public Sc_DialogPlaybackHandle RequestSequence(
        SO_DialogSequence sequence,
        Sc_DialogPlaybackOptions options = null)
    {
        if (sequence == null)
        {
            Debug.LogWarning("[Mb_DialogManager] Rejected sequence request: sequence is null.");
            return null;
        }

        if (sequence.Dialogs == null || sequence.Dialogs.Count == 0)
        {
            Debug.LogWarning($"[Mb_DialogManager] Rejected sequence request: " +
                             $"sequence '{sequence.name}' has no dialogs.");
            return null;
        }

        List<SO_Dialog> dialogs = new List<SO_Dialog>(sequence.Dialogs.Count);

        foreach (SO_Dialog dialog in sequence.Dialogs)
        {
            if (dialog == null)
            {
                Debug.LogWarning($"[Mb_DialogManager] Sequence '{sequence.name}' " +
                                 "contains a null dialog entry; skipping it.");
                continue;
            }

            dialogs.Add(dialog);
        }

        return EnqueueRequest(dialogs, options, sequence);
    }


    /// <summary>
    /// Fires a completion event by key.
    /// If the active dialog is a tutorial instruction with a matching CompletionEventKey,
    /// it is dismissed immediately. Otherwise the call is silently ignored.
    /// </summary>
    public void CompleteInstruction(string eventKey)
    {
        CompleteInstruction(_activeRequest?.Handle, eventKey);
    }


    /// <summary>
    /// Completes the active tutorial instruction using a typed event.
    /// </summary>
    public bool CompleteInstruction(E_DialogCompletionEvent completionEvent)
    {
        if (!Sc_DialogCompletionContract.TryCreate(
                completionEvent,
                out Sc_DialogCompletionContract contract))
        {
            Debug.LogWarning($"[Mb_DialogManager] Rejected invalid tutorial completion " +
                             $"event '{completionEvent}'.");
            return false;
        }

        return CompleteInstruction(_activeRequest?.Handle, contract);
    }


    /// <summary>
    /// Completes a tutorial instruction only when the supplied request owns it.
    /// </summary>
    public bool CompleteInstruction(
        Sc_DialogPlaybackHandle handle,
        string eventKey)
    {
        if (!Sc_DialogCompletionContract.TryCreate(
                eventKey,
                out Sc_DialogCompletionContract contract))
        {
            Debug.LogWarning($"[Mb_DialogManager] Rejected invalid tutorial completion " +
                             $"key '{eventKey ?? "null"}'.");
            return false;
        }

        return CompleteInstruction(handle, contract);
    }


    /// <summary>
    /// Completes a tutorial instruction using a validated completion contract.
    /// </summary>
    public bool CompleteInstruction(
        Sc_DialogPlaybackHandle handle,
        Sc_DialogCompletionContract contract)
    {
        if (handle == null || _activeRequest == null ||
            _activeRequest.Handle != handle || !_waitingForCompletion ||
            _activeDialog == null)
        {
            Debug.LogWarning("[Mb_DialogManager] Rejected tutorial completion: " +
                             "the supplied request is not the active waiting request.");
            return false;
        }

        if (_activeDialog.CompletionEventKey != contract.Key)
        {
            Debug.LogWarning($"[Mb_DialogManager] Rejected tutorial completion for " +
                             $"request #{handle.RequestId}: event key did not match.");
            return false;
        }

        DismissActive();
        return true;
    }


    /// <summary>
    /// Immediately clears the active dialog and empties the queue.
    /// Use on scene transitions or stage end.
    /// </summary>
    public void ClearAll()
    {
        CancelAllRequests("ClearAll called");
    }

    #endregion                  //----------------------------------------


    #region Playback Internals  //----------------------------------------

    // Starts the next queued dialog if nothing is currently active.
    private void TryPlayNext()
    {
        // Already showing something — let it finish naturally
        if (_dialogActive) return;

        if (_queue.Count == 0) return;

        _activeRequest = _queue[0];
        _queue.RemoveAt(0);
        _activeRequest.Handle.MarkPlaying();
        PlayNextDialogInActiveRequest();
    }


    private Sc_DialogPlaybackHandle EnqueueRequest(
        List<SO_Dialog> dialogs,
        Sc_DialogPlaybackOptions options,
        SO_DialogSequence sequence = null)
    {
        if (dialogs == null || dialogs.Count == 0)
        {
            Debug.LogWarning("[Mb_DialogManager] Rejected request: " +
                             "it contains no valid dialogs.");
            return null;
        }

        Sc_DialogPlaybackOptions requestOptions = options ?? new Sc_DialogPlaybackOptions();
        string source = string.IsNullOrWhiteSpace(requestOptions.Source)
            ? "Unknown"
            : requestOptions.Source;

        DialogPlaybackRequest request = null;
        Sc_DialogPlaybackHandle handle = new Sc_DialogPlaybackHandle(
            _nextRequestId,
            source,
            () => CancelRequest(request),
            () => SkipCurrentDialog(request));

        request = new DialogPlaybackRequest(
            _nextRequestId++, source, sequence, dialogs, requestOptions, handle);

        _queue.Add(request);
        Debug.Log($"[Mb_DialogManager] Accepted request #{request.Id} from " +
                  $"'{request.Source}' with {request.Dialogs.Count} dialog(s).");
        TryPlayNext();
        return handle;
    }


    private void PlayNextDialogInActiveRequest()
    {
        if (_activeRequest == null)
        {
            _dialogActive = false;
            TryPlayNext();
            return;
        }

        if (_activeRequest.NextDialogIndex >= _activeRequest.Dialogs.Count)
        {
            CompleteActiveRequest();
            return;
        }

        SO_Dialog next = _activeRequest.Dialogs[_activeRequest.NextDialogIndex++];
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

        // Populate presentation once, then control visibility separately.
        _DialogUI?.Show(dialog);
        _DialogUI?.SetPanelVisible(_panelAllowed);

        // Play voice clip if one is provided
        if (_audioController != null && _audioController.HasAudioSource)
        {
            if (dialog.VoiceClip != null)
            {
                _audioController.Play(dialog.VoiceClip);
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
        else if (!dialog.IsTutorialInstruction)
        {
            Debug.LogError("[Mb_DialogManager] AudioSource is missing. " +
                           "Using the configured fallback duration for the active dialog.");
            _usingFallbackTimer = true;
            _fallbackTimer = _FallbackDismissDuration;
        }
        else
        {
            Debug.LogError("[Mb_DialogManager] AudioSource is missing for a tutorial " +
                           "instruction; waiting for its completion event.");
            EnterWaitForCompletion();
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

        _audioController?.Stop();
        _DialogUI?.Hide();

        if (_activeRequest != null &&
            _activeRequest.NextDialogIndex < _activeRequest.Dialogs.Count)
        {
            _dialogActive = true;
            PlayNextDialogInActiveRequest();
            return;
        }

        CompleteActiveRequest();
    }


    private void CompleteActiveRequest()
    {
        if (_activeRequest != null)
        {
            Debug.Log($"[Mb_DialogManager] Completed request #{_activeRequest.Id} " +
                      $"from '{_activeRequest.Source}'.");
            _activeRequest.Handle.Complete(
                _activeRequest.WasSkipped
                    ? E_DialogPlaybackStatus.Skipped
                    : E_DialogPlaybackStatus.Completed,
                _activeRequest.WasSkipped
                    ? "One or more dialogs were skipped."
                    : "Sequence playback completed.");
        }

        _activeRequest = null;
        _dialogActive = false;
        _activeDialog = null;
        _waitingForCompletion = false;
        _usingFallbackTimer = false;
        _fallbackTimer = 0f;
        _audioController?.Stop();
        _DialogUI?.Hide();
        TryPlayNext();
    }


    private void CancelAllRequests(string reason)
    {
        if (_activeRequest != null)
        {
            Debug.LogWarning($"[Mb_DialogManager] Cancelled request #{_activeRequest.Id} " +
                             $"from '{_activeRequest.Source}': {reason}.");
            _activeRequest.Handle.Complete(E_DialogPlaybackStatus.Cancelled, reason);
        }

        while (_queue.Count > 0)
        {
            DialogPlaybackRequest request = _queue[0];
            _queue.RemoveAt(0);
            Debug.LogWarning($"[Mb_DialogManager] Cancelled queued request #{request.Id} " +
                             $"from '{request.Source}': {reason}.");
            request.Handle.Complete(E_DialogPlaybackStatus.Cancelled, reason);
        }

        _activeRequest = null;
        _activeDialog = null;
        _dialogActive = false;
        _waitingForCompletion = false;
        _usingFallbackTimer = false;
        _fallbackTimer = 0f;
        _audioController?.Stop();
        _DialogUI?.Hide();
    }


    private bool CancelRequest(DialogPlaybackRequest request)
    {
        if (request == null || request.Handle.IsCompleted)
            return false;

        if (_activeRequest == request)
        {
            string reason = "cancelled by request owner";
            Debug.LogWarning($"[Mb_DialogManager] Cancelled active request #{request.Id} " +
                             $"from '{request.Source}': {reason}.");
            request.Handle.Complete(E_DialogPlaybackStatus.Cancelled, reason);
            StopActivePlayback();
            _activeRequest = null;
            TryPlayNext();
            return true;
        }

        if (_queue.Remove(request))
        {
            Debug.LogWarning($"[Mb_DialogManager] Cancelled queued request #{request.Id} " +
                             $"from '{request.Source}': cancelled by request owner.");
            request.Handle.Complete(
                E_DialogPlaybackStatus.Cancelled,
                "Cancelled before playback started.");
            return true;
        }

        return false;
    }


    private bool SkipCurrentDialog(DialogPlaybackRequest request)
    {
        if (request == null || _activeRequest != request ||
            !request.Options.AllowSkipping || !_dialogActive)
        {
            return false;
        }

        request.WasSkipped = true;
        Debug.Log($"[Mb_DialogManager] Skipping current dialog for request #{request.Id}.");
        DismissActive();
        return true;
    }


    private void StopActivePlayback()
    {
        _activeDialog = null;
        _dialogActive = false;
        _waitingForCompletion = false;
        _usingFallbackTimer = false;
        _fallbackTimer = 0f;
        _audioController?.Stop();
        _DialogUI?.Hide();
    }

    #endregion                  //----------------------------------------


    #region Pause & State Handling  //------------------------------------

    private void HandlePause()
    {
        // Pause the voice clip in place — it will resume from the same position.
        _audioController?.Pause();
    }


    private void HandleResume()
    {
        // Resume only if a dialog is active and had a clip playing
        if (_dialogActive && !_waitingForCompletion)
            _audioController?.Resume();
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
            {
                _DialogUI?.SetPanelVisible(true);
                if (_waitingForCompletion)
                    _DialogUI?.SetContinueIndicatorVisible(true);
            }
            else
                _DialogUI?.SetPanelVisible(false);
        }
    }


    private sealed class DialogPlaybackRequest
    {
        public readonly int Id;
        public readonly string Source;
        public readonly SO_DialogSequence Sequence;
        public readonly List<SO_Dialog> Dialogs;
        public readonly Sc_DialogPlaybackOptions Options;
        public readonly Sc_DialogPlaybackHandle Handle;
        public int NextDialogIndex;
        public bool WasSkipped;

        public DialogPlaybackRequest(
            int id,
            string source,
            SO_DialogSequence sequence,
            List<SO_Dialog> dialogs,
            Sc_DialogPlaybackOptions options,
            Sc_DialogPlaybackHandle handle)
        {
            Id = id;
            Source = source;
            Sequence = sequence;
            Dialogs = dialogs;
            Options = options;
            Handle = handle;
            NextDialogIndex = 0;
            WasSkipped = false;
        }
    }

    #endregion                  //----------------------------------------
}
